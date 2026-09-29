using System.Globalization;
using EMS.Models;
using EMS.Models.Admin;
using EMS.Models.Common;
using EMS.Services.Admin;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Controllers;

/// <summary>
/// Super admin console. Enquiries, leads, customers and billing show <see cref="SampleAdminData"/> until they are
/// connected to the database; free trials (and the onboarding progress on the dashboard) are real.
/// </summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminController(TrialService trials) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var revenue = SampleAdminData.RevenueByMonth();
        var enquiries = SampleAdminData.EnquiriesByWeek();
        var leads = SampleAdminData.Leads();
        var customers = SampleAdminData.Customers();
        var live = await trials.TrialsAsync(ct);
        var mrr = revenue[^1].Amount;
        var growth = (mrr - revenue[^2].Amount) / revenue[^2].Amount;

        int Reached(OnboardingStep step) => live.Count(t => t.Step > step);
        return View(new AdminDashboard(
        [
            new("Monthly recurring revenue", mrr.ToRupees(), $"{growth:+0.0%;-0.0%} vs last month"),
            new("Active customers", customers.Count.ToString(), $"{customers.Sum(c => c.Employees):N0} employees managed"),
            new("Open leads", leads.Count.ToString(), $"{leads.Sum(l => l.MonthlyValue).ToRupees()} / month in pipeline"),
            new("Free trials", live.Count.ToString(), $"{live.Count(t => t.Step == OnboardingStep.Done)} finished setup"),
        ],
        new ColumnChart("Monthly recurring revenue", "Last 12 months",
            revenue.Select(r => new ChartPoint(r.Month.ToString("MMM", CultureInfo.InvariantCulture), r.Amount, $"{r.Month:MMM yyyy}: {r.Amount.ToRupees()}")).ToList(),
            Charts.CompactRupees),
        new ColumnChart("Website enquiries", "Per week, last 8 weeks",
            enquiries.Select(w => new ChartPoint(w.WeekStart.ToString("dd MMM", CultureInfo.InvariantCulture), w.Count, $"Week of {w.WeekStart.ToShortDate()}: {w.Count} enquiries")).ToList(),
            v => v.ToString("0", CultureInfo.InvariantCulture)),
        new BarChart("Lead pipeline", "Open leads by stage",
            Enum.GetValues<LeadStage>().Select(s => (Stage: s, Count: leads.Count(l => l.Stage == s)))
                .Select(x => new ChartPoint(x.Stage.DisplayName(), x.Count, $"{x.Count} lead{(x.Count == 1 ? "" : "s")}")).ToList()),
        new BarChart("Customers by industry", "Active customers",
            customers.GroupBy(c => c.Industry).OrderByDescending(g => g.Count())
                .Select(g => new ChartPoint(g.Key.DisplayName(), g.Count(), $"{g.Count()} customer{(g.Count() == 1 ? "" : "s")}")).ToList()),
        new BarChart("Trial onboarding", "How far each free trial has got",
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
        var rows = SampleAdminData.Enquiries();
        return View(new OfferableList<EnquiryRow>(
        [
            new("New", rows.Count(r => r.Status == EnquiryStatus.New).ToString(), "Awaiting first call"),
            new("Contacted", rows.Count(r => r.Status == EnquiryStatus.Contacted).ToString()),
            new("Converted", rows.Count(r => r.Status == EnquiryStatus.Converted).ToString(), "Became a lead or customer"),
            new("Last 7 days", rows.Count(r => r.ReceivedAt >= DateTime.Now.AddDays(-7)).ToString(), $"{rows.Count} in total"),
        ], rows, await OfferedAsync(ct)));
    }

    public async Task<IActionResult> Leads(CancellationToken ct)
    {
        var rows = SampleAdminData.Leads();
        var weekEnd = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
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
        var rows = SampleAdminData.Customers();
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
        var rows = SampleAdminData.Invoices();
        decimal Total(InvoiceStatus status) => rows.Where(r => r.Status == status).Sum(r => r.Amount);
        return View(new AdminList<InvoiceRow>(
        [
            new("Invoiced", rows.Sum(r => r.Amount).ToRupees(), $"{rows.Count} invoices"),
            new("Collected", Total(InvoiceStatus.Paid).ToRupees()),
            new("Due", Total(InvoiceStatus.Due).ToRupees(), "Not yet past due date"),
            new("Overdue", Total(InvoiceStatus.Overdue).ToRupees(), $"{rows.Count(r => r.Status == InvoiceStatus.Overdue)} invoices"),
        ], rows));
    }

    /// <summary>Free-trial form, prefilled from the lead or enquiry with this email when there is one.</summary>
    public IActionResult OfferTrial(string? email)
    {
        var input = new TrialInput();
        if (SampleAdminData.Leads().FirstOrDefault(l => l.Email == email) is { } lead)
        {
            input = new TrialInput { OrganizationName = lead.Organization, OwnerName = lead.ContactName, OwnerEmail = lead.Email, Industry = lead.Industry };
        }
        if (SampleAdminData.Enquiries().FirstOrDefault(e => e.Email == email) is { } enquiry)
        {
            input.OrganizationName = enquiry.Organization;
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

    private async Task<IReadOnlySet<string>> OfferedAsync(CancellationToken ct) =>
        (await trials.OwnerEmailsAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
