using System.Globalization;
using EMS.Data;
using EMS.Models;
using EMS.Models.Admin;
using EMS.Models.Common;
using EMS.Services.Auth;
using EMS.Services.Common;
using EMS.Services.Demo;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Controllers;

/// <summary>
/// Super admin console. Enquiries and free trials come from the database. Leads, paying customers and billing are
/// not stored yet, so those screens are empty.
/// </summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminController(
    TrialService trials,
    ICrudService<Enquiry> enquiries,
    ApplicationDbContext db,
    SignInManager<IdentityUser> signIn,
    DemoSeeder demo,
    ILogger<AdminController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // The public demo is not a real trial.
        var live = (await trials.TrialsAsync(ct)).Where(t => !t.IsDemo).ToList();
        var weeks = await EnquiriesByWeekAsync(8, ct);
        var newEnquiries = await enquiries.Query().CountAsync(e => e.Status == EnquiryStatus.New, ct);
        var totalEnquiries = await enquiries.Query().CountAsync(ct);

        int Reached(OnboardingStep step) => live.Count(t => t.Step > step);
        return View(new AdminDashboard(
        [
            new("New enquiries", newEnquiries.ToString(), $"{totalEnquiries} in total"),
            new("Free trials", live.Count.ToString(), $"{live.Count(t => t.Step == OnboardingStep.Done)} finished setup"),
            new("Still setting up", live.Count(t => t.Step != OnboardingStep.Done).ToString()),
            new("Employees managed", live.Sum(t => t.Employees).ToString("N0"), "Across all trials"),
        ],
        new ColumnChart("Website enquiries", "Per week, last 8 weeks",
            weeks.Select(w => new ChartPoint(w.WeekStart.ToString("dd MMM", CultureInfo.InvariantCulture), w.Count, $"Week of {w.WeekStart.ToShortDate()}: {w.Count} enquiries")).ToList(),
            v => v.ToString("0", CultureInfo.InvariantCulture)),
        new BarChart("Trials by industry", "Organizations on a free trial",
            live.Where(t => t.Industry is not null).GroupBy(t => t.Industry!.Value).OrderByDescending(g => g.Count())
                .Select(g => new ChartPoint(g.Key.DisplayName(), g.Count(), $"{g.Count()} trial{(g.Count() == 1 ? "" : "s")}")).ToList()),
        new BarChart("Trial onboarding", "How far each free trial has got",
            live.Count == 0 ? [] :
            [
                new("Trial offered", live.Count, $"{live.Count} offered"),
                new("Signed in, password changed", Reached(OnboardingStep.Password), $"{Reached(OnboardingStep.Password)} of {live.Count}"),
                new("Profile completed", Reached(OnboardingStep.Profile), $"{Reached(OnboardingStep.Profile)} of {live.Count}"),
                new("Shifts set up", Reached(OnboardingStep.Shifts), $"{Reached(OnboardingStep.Shifts)} of {live.Count}"),
                new("Setup finished", Reached(OnboardingStep.Employees), $"{Reached(OnboardingStep.Employees)} of {live.Count}"),
            ]),
        live.Take(5).ToList()));
    }

    public async Task<IActionResult> Enquiries(CancellationToken ct)
    {
        var rows = (await enquiries.Query().OrderByDescending(e => e.CreatedAt).ToListAsync(ct)).Select(ToRow).ToList();
        return View(new OfferableList<EnquiryRow>(
        [
            new("New", rows.Count(r => r.Status == EnquiryStatus.New).ToString(), "Awaiting first call"),
            new("Contacted", rows.Count(r => r.Status == EnquiryStatus.Contacted).ToString()),
            new("Converted", rows.Count(r => r.Status == EnquiryStatus.Converted).ToString(), "Became a lead or customer"),
            new("Last 7 days", rows.Count(r => r.ReceivedAt >= AppClock.Now.AddDays(-7)).ToString(), $"{rows.Count} in total"),
        ], rows, await OfferedAsync(ct)));
    }

    public async Task<IActionResult> Leads(CancellationToken ct)
    {
        // Leads are not stored yet.
        IReadOnlyList<LeadRow> rows = [];
        var weekEnd = DateOnly.FromDateTime(AppClock.Today.AddDays(7));
        return View(new OfferableList<LeadRow>(
        [
            new("Open leads", rows.Count.ToString()),
            new("Demos scheduled", rows.Count(r => r.Stage == LeadStage.DemoScheduled).ToString()),
            new("Pipeline value", rows.Sum(r => r.MonthlyValue).ToRupees(), "per month"),
            new("Follow-ups this week", rows.Count(r => r.NextFollowUp <= weekEnd).ToString()),
        ], rows, await OfferedAsync(ct)));
    }

    public async Task<IActionResult> Customers(CancellationToken ct)
    {
        // Paying customers are not stored yet.
        IReadOnlyList<CustomerRow> rows = [];
        return View(new CustomersPage(new AdminList<CustomerRow>(
        [
            new("Active customers", rows.Count.ToString(), $"{rows.Count(r => r.Health == CustomerHealth.Onboarding)} onboarding"),
            new("Employees managed", rows.Sum(r => r.Employees).ToString("N0")),
            new("Offices", rows.Sum(r => r.Offices).ToString()),
            new("Monthly revenue", rows.Sum(r => r.MonthlyValue).ToRupees(), $"{rows.Count(r => r.Health == CustomerHealth.AtRisk)} at risk"),
        ], rows), await trials.TrialsAsync(ct)));
    }

    public IActionResult Billing()
    {
        // Invoices are not stored yet.
        IReadOnlyList<InvoiceRow> rows = [];
        decimal Total(InvoiceStatus status) => rows.Where(r => r.Status == status).Sum(r => r.Amount);
        return View(new AdminList<InvoiceRow>(
        [
            new("Invoiced", rows.Sum(r => r.Amount).ToRupees(), $"{rows.Count} invoices"),
            new("Collected", Total(InvoiceStatus.Paid).ToRupees()),
            new("Due", Total(InvoiceStatus.Due).ToRupees(), "Not yet past due date"),
            new("Overdue", Total(InvoiceStatus.Overdue).ToRupees(), $"{rows.Count(r => r.Status == InvoiceStatus.Overdue)} invoices"),
        ], rows));
    }

    /// <summary>
    /// Opens the customer's organization panel as its owner (in a new tab). Uses a separate cookie for /Org only, so this
    /// super admin session keeps working.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SignInAs(int id, CancellationToken ct)
    {
        var organization = await db.Organizations.Include(o => o.Owner).FirstOrDefaultAsync(o => o.UniqueId == id, ct);
        if (organization?.Owner is not { } owner) return NotFound();
        if (organization.OnboardingCompletedAt is null)
        {
            TempData["Error"] = $"{organization.Name} has not finished setup yet, so there is no panel to open.";
            return RedirectToAction(nameof(Customers));
        }

        await Impersonation.SignInAsync(HttpContext, signIn, owner, User);
        logger.LogInformation("{Admin} signed in as {Owner} ({Organization}).", User.Identity?.Name, owner.Email, organization.Name);
        return RedirectToAction("Index", "Dashboard", new { area = "Org" });
    }

    /// <summary>Rebuilds the demo organization now instead of waiting for midnight.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetDemo(CancellationToken ct)
    {
        if (!demo.Enabled) return NotFound();
        await demo.ResetAsync(ct);
        TempData["Message"] = "Demo organization reset. Visitors' changes were discarded and the attendance runs up to yesterday.";
        return RedirectToAction(nameof(Customers));
    }

    /// <summary>Free-trial form, prefilled from the lead or enquiry with this email when there is one.</summary>
    public async Task<IActionResult> OfferTrial(string? email, CancellationToken ct)
    {
        var input = new TrialInput();
        if (!string.IsNullOrWhiteSpace(email)
            && await enquiries.Query().Where(e => e.Email == email).OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync(ct) is { } enquiry)
        {
            input.OrganizationName = enquiry.OrganizationName;
            input.OwnerName = enquiry.Name;
            input.OwnerEmail = enquiry.Email;
            input.OwnerPhone = enquiry.Phone;
            input.Industry = enquiry.Industry;
        }
        return View(input);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OfferTrial(TrialInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(input);

        var loginUrl = Url.Page("/Account/Login", null, new { area = "Identity" }, Request.Scheme)!;
        var result = await trials.OfferAsync(input, loginUrl, ct);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            return View(input);
        }

        // Shown once, not redirected: the temporary password is not stored anywhere readable.
        return View("TrialOffered", result.Data);
    }

    /// <summary>Enquiries received in each of the last <paramref name="weeks"/> weeks (Monday to Sunday), ending with this week.</summary>
    private async Task<IReadOnlyList<(DateOnly WeekStart, int Count)>> EnquiriesByWeekAsync(int weeks, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(AppClock.Today);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var first = monday.AddDays(-7 * (weeks - 1));
        var since = first.ToDateTime(TimeOnly.MinValue).AppTimeToUtc();
        var received = (await enquiries.Query().Where(e => e.CreatedAt >= since).Select(e => e.CreatedAt).ToListAsync(ct))
            .Select(at => DateOnly.FromDateTime(at.ToAppTime())).ToList();
        return Enumerable.Range(0, weeks)
            .Select(i => first.AddDays(7 * i))
            .Select(start => (start, received.Count(d => d >= start && d < start.AddDays(7))))
            .ToList();
    }

    private static EnquiryRow ToRow(Enquiry e) => new(
        e.CreatedAt.ToAppTime(), e.Name, e.OrganizationName, e.Email, e.Phone, e.Industry, e.TeamSize, e.Interest, e.Status);

    private async Task<IReadOnlySet<string>> OfferedAsync(CancellationToken ct) =>
        (await trials.OwnerEmailsAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
