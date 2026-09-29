using EMS.Models;
using EMS.Models.Admin;
using EMS.Models.Common;
using EMS.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.Controllers;

/// <summary>Super admin console. Screens show <see cref="SampleAdminData"/> until they are connected to the database.</summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminController : Controller
{
    public IActionResult Index() => RedirectToAction(nameof(Enquiries));

    public IActionResult Enquiries()
    {
        var rows = SampleAdminData.Enquiries();
        return View(new AdminList<EnquiryRow>(
        [
            new("New", rows.Count(r => r.Status == EnquiryStatus.New).ToString(), "Awaiting first call"),
            new("Contacted", rows.Count(r => r.Status == EnquiryStatus.Contacted).ToString()),
            new("Converted", rows.Count(r => r.Status == EnquiryStatus.Converted).ToString(), "Became a lead or customer"),
            new("Last 7 days", rows.Count(r => r.ReceivedAt >= DateTime.Now.AddDays(-7)).ToString(), $"{rows.Count} in total"),
        ], rows));
    }

    public IActionResult Leads()
    {
        var rows = SampleAdminData.Leads();
        var weekEnd = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
        return View(new AdminList<LeadRow>(
        [
            new("Open leads", rows.Count.ToString()),
            new("Demos scheduled", rows.Count(r => r.Stage == LeadStage.DemoScheduled).ToString()),
            new("Pipeline value", rows.Sum(r => r.MonthlyValue).ToRupees(), "per month"),
            new("Follow-ups this week", rows.Count(r => r.NextFollowUp <= weekEnd).ToString()),
        ], rows));
    }

    public IActionResult Customers()
    {
        var rows = SampleAdminData.Customers();
        return View(new AdminList<CustomerRow>(
        [
            new("Active customers", rows.Count.ToString(), $"{rows.Count(r => r.Health == CustomerHealth.Onboarding)} onboarding"),
            new("Employees managed", rows.Sum(r => r.Employees).ToString("N0")),
            new("Offices", rows.Sum(r => r.Offices).ToString()),
            new("Monthly revenue", rows.Sum(r => r.MonthlyValue).ToRupees(), $"{rows.Count(r => r.Health == CustomerHealth.AtRisk)} at risk"),
        ], rows));
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
}
