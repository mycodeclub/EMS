namespace EMS.Models.Landing;

/// <summary>A question on the public site. Shown in the FAQ, and published as FAQPage structured data and in /llms.txt.</summary>
public record Faq(string Question, string Answer);

/// <summary>
/// Copy the landing page shares with search engines and AI assistants (structured data, /llms.txt), kept in one place so
/// what they read always matches what visitors see.
/// </summary>
public static class LandingContent
{
    public const string Title = "Attendance & HR Software for India";

    public const string Description =
        "Attendance and HR software for hospitals, hotels, factories, schools, offices across India. "
        + "Biometric sync, 24x7 shifts, leave, salary slips. First month free.";

    /// <summary>What the product does, one line each.</summary>
    public static readonly string[] Features =
    [
        "Biometric attendance: punches from your existing devices, matched to the right employee even when device IDs are reused",
        "Shift rosters for 24x7 operations: night shifts counted on the day they start, double shifts, grace time and half-day rules",
        "Leave management: casual, sick and earned leave with yearly quotas, carry-forward, half days and approvals",
        "Salary slips: monthly salary pro-rated by paid days from the attendance register",
        "Employee self-service: staff see their attendance, shift and slips, apply for leave and submit resignations",
        "HR letters: offer, confirmation, appraisal and relieving letters, ready to print or save as PDF",
        "Employee onboarding: joining checklist, PAN and Aadhaar with format checks, bank details and document verification",
        "Multiple offices and branches, each with its own address, GST registration, shifts and state holiday list",
        "Import employees and attendance from Excel or CSV",
        "Complete audit trail: every change shows who made it and when; records are never permanently deleted",
    ];

    public static readonly string[] Industries =
    [
        "Hospitals and clinics", "Hotels and hospitality", "Factories and manufacturing", "Schools and colleges",
        "Government and public offices", "Corporate and IT offices", "Security and facility services", "Small and growing businesses",
    ];

    public static readonly Faq[] Faqs =
    [
        new("What is EMS?",
            "EMS is cloud-based attendance and HR software for Indian organizations. It records attendance from biometric devices, "
            + "plans 24x7 shift rosters, manages leave, prepares salary slips and HR letters, and gives every employee a self-service login."),
        new("Is EMS made for businesses in India?",
            "Yes. It is built for India: PAN, Aadhaar and GSTIN fields with format checks, a separate GST registration for each state, "
            + "state holiday calendars, pricing in rupees, and support for offices in any city or state."),
        new("What happens after the free month?",
            "We share a plan based on your team size and number of offices. Nothing is charged automatically — you decide whether to continue."),
        new("Which biometric devices can we connect?",
            "EMS works with biometric devices that export or send attendance logs. Mention your device make and model in the form below and we'll confirm before you start."),
        new("Can we manage several offices or hospitals together?",
            "Yes, on the Business plan. Each branch has its own address, contacts, shifts and holidays, and you see all of them from one account. The free Starter month covers one office."),
        new("How are night shifts counted?",
            "A shift that starts at night and ends the next morning is counted for the day it started, so attendance and payroll line up."),
        new("Does EMS generate salary slips?",
            "Yes. Each month's salary is pro-rated by the paid days in the attendance register, and every employee can open and print their own salary slip."),
        new("Can we import our existing employee data from Excel?",
            "Yes. Download the template, fill in your employees or past attendance, and upload the Excel or CSV file. Rows with mistakes are listed so you can fix them."),
        new("Can employees see their own attendance and apply for leave?",
            "Yes. Staff with a login see their attendance, shift and salary slips, apply for leave, update their profile, upload joining documents and download their letters."),
        new("What if a device ID is given to a new employee?",
            "EMS keeps the history of every device ID with dates. Older punches stay with the previous employee and new punches go to the new one."),
        new("Can deleted data be recovered?",
            "Yes. Records are never permanently deleted, and every change shows who made it and when."),
    ];
}
