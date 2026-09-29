using EMS.Models;
using EMS.Models.Admin;

namespace EMS.Services.Admin;

/// <summary>
/// Placeholder rows for the admin console until enquiries, leads, customers and billing are read from the database.
/// Dates are relative to today so the screens always look current.
/// </summary>
public static class SampleAdminData
{
    private static DateTime Now => DateTime.Now;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static IReadOnlyList<EnquiryRow> Enquiries() =>
    [
        new(Now.AddHours(-2), "Dr. Meera Kulkarni", "Sunrise Multispeciality Hospital", "meera.k@example.com", "+91 98200 11234", Industry.Hospital, TeamSize.From101To500, EnquiryInterest.Demo, EnquiryStatus.New),
        new(Now.AddHours(-9), "Rahul Verma", "The Grand Orchid Hotel", "rahul.verma@example.com", "+91 99100 45521", Industry.Hotel, TeamSize.From21To100, EnquiryInterest.FreeTrial, EnquiryStatus.New),
        new(Now.AddDays(-1), "Anita Desai", "Zilla Parishad Office, Nashik", "anita.desai@example.com", "+91 94220 78810", Industry.PublicOffice, TeamSize.From21To100, EnquiryInterest.Pricing, EnquiryStatus.Contacted),
        new(Now.AddDays(-2), "Karthik Iyer", "Brightpath Engineering", "karthik@example.com", "+91 98450 33012", Industry.Manufacturing, TeamSize.Above500, EnquiryInterest.Demo, EnquiryStatus.Contacted),
        new(Now.AddDays(-3), "Sneha Reddy", "Little Steps Clinic", "sneha.reddy@example.com", "+91 90000 12876", Industry.Hospital, TeamSize.UpTo20, EnquiryInterest.FreeTrial, EnquiryStatus.Converted),
        new(Now.AddDays(-5), "Imran Shaikh", "Horizon Business Park", "imran.s@example.com", "+91 98670 55420", Industry.Corporate, TeamSize.From101To500, EnquiryInterest.Pricing, EnquiryStatus.Contacted),
        new(Now.AddDays(-8), "Priya Nair", "St. Joseph's College", "priya.nair@example.com", "+91 94470 90213", Industry.Education, TeamSize.From21To100, EnquiryInterest.Other, EnquiryStatus.Closed),
        new(Now.AddDays(-12), "Vikram Singh", "Royal Heritage Resort", "vikram@example.com", "+91 94140 67731", Industry.Hotel, TeamSize.From101To500, EnquiryInterest.Demo, EnquiryStatus.Converted),
    ];

    public static IReadOnlyList<LeadRow> Leads() =>
    [
        new("Sunrise Multispeciality Hospital", "Dr. Meera Kulkarni", "meera.k@example.com", Industry.Hospital, 320, 3, LeadStage.New, 38_000, "Ankit", Today.AddDays(1)),
        new("Brightpath Engineering", "Karthik Iyer", "karthik@example.com", Industry.Manufacturing, 740, 2, LeadStage.DemoScheduled, 72_000, "Ankit", Today.AddDays(2)),
        new("Zilla Parishad Office, Nashik", "Anita Desai", "anita.desai@example.com", Industry.PublicOffice, 85, 1, LeadStage.Qualified, 9_500, "Neha", Today.AddDays(3)),
        new("Horizon Business Park", "Imran Shaikh", "imran.s@example.com", Industry.Corporate, 210, 4, LeadStage.ProposalSent, 26_000, "Neha", Today.AddDays(4)),
        new("CityCare Diagnostics", "Farah Khan", "farah@example.com", Industry.Hospital, 140, 6, LeadStage.Negotiation, 21_000, "Ankit", Today),
        new("Seaside Suites", "Joseph D'Souza", "joseph@example.com", Industry.Hotel, 60, 1, LeadStage.Qualified, 6_000, "Rohit", Today.AddDays(6)),
        new("Apex Auto Components", "Suresh Patil", "suresh.p@example.com", Industry.Manufacturing, 450, 2, LeadStage.ProposalSent, 48_000, "Rohit", Today.AddDays(-1)),
    ];

    public static IReadOnlyList<CustomerRow> Customers() =>
    [
        new("Royal Heritage Resort", "Udaipur", Industry.Hotel, "Business", 2, 184, Today.AddMonths(-14), Today.AddMonths(10), 22_000, CustomerHealth.Healthy),
        new("Lifeline Hospitals", "Pune", Industry.Hospital, "Business", 5, 612, Today.AddMonths(-22), Today.AddMonths(2), 68_000, CustomerHealth.Healthy),
        new("Little Steps Clinic", "Hyderabad", Industry.Hospital, "Starter", 1, 18, Today.AddDays(-9), Today.AddDays(21), 0, CustomerHealth.Onboarding),
        new("Metro Public Works Dept.", "Nagpur", Industry.PublicOffice, "Business", 3, 240, Today.AddMonths(-8), Today.AddMonths(4), 28_000, CustomerHealth.AtRisk),
        new("Greenfield International School", "Bengaluru", Industry.Education, "Business", 1, 96, Today.AddMonths(-5), Today.AddMonths(7), 11_000, CustomerHealth.Healthy),
        new("Precision Castings", "Rajkot", Industry.Manufacturing, "Business", 2, 380, Today.AddMonths(-11), Today.AddDays(18), 41_000, CustomerHealth.AtRisk),
        new("Bluewater Hotels", "Kochi", Industry.Hotel, "Business", 4, 265, Today.AddMonths(-3), Today.AddMonths(9), 31_000, CustomerHealth.Healthy),
    ];

    public static IReadOnlyList<InvoiceRow> Invoices()
    {
        // Invoices are raised on the 1st and due at month end, so this month's are never overdue and last month's always are.
        var month = new DateOnly(Today.Year, Today.Month, 1);
        var last = month.AddMonths(-1);
        var monthEnd = month.AddMonths(1).AddDays(-1);
        var lastEnd = month.AddDays(-1);
        return
        [
            new($"EMS-{month:yyMM}-001", "Lifeline Hospitals", month, monthEnd, 68_000, InvoiceStatus.Paid),
            new($"EMS-{month:yyMM}-002", "Bluewater Hotels", month, monthEnd, 31_000, InvoiceStatus.Paid),
            new($"EMS-{month:yyMM}-003", "Royal Heritage Resort", month, monthEnd, 22_000, InvoiceStatus.Due),
            new($"EMS-{month:yyMM}-004", "Precision Castings", month, monthEnd, 41_000, InvoiceStatus.Due),
            new($"EMS-{month:yyMM}-005", "Greenfield International School", month, monthEnd, 11_000, InvoiceStatus.Paid),
            new($"EMS-{month:yyMM}-006", "Metro Public Works Dept.", month, monthEnd, 28_000, InvoiceStatus.Due),
            new($"EMS-{last:yyMM}-004", "Precision Castings", last, lastEnd, 41_000, InvoiceStatus.Overdue),
            new($"EMS-{last:yyMM}-006", "Metro Public Works Dept.", last, lastEnd, 28_000, InvoiceStatus.Overdue),
        ];
    }
}
