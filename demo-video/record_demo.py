"""Records a narrated walkthrough of EMS: admin -> HR -> accounts -> employee.

Drives Chromium with Playwright (video on), moves a visible pointer before every click, shows captions,
narrates each step with macOS `say` (voice Tara, slow), then mixes the narration onto the video with ffmpeg.
"""
import datetime, math, os, re, subprocess, sys, time
import imageio_ffmpeg
from playwright.sync_api import sync_playwright

BASE = "https://localhost:7134"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "demo")
VOICE, RATE = "Tara", "160"
W, H = 1440, 900
TODAY = datetime.date.today()
os.makedirs(OUT, exist_ok=True)
for f in os.listdir(OUT):
    os.remove(os.path.join(OUT, f))

segments = []  # (start seconds, file)
t0 = 0.0
page = None
mouse = [W / 2, H / 2]

CURSOR_JS = r"""
(() => {
  const install = () => {
    if (document.getElementById('__cur')) return;
    const c = document.createElement('div'); c.id = '__cur';
    c.style.cssText = 'position:fixed;left:0;top:0;width:26px;height:26px;z-index:2147483647;pointer-events:none;transform:translate(-4px,-3px);filter:drop-shadow(0 2px 3px rgba(0,0,0,.35))';
    c.innerHTML = '<svg width="26" height="26" viewBox="0 0 24 24"><path d="M4 2.5l6.5 18 2.4-7.6 7.6-2.4z" fill="#1d1d1f" stroke="#fff" stroke-width="1.6" stroke-linejoin="round"/></svg>';
    const cap = document.createElement('div'); cap.id = '__cap';
    cap.style.cssText = 'position:fixed;left:50%;bottom:28px;transform:translateX(-50%);max-width:72%;padding:10px 20px;border-radius:14px;background:rgba(20,20,24,.82);color:#fff;font:500 17px/1.45 -apple-system,Segoe UI,Roboto,sans-serif;text-align:center;z-index:2147483646;pointer-events:none;transition:opacity .3s;opacity:0';
    document.documentElement.append(cap, c);
    const pos = JSON.parse(sessionStorage.getItem('__pos') || '[720,450]');
    c.style.left = pos[0] + 'px'; c.style.top = pos[1] + 'px';
    window.__caption = t => { sessionStorage.setItem('__captxt', t || ''); cap.textContent = t || ''; cap.style.opacity = t ? 1 : 0; };
    window.__caption(sessionStorage.getItem('__captxt'));
    document.addEventListener('mousemove', e => {
      c.style.left = e.clientX + 'px'; c.style.top = e.clientY + 'px';
      sessionStorage.setItem('__pos', JSON.stringify([e.clientX, e.clientY]));
    }, true);
    document.addEventListener('mousedown', e => {
      const r = document.createElement('div');
      r.style.cssText = `position:fixed;left:${e.clientX - 18}px;top:${e.clientY - 18}px;width:36px;height:36px;border-radius:50%;border:3px solid rgba(0,113,227,.85);background:rgba(0,113,227,.18);z-index:2147483646;pointer-events:none;transition:transform .45s ease-out,opacity .45s ease-out`;
      document.documentElement.append(r);
      requestAnimationFrame(() => { r.style.transform = 'scale(1.8)'; r.style.opacity = '0'; });
      setTimeout(() => r.remove(), 500);
    }, true);
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', install); else install();
})();
"""


def now():
    return time.monotonic() - t0


def say(text):
    """Starts a narration line (recorded for the soundtrack) and shows it as a caption. Returns when it will end."""
    f = os.path.join(OUT, f"n{len(segments):03}.aiff")
    if FAST:
        print(f"[{now():6.1f}s] {text[:70]}", flush=True)
        return now()
    subprocess.run(["say", "-v", VOICE, "-r", RATE, "-o", f, text], check=True)
    info = subprocess.run(["afinfo", f], capture_output=True, text=True).stdout
    duration = float(re.search(r"estimated duration: ([\d.]+)", info).group(1))
    start = now()
    segments.append((start, f))
    try:
        page.evaluate("t => window.__caption && window.__caption(t)", text)
    except Exception:
        pass
    print(f"[{start:6.1f}s] {text}", flush=True)
    return start + duration


FAST = os.environ.get("FAST") == "1"  # check every step quickly, without waiting for the narration


def finish(end, pad=0.5):
    """Waits until the narration ending at `end` has finished."""
    if FAST:
        return
    left = end - now() + pad
    if left > 0:
        page.wait_for_timeout(left * 1000)


def pause(seconds):
    page.wait_for_timeout(seconds * 1000)


def glide(x, y):
    dx, dy = x - mouse[0], y - mouse[1]
    steps = max(12, int(math.hypot(dx, dy) / 18))
    page.mouse.move(x, y, steps=steps)
    mouse[0], mouse[1] = x, y


def point(locator):
    locator.scroll_into_view_if_needed()
    pause(0.25)
    box = locator.bounding_box()
    x, y = box["x"] + box["width"] / 2, box["y"] + box["height"] / 2
    glide(x, y)
    pause(0.2)
    return x, y


def click(locator, wait=0.6):
    x, y = point(locator)
    page.mouse.click(x, y)
    try:
        page.wait_for_load_state("networkidle", timeout=8000)
    except Exception:
        pass
    pause(wait)


def type_in(locator, text, delay=45):
    click(locator, wait=0.1)
    page.keyboard.press("Meta+A")
    page.keyboard.type(text, delay=delay)
    pause(0.15)


def fill(locator, value):
    point(locator)
    page.mouse.click(mouse[0], mouse[1])
    locator.fill(value)
    pause(0.3)


def choose(locator, **option):
    point(locator)
    if isinstance(option.get("label"), re.Pattern):
        texts = locator.locator("option").all_inner_texts()
        option = {"label": next(t for t in texts if option["label"].search(t))}
    locator.select_option(**option)
    pause(0.4)


def scroll(amount, steps=6):
    for _ in range(steps):
        page.mouse.wheel(0, amount / steps)
        pause(0.12)
    pause(0.3)


def nav(text):
    click(page.locator(".side-nav a", has_text=text).first)


def sign_out():
    click(page.locator(".side-foot button", has_text="Sign out"))
    page.wait_for_url(re.compile(r".*"), timeout=8000)
    pause(0.5)


def go_login():
    page.goto(BASE + "/Identity/Account/Login")
    page.wait_for_load_state("networkidle")
    pause(0.5)


def message():
    loc = page.locator(".alert-success")
    return loc.first.inner_text() if loc.count() else ""


def add_employee(first, last, gender, mobile, email, dept, desig, shift, salary):
    click(page.locator("a.btn", has_text="Add employee").first)
    type_in(page.locator("#FirstName"), first)
    type_in(page.locator("#LastName"), last)
    choose(page.locator("#Gender"), label=gender)
    type_in(page.locator("#Mobile"), mobile)
    type_in(page.locator("#Email"), email, delay=30)
    type_in(page.locator("#Department"), dept)
    type_in(page.locator("#Designation"), desig)
    choose(page.locator("#ShiftId"), label=re.compile(shift))
    type_in(page.locator("#MonthlySalary"), str(salary))
    click(page.locator("button[type=submit]", has_text="Add employee"), wait=0.8)


def give_login(full_name, role_label):
    click(page.locator("td a", has_text=full_name).first)
    point(page.locator("#login"))
    choose(page.locator("#login-role"), label=role_label)
    click(page.locator("button", has_text="Give login access"), wait=0.8)
    text = message()
    password = re.search(r"temporary password (\S+) \(", text).group(1)
    page.evaluate("window.scrollTo({top:0,behavior:'smooth'})")
    pause(0.8)
    return password


def sign_in(email, password):
    type_in(page.locator("#Input_Email"), email, delay=35)
    type_in(page.locator("#Input_Password"), password, delay=55)
    click(page.locator("#login-submit"), wait=1)


def make_files():
    """Biometric export for today (device IDs = employee codes) and a profile photo."""
    rows = ["Enroll ID,Date,Punch in,Punch out"]
    times = {"E004": ("08:58", "17:36"), "E005": ("09:07", "17:41"), "E006": ("08:51", "17:32"), "E007": ("09:31", "17:50"),
             "E008": ("06:56", "15:04"), "E009": ("07:02", "15:10"), "E010": ("06:49", "15:02")}
    for code, (a, b) in times.items():
        rows.append(f"{code},{TODAY.isoformat()},{a},{b}")
    csv = os.path.join(OUT, "biometric-export.csv")
    with open(csv, "w") as fh:
        fh.write("\n".join(rows) + "\n")
    return csv


def avatar(browser):
    p = browser.new_page(viewport={"width": 300, "height": 300})
    p.set_content("""<div id=a style="width:240px;height:240px;border-radius:50%;margin:30px;display:flex;align-items:center;justify-content:center;
        background:linear-gradient(135deg,#5e5ce6,#2997ff);color:#fff;font:700 96px -apple-system,sans-serif">PN</div>""")
    path = os.path.join(OUT, "priya.png")
    p.locator("#a").screenshot(path=path, omit_background=True)
    p.close()
    return path


def run():
    global page, t0
    csv = make_files()
    with sync_playwright() as pw:
        browser = pw.chromium.launch()
        photo = avatar(browser)
        context = browser.new_context(viewport={"width": W, "height": H}, ignore_https_errors=True,
                                      record_video_dir=OUT, record_video_size={"width": W, "height": H}, device_scale_factor=1)
        context.add_init_script(CURSOR_JS)
        page = context.new_page()
        t0 = time.monotonic()
        go_login()

        # ---------------- Intro ----------------
        e = say("Welcome to E M S, the employee management system by BitProSoftTech. "
                "In this demo, we follow Greenfield Institute through a working day: the admin sets up staff, "
                "HR runs attendance, accounts prepares salary slips, and an employee uses the self-service portal.")
        point(page.locator(".auth-demo"))
        finish(e)

        # ---------------- Admin ----------------
        e = say("We start as the admin, the owner of the institute. The live demo signs in with a single click.")
        click(page.locator(".auth-demo-login", has_text="Admin"), wait=1.5)
        finish(e)
        e = say("This is the admin dashboard: staff on the rolls, who is present today, attendance for the last two weeks, "
                "staff by shift, and this month's payroll.")
        scroll(450); pause(1); scroll(-450)
        finish(e)

        e = say("First, the admin adds a new HR executive. Open Employees, then Add employee, and fill in her details: "
                "name, mobile, email, department, designation, shift and monthly salary.")
        nav("Employees")
        add_employee("Sneha", "Kulkarni", "Female", "9876501234", "sneha.hr@greenfield.test", "Human Resources", "HR Executive", "General", 38000)
        finish(e)
        e = say("Sneha needs to sign in, so in Login access we choose H R. E M S creates a temporary password, shows it once, and emails it to her.")
        hr_password = give_login("Sneha Kulkarni", "HR")
        finish(e, 1.5)

        e = say("In the same way, the admin adds Amit Shah as an accounts executive, with Accounts access.")
        nav("Employees")
        add_employee("Amit", "Shah", "Male", "9876502345", "amit.accounts@greenfield.test", "Accounts", "Accounts Executive", "General", 36000)
        accounts_password = give_login("Amit Shah", "Accounts")
        finish(e, 1)

        e = say("And Ravi Menon, a lab assistant, as an employee. Employees only see their own attendance, leave, profile and letters.")
        nav("Employees")
        add_employee("Ravi", "Menon", "Male", "9876503456", "ravi@greenfield.test", "Computer Science", "Lab Assistant", "Early", 25000)
        give_login("Ravi Menon", "Employee")
        finish(e, 1)

        e = say("The admin's work is done. Let's sign out, and sign in as Sneha from HR.")
        sign_out()
        go_login()
        finish(e)

        # ---------------- HR ----------------
        e = say("Sneha signs in with her email and the temporary password.")
        sign_in("sneha.hr@greenfield.test", hr_password)
        finish(e)
        e = say("Her menu is built for HR: employees, attendance, leave and resignations, shifts and import. "
                "Salaries and the organization profile stay with accounts and the admin.")
        point(page.locator(".side-nav"))
        finish(e)

        e = say("Onboarding. A lab technician, Kiran Rao, joins today on the early shift. HR adds him in a few seconds.")
        nav("Employees")
        add_employee("Kiran", "Rao", "Male", "9876504567", "kiran@greenfield.test", "Science", "Lab Technician", "Early", 24000)
        finish(e)
        e = say("The employee list now shows everyone on the rolls, with their code, shift and joining date.")
        scroll(350); pause(0.8); scroll(-350)
        finish(e)

        e = say("Staffing. Under Shifts, HR sets the timings for each shift: General, Early, and a Night shift that ends after midnight, each with its break.")
        nav("Shifts")
        scroll(300); pause(0.6); scroll(-300)
        finish(e)

        e = say("Attendance. On the punch in and out page, HR types the times for today. "
                "E M S works out the hours, the late arrival and the status from each person's shift, while you type.")
        nav("Attendance")
        click(page.locator("a.btn", has_text="Punch in / out"))
        fill(page.get_by_label("Meera Iyer in time"), "09:02"); fill(page.get_by_label("Meera Iyer out time"), "17:40")
        fill(page.get_by_label("Rahul Verma in time"), "09:26"); fill(page.get_by_label("Rahul Verma out time"), "17:38")
        fill(page.get_by_label("Priya Nair in time"), "08:54"); fill(page.get_by_label("Priya Nair out time"), "17:45")
        finish(e)
        e = say("Rahul came in at nine twenty six, after the fifteen minute grace, so he is marked late. Save attendance.")
        point(page.get_by_label("Rahul Verma in time"))
        pause(1)
        click(page.locator("button[type=submit]", has_text=re.compile(r"^\s*Save\s*$")), wait=1)
        finish(e)

        e = say("Most staff punch in on the biometric machine. HR downloads the export from the device and uploads it here, as a C S V or Excel file.")
        nav("Import")
        form = page.locator("form[action*='/Org/Import/Attendance']")
        point(form.locator("input[type=file]"))
        form.locator("input[type=file]").set_input_files(csv)
        pause(0.8)
        click(form.locator("button[type=submit]"), wait=1.2)
        finish(e)
        e = say("Every row is checked before anything is saved. The punches are matched to employees by their biometric I D, and each day's status is worked out.")
        scroll(300); pause(1)
        finish(e)

        e = say("Review and correct. The month register shows every employee and every day, colour coded. "
                "A blue dot means punch times, orange means late. Any past day can be changed.")
        nav("Attendance")
        click(page.locator(".month-nav a", has_text="Sep"), wait=1)
        scroll(250); pause(0.6)
        finish(e)
        e = say("Here, Imran was on leave on the twenty ninth, not absent. Sneha changes the day, and saves.")
        choose(page.get_by_label("Imran Shaikh, 29 Sep"), value="4")
        click(page.locator("button", has_text="Save attendance"), wait=1)
        finish(e)

        e = say("Leave and resignations. Kavita has asked for a sick day, and Vikram has resigned. "
                "Sneha approves the leave, which marks the day as leave in attendance, and accepts the resignation with the last working day.")
        nav("Leave & resignations")
        row = page.locator("tr", has_text="Kavita Joshi")
        click(row.locator("button", has_text="Approve"), wait=1)
        row = page.locator("tr", has_text="Vikram Singh")
        type_in(row.locator("input[name=remarks]"), "Thank you for your service.")
        click(row.locator("button", has_text="Accept"), wait=1)
        scroll(300); pause(0.5)
        finish(e)

        e = say("Payroll belongs to accounts. Sneha signs out, and Amit from accounts signs in.")
        sign_out()
        go_login()
        sign_in("amit.accounts@greenfield.test", accounts_password)
        finish(e)

        # ---------------- Accounts ----------------
        e = say("Salary slips. The monthly salary is pro-rated by the paid days in the attendance register. Let's open September.")
        nav("Salary slips")
        click(page.locator(".month-nav a", has_text="Sep"), wait=1)
        finish(e)
        e = say("Gross salaries, loss of pay for unpaid days, and the net payable, for every employee.")
        scroll(350); pause(0.8)
        finish(e)
        e = say("Each employee has a printable salary slip, ready to save as P D F.")
        click(page.locator("tr", has_text="Priya Nair").locator("a", has_text="View slip"), wait=1)
        scroll(350); pause(0.8); scroll(-350)
        point(page.locator("button", has_text="Print"))
        finish(e)

        e = say("Now let's see the other side. Amit signs out, and Priya, a physics lecturer, signs in as an employee.")
        sign_out()
        go_login()
        click(page.locator(".auth-demo-login", has_text="Employee"), wait=1.5)
        finish(e)

        # ---------------- Employee ----------------
        e = say("My attendance shows Priya's days with her in and out times, and her shift: timings, break, the grace before she is late, and her weekly off.")
        point(page.locator(".shift-meta"))
        scroll(350); pause(0.8); scroll(-350)
        finish(e)
        e = say("Her salary slip for any month is one click away.")
        click(page.locator("a.btn", has_text="Salary slip"), wait=1)
        click(page.locator("a", has_text="My attendance").first, wait=0.6)
        finish(e)

        e = say("Leave. Priya sees her balance of casual, sick and earned leave, and applies for two days, with a reason.")
        nav("Leave")
        choose(page.locator("#Input_LeaveTypeId"), index=1)
        d1 = TODAY + datetime.timedelta(days=(7 - TODAY.weekday()) % 7 + 14)
        fill(page.locator("#Input_FromDate"), d1.isoformat())
        fill(page.locator("#Input_ToDate"), (d1 + datetime.timedelta(days=1)).isoformat())
        type_in(page.locator("#Input_Reason"), "Sister's graduation in Chennai", delay=35)
        click(page.locator("button", has_text="Apply"), wait=1)
        finish(e)
        e = say("It waits for HR's approval, and she can cancel it until then.")
        point(page.locator("tr", has_text="Sister").first)
        finish(e)

        e = say("My profile. Priya uploads a profile photo, and keeps her details up to date: contact, address and emergency contact.")
        nav("My profile")
        point(page.locator(".photo-form input[type=file]"))
        page.locator(".photo-form input[type=file]").set_input_files(photo)
        click(page.locator("button", has_text="Upload photo"), wait=1)
        finish(e)
        e = say("Her P A N and Aadhaar are shown masked, and she can correct them. Her salary account is here too, and her previous experience, which adds up her total experience.")
        scroll(700, 8); pause(1); scroll(600, 6); pause(1)
        finish(e)

        e = say("Letters. Her offer letter, and an appraisal letter for each salary revision, ready to print or save as P D F.")
        nav("Letters")
        click(page.locator("tr", has_text="Appraisal letter").locator("a", has_text="Open"), wait=1)
        scroll(300); pause(1); scroll(-300)
        finish(e)

        e = say("Resignation. Priya sees her thirty day notice period. If she resigns, HR confirms her last working day, "
                "she sees a countdown while on notice, and her relieving letter appears under Letters.")
        nav("Resignation")
        point(page.locator("#Input_RequestedLastDay"))
        finish(e)

        e = say("And under Account and password, staff change their sign in email and password. The shared demo logins are locked, so every visitor gets the same demo.")
        nav("Account & password")
        finish(e)

        e = say("Finally, Priya signs out.")
        sign_out()
        finish(e)
        e = say("That's E M S: one system for staff, shifts, attendance, leave and payroll. "
                "Try the live demo yourself, or start your free month today. Thank you for watching.")
        finish(e, 1.5)

        video = page.video.path()
        context.close()
        browser.close()
    return video


def mux(video):
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    inputs, filters = ["-i", video], []
    for i, (start, f) in enumerate(segments, start=1):
        inputs += ["-i", f]
        ms = int(start * 1000)
        filters.append(f"[{i}:a]aresample=48000,adelay={ms}|{ms}[a{i}]")
    mix = "".join(f"[a{i}]" for i in range(1, len(segments) + 1))
    filters.append(f"{mix}amix=inputs={len(segments)}:normalize=0,volume=1.0[aud]")
    out = os.path.join(HERE, "EMS-demo.mp4")
    cmd = [ffmpeg, "-y", *inputs, "-filter_complex", ";".join(filters), "-map", "0:v", "-map", "[aud]",
           "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-r", "25",
           "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", out]
    subprocess.run(cmd, check=True, capture_output=True)
    return out


if __name__ == "__main__":
    v = run()
    if FAST:
        print("fast check passed", flush=True)
        sys.exit(0)
    print("video:", v, flush=True)
    print("output:", mux(v), flush=True)
