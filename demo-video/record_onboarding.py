"""Narrated walkthrough of employee onboarding in EMS (reuses the pointer, captions and voice from record_demo.py)."""
import os, re, shutil, sys, time
import record_demo as R

say, finish, pause, click, type_in, fill, choose, point, scroll, nav, sign_out, go_login, message, sign_in = (
    R.say, R.finish, R.pause, R.click, R.type_in, R.fill, R.choose, R.point, R.scroll, R.nav, R.sign_out, R.go_login, R.message, R.sign_in)


def pdf(path, *lines):
    """A small one-page PDF to upload as a sample document."""
    text = "BT /F1 20 Tf 72 760 Td " + " ".join(f"({l}) Tj 0 -28 Td" for l in lines) + " ET"
    objs = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", f"<< /Length {len(text)} >>\nstream\n{text}\nendstream"]
    out, offsets = "%PDF-1.4\n", []
    for i, o in enumerate(objs):
        offsets.append(len(out)); out += f"{i + 1} 0 obj\n{o}\nendobj\n"
    x = len(out)
    out += f"xref\n0 {len(objs) + 1}\n0000000000 65535 f \n" + "".join(f"{o:010} 00000 n \n" for o in offsets)
    out += f"trailer\n<< /Size {len(objs) + 1} /Root 1 0 R >>\nstartxref\n{x}\n%%EOF\n"
    with open(path, "w") as fh:
        fh.write(out)
    return path


def run():
    from playwright.sync_api import sync_playwright
    pan = pdf(os.path.join(R.OUT, "kiran-pan-card.pdf"), "PAN card", "Kiran Rao", "Sample for the EMS demo")
    aadhaar = pdf(os.path.join(R.OUT, "kiran-aadhaar.pdf"), "Aadhaar card", "Kiran Rao", "Sample for the EMS demo")
    degree = pdf(os.path.join(R.OUT, "kiran-degree.pdf"), "B.Sc. certificate", "Kiran Rao", "Sample for the EMS demo")

    with sync_playwright() as pw:
        browser = pw.chromium.launch()
        context = browser.new_context(viewport={"width": R.W, "height": R.H}, ignore_https_errors=True,
                                      record_video_dir=R.OUT, record_video_size={"width": R.W, "height": R.H})
        context.add_init_script(R.CURSOR_JS)
        R.page = page = context.new_page()
        R.t0 = time.monotonic()
        go_login()

        e = say("Welcome to E M S. In this demo, we follow a new employee's onboarding at Greenfield Institute: "
                "from HR adding him, to his joining documents, verification, and confirmation after probation.")
        point(page.locator(".auth-demo"))
        finish(e)

        # ---------------- HR adds the employee ----------------
        e = say("Meera from HR signs in. On the Employees page, every employee now shows their onboarding progress, "
                "and Onboarding pending lists only those with something left.")
        click(page.locator(".auth-demo-login", has_text="HR"), wait=1.2)
        nav("Employees")
        point(page.locator("th", has_text="Onboarding"))
        pause(0.8)
        click(page.locator("a.btn", has_text="Onboarding pending"), wait=1)
        finish(e)

        e = say("Kiran Rao joins today as a lab technician. Meera clicks Add employee, and fills in his personal details: "
                "name, date of birth, mobile, email, qualification, address and an emergency contact.")
        click(page.locator("a.btn", has_text="Add employee").first)
        type_in(page.locator("#FirstName"), "Kiran")
        type_in(page.locator("#LastName"), "Rao")
        choose(page.locator("#Gender"), label="Male")
        fill(page.locator("#DateOfBirth"), "1997-08-21")
        type_in(page.locator("#Mobile"), "9811122233")
        type_in(page.locator("#Email"), "kiran.rao@greenfield.test", delay=28)
        type_in(page.locator("#HighestQualification"), "B.Sc. Chemistry")
        type_in(page.locator("#EmergencyContactName"), "Lakshmi Rao (mother)")
        type_in(page.locator("#CurrentAddress"), "7, Sai Nagar, Hadapsar, Pune 411028", delay=28)
        type_in(page.locator("#EmergencyContactPhone"), "9822033344")
        finish(e)

        e = say("Then the job details. His biometric I D matches the attendance machine, so his punches are imported automatically. "
                "He reports to Kavita, is full time on the early shift, and starts on a six month probation.")
        type_in(page.locator("#AttendanceId"), "BIO-213")
        type_in(page.locator("#Department"), "Science")
        type_in(page.locator("#Designation"), "Lab Technician")
        choose(page.locator("#ReportingManagerId"), label=re.compile("Kavita"))
        choose(page.locator("#EmploymentType"), label="Full time")
        choose(page.locator("#ShiftId"), label=re.compile("Early"))
        type_in(page.locator("#MonthlySalary"), "24000")
        point(page.locator("#ProbationEndsOn"))
        finish(e)

        e = say("His P A N, Aadhaar and salary account can be entered now, or Kiran can add them himself. "
                "Meera also gives him a login in the same step.")
        type_in(page.locator("#Pan"), "KRNPR1234K")
        type_in(page.locator("#BankName"), "Bank of Maharashtra")
        type_in(page.locator("#BankAccountNumber"), "60123456789")
        type_in(page.locator("#BankIfsc"), "MAHB0000123")
        click(page.locator("#CreateLogin"), wait=0.3)
        choose(page.locator("#LoginRole"), label="Employee")
        click(page.locator("button[type=submit]", has_text="Add employee"), wait=1.2)
        password = re.search(r"temporary password (\S+) \(", message()).group(1)
        finish(e)

        e = say("Kiran is added, and his login is ready. His page opens with the probation banner, his status, and his onboarding progress.")
        point(page.locator(".alert-success"))
        pause(1)
        point(page.locator(".badge", has_text="Onboarding"))
        finish(e)

        e = say("Further down is his joining checklist: what Kiran fills in, what HR sets up, and the documents to collect. "
                "Meera has his P A N card, so she uploads it here. Documents HR uploads are verified straight away.")
        point(page.locator("#documents"))
        scroll(200)
        choose(page.locator("#doc-type"), label="PAN card")
        point(page.locator("#doc-file"))
        page.locator("#doc-file").set_input_files(pan)
        click(page.locator("button", has_text="Upload document"), wait=1.2)
        point(page.locator("#documents .card-head"))
        finish(e, 1)

        e = say("Now let's see Kiran's side. Meera signs out, and Kiran signs in with his temporary password.")
        sign_out()
        go_login()
        sign_in("kiran.rao@greenfield.test", password)
        finish(e)

        # ---------------- Employee completes the checklist ----------------
        e = say("A banner tells Kiran what is still needed. He opens his joining checklist.")
        point(page.locator(".notice", has_text="Joining checklist"))
        pause(1)
        click(page.locator(".notice a", has_text="Complete it"), wait=1)
        finish(e)
        e = say("Ticks show what's done. For each missing document, he can upload a P D F or a clear photo. He uploads his Aadhaar card, and his degree certificate.")
        scroll(250); pause(0.6)
        choose(page.locator("#doc-type"), label="Aadhaar card")
        page.locator("#doc-file").set_input_files(aadhaar)
        click(page.locator("button[type=submit]", has_text=re.compile(r"^\s*Upload\s*$")), wait=1)
        choose(page.locator("#doc-type"), label="Highest qualification certificate")
        page.locator("#doc-file").set_input_files(degree)
        click(page.locator("button[type=submit]", has_text=re.compile(r"^\s*Upload\s*$")), wait=1)
        finish(e)
        e = say("Both now wait for HR to verify them. He can also fill in his Aadhaar number and emergency details under My profile.")
        point(page.locator("tr", has_text="Aadhaar card").first)
        scroll(400); pause(0.8)
        finish(e)

        # ---------------- HR verifies ----------------
        e = say("Back to HR. Meera signs in, and opens Leave and resignations, where joining documents wait for verification.")
        sign_out()
        go_login()
        click(page.locator(".auth-demo-login", has_text="HR"), wait=1.2)
        nav("Leave & resignations")
        finish(e)
        e = say("She opens each document, and checks it against the original. Kiran's Aadhaar card is fine, so she verifies it.")
        row = page.locator("tr", has_text="Kiran Rao").filter(has_text="Aadhaar card")
        point(row.locator("a").first)
        pause(0.8)
        click(row.locator("button", has_text="Verify"), wait=1)
        finish(e)
        e = say("The degree certificate is unclear, so she rejects it, with a note telling Kiran what to upload instead.")
        row = page.locator("tr", has_text="Kiran Rao").filter(has_text="qualification")
        type_in(row.locator("input[name=note]"), "Please upload the final degree certificate, not the marksheet.", delay=22)
        click(row.locator("button", has_text="Reject"), wait=1)
        finish(e)

        # ---------------- Probation ----------------
        e = say("Finally, probation. Neha joined last month and is on probation. When it's over, HR confirms her employment with one click.")
        nav("Employees")
        click(page.locator("td a", has_text="Neha Gupta"), wait=1)
        point(page.locator(".notice", has_text="On probation"))
        pause(0.8)
        click(page.locator("button", has_text="Confirm employment"), wait=1)
        finish(e)
        e = say("Neha is now active, and her confirmation letter is ready, to print or save as P D F.")
        click(page.locator("a.btn", has_text="Letters"), wait=1)
        click(page.locator("tr", has_text="Confirmation letter").locator("a", has_text="Open"), wait=1)
        scroll(300); pause(0.8); scroll(-300)
        finish(e)

        e = say("That's onboarding in E M S: complete details from day one, a clear checklist for HR and the employee, "
                "verified documents, and probation that is never forgotten. Thank you for watching.")
        finish(e, 1.5)

        video = page.video.path()
        context.close()
        browser.close()
    return video


if __name__ == "__main__":
    v = run()
    if R.FAST:
        print("fast check passed", flush=True)
        sys.exit(0)
    out = R.mux(v)
    final = os.path.join(R.HERE, "EMS-onboarding-demo.mp4")
    shutil.move(out, final)
    print("output:", final, flush=True)
