using System.ComponentModel.DataAnnotations;

namespace EMS.Models;

public enum ContactRole { Primary = 1, Secondary, HelpDesk, Director, Manager, HR, Other }

public enum Gender { Male = 1, Female, Other }

public enum EmployeeStatus
{
    Active = 1,
    [Display(Name = "On probation")] OnProbation,
    [Display(Name = "On notice")] OnNotice,
    Resigned,
    Terminated,
}

public enum LeaveStatus { Pending = 1, Approved, Rejected, Cancelled }

public enum ResignationStatus { Pending = 1, Accepted, Rejected, Withdrawn }

public enum AttendanceStatus { Present = 1, Absent, HalfDay, OnLeave, Holiday, WeeklyOff }

public enum AttendanceSource { Biometric = 1, Manual, Import }

public enum Industry
{
    [Display(Name = "Hospital / Clinic")] Hospital = 1,
    [Display(Name = "Hotel / Hospitality")] Hotel,
    [Display(Name = "Government / Public office")] PublicOffice,
    [Display(Name = "Corporate office")] Corporate,
    [Display(Name = "Manufacturing / Factory")] Manufacturing,
    [Display(Name = "School / College")] Education,
    [Display(Name = "Other")] Other,
}

public enum TeamSize
{
    [Display(Name = "Up to 20")] UpTo20 = 1,
    [Display(Name = "21 – 100")] From21To100,
    [Display(Name = "101 – 500")] From101To500,
    [Display(Name = "More than 500")] Above500,
}

public enum EnquiryInterest
{
    [Display(Name = "Free trial")] FreeTrial = 1,
    [Display(Name = "Product demo")] Demo,
    [Display(Name = "Pricing")] Pricing,
    [Display(Name = "Something else")] Other,
}

public enum EnquiryStatus { New = 1, Contacted, Converted, Closed }

public enum EmploymentType
{
    [Display(Name = "Full time")] FullTime = 1,
    [Display(Name = "Part time")] PartTime,
    Contract,
    Intern,
    Consultant,
}

/// <summary>Joining documents collected during onboarding.</summary>
public enum DocumentType
{
    [Display(Name = "PAN card")] PanCard = 1,
    [Display(Name = "Aadhaar card")] AadhaarCard,
    [Display(Name = "Highest qualification certificate")] EducationCertificate,
    [Display(Name = "Cancelled cheque or bank passbook")] BankProof,
    [Display(Name = "Address proof")] AddressProof,
    [Display(Name = "Relieving letter from previous employer")] PreviousRelievingLetter,
    [Display(Name = "Signed offer letter")] SignedOfferLetter,
    [Display(Name = "Passport-size photo")] PassportPhoto,
    [Display(Name = "Other")] Other,
}

public enum DocumentStatus { [Display(Name = "Waiting for verification")] Pending = 1, Verified, Rejected }
