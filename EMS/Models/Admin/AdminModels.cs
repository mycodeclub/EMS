using System.ComponentModel.DataAnnotations;

namespace EMS.Models.Admin;

/// <summary>A list screen of the admin console: summary tiles above a table.</summary>
public record AdminList<T>(IReadOnlyList<StatTile> Stats, IReadOnlyList<T> Rows);

public record StatTile(string Label, string Value, string? Note = null);

public record EnquiryRow(
    DateTime ReceivedAt, string Name, string Organization, string Email, string Phone,
    Industry Industry, TeamSize TeamSize, EnquiryInterest Interest, EnquiryStatus Status);

public record LeadRow(
    string Organization, string ContactName, string Email, Industry Industry, int Employees, int Offices,
    LeadStage Stage, decimal MonthlyValue, string Owner, DateOnly NextFollowUp);

public record CustomerRow(
    string Organization, string City, Industry Industry, string Plan, int Offices, int Employees,
    DateOnly CustomerSince, DateOnly RenewsOn, decimal MonthlyValue, CustomerHealth Health);

public record InvoiceRow(
    string Number, string Customer, DateOnly IssuedOn, DateOnly DueOn, decimal Amount, InvoiceStatus Status);

public enum LeadStage
{
    New = 1,
    Qualified,
    [Display(Name = "Demo scheduled")] DemoScheduled,
    [Display(Name = "Proposal sent")] ProposalSent,
    Negotiation,
}

public enum CustomerHealth { Healthy = 1, Onboarding, [Display(Name = "At risk")] AtRisk }

public enum InvoiceStatus { Paid = 1, Due, Overdue }
