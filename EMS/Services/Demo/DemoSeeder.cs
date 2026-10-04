using System.Security.Claims;
using EMS.Data;
using EMS.Models;
using EMS.Models.Common;
using EMS.Services.Leave;
using EMS.Services.People;
using EMS.Services.Timekeeping;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Services.Demo;

public class DemoOptions
{
    public const string Section = "Demo";

    /// <summary>Creates the demo organization and resets it every night.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Password of every demo login. Shown on the sign-in page.</summary>
    public string Password { get; set; } = "Demo@1234";

    /// <summary>IANA time zone whose midnight triggers the reset, e.g. "Asia/Kolkata". Blank = the server's time zone.</summary>
    public string? TimeZone { get; set; }
}

/// <summary>A demo login, as listed on the sign-in page.</summary>
public record DemoLogin(string UserId, string Email, string Role, string Label, string Description);

/// <summary>
/// The public demo: one organization with an admin, HR, accounts and employee login, a dozen employees on three shifts
/// and attendance from the start of the month three months back until yesterday. <see cref="ResetAsync"/> deletes the demo
/// organization (with whatever visitors changed) and builds it again; no other organization is touched.
/// </summary>
public class DemoSeeder(
    ApplicationDbContext db,
    UserManager<IdentityUser> users,
    LeaveService leave,
    PhotoStore photos,
    IOptions<DemoOptions> options,
    ILogger<DemoSeeder> logger)
{
    /// <summary>User claim on demo logins; they cannot change their password, email or account.</summary>
    public const string DemoClaim = "ems:demo";

    public const string OrganizationName = "Greenfield Institute (Demo)";

    // Fixed ids, so a reset recreates the same accounts.
    public static readonly IReadOnlyList<DemoLogin> Logins =
    [
        new("demo-admin", "admin@greenfield.test", AppRoles.OrgAdmin, "Admin", "Owner of the institute: every page, including the organization profile."),
        new("demo-hr", "hr@greenfield.test", AppRoles.OrgHR, "HR", "Employees, attendance, punch in / out, shifts and imports."),
        new("demo-accounts", "accounts@greenfield.test", AppRoles.OrgAccounts, "Accounts", "Salary sheet and every employee's salary slip."),
        new("demo-employee", "employee@greenfield.test", AppRoles.Employee, "Employee", "Their own attendance and salary slips."),
    ];

    private record DemoEmployee(string FirstName, string LastName, Gender Gender, string Department, string Designation, string Shift, decimal Salary, DateOnly? Joined, string? Login);

    private static readonly DemoEmployee[] Staff =
    [
        new("Meera", "Iyer", Gender.Female, "Human Resources", "HR Manager", "GEN", 55000, new(2022, 6, 1), "demo-hr"),
        new("Rahul", "Verma", Gender.Male, "Accounts", "Accounts Officer", "GEN", 48000, new(2023, 1, 16), "demo-accounts"),
        new("Priya", "Nair", Gender.Female, "Science", "Physics Lecturer", "GEN", 52000, new(2021, 7, 1), "demo-employee"),
        new("Arjun", "Mehta", Gender.Male, "Science", "Chemistry Lecturer", "GEN", 50000, new(2022, 7, 11), null),
        new("Kavita", "Joshi", Gender.Female, "Mathematics", "Mathematics Lecturer", "GEN", 51000, new(2020, 8, 3), null),
        new("Anita", "Deshpande", Gender.Female, "Library", "Librarian", "GEN", 32000, new(2019, 11, 18), null),
        new("Neha", "Gupta", Gender.Female, "English", "English Lecturer", "GEN", 49000, null, null), // joined last month
        new("Sandeep", "Kulkarni", Gender.Male, "Computer Science", "Lab Assistant", "EAR", 26000, new(2023, 3, 1), null),
        new("Vikram", "Singh", Gender.Male, "Administration", "Office Clerk", "EAR", 22000, new(2024, 2, 5), null),
        new("Imran", "Shaikh", Gender.Male, "Maintenance", "Electrician", "EAR", 24000, new(2022, 12, 1), null),
        new("Ramesh", "Patil", Gender.Male, "Security", "Security Guard", "NGT", 18000, new(2021, 9, 1), null),
        new("Suresh", "Yadav", Gender.Male, "Security", "Security Guard", "NGT", 18000, new(2023, 10, 9), null),
    ];

    public bool Enabled => options.Value.Enabled;
    public string Password => options.Value.Password;

    /// <summary>When the demo was last built (UTC), or null if there is none.</summary>
    public Task<DateTime?> LastResetAsync(CancellationToken ct = default) =>
        db.Organizations.Where(o => o.IsDemo).Select(o => (DateTime?)o.CreatedAt).FirstOrDefaultAsync(ct);

    public async Task ResetAsync(CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await DeleteAsync(ct);
        await CreateAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        logger.LogInformation("Demo organization reset.");
    }

    /// <summary>Hard-deletes the demo organization, everything in it (soft-deleted rows too) and the demo logins.</summary>
    private async Task DeleteAsync(CancellationToken ct)
    {
        var demoUserIds = Logins.Select(l => l.UserId).ToList();
        var orgIds = await db.Organizations.IgnoreQueryFilters()
            .Where(o => o.IsDemo || (o.OwnerUserId != null && demoUserIds.Contains(o.OwnerUserId)))
            .Select(o => o.UniqueId).ToListAsync(ct);

        // Logins visitors gave to demo employees (beyond the four fixed ones) go too.
        var extraUserIds = await db.Employees.IgnoreQueryFilters()
            .Where(e => orgIds.Contains(e.OrganizationId) && e.UserId != null && !demoUserIds.Contains(e.UserId))
            .Select(e => e.UserId!).ToListAsync(ct);

        if (orgIds.Count > 0)
        {
            var employees = db.Employees.IgnoreQueryFilters().Where(e => orgIds.Contains(e.OrganizationId)).Select(e => e.UniqueId);
            var photoFiles = await db.Employees.IgnoreQueryFilters().Where(e => orgIds.Contains(e.OrganizationId) && e.PhotoPath != null)
                .Select(e => e.PhotoPath).ToListAsync(ct);
            photoFiles.ForEach(photos.Delete);
            await db.EmployeeExperiences.IgnoreQueryFilters().Where(x => employees.Contains(x.EmployeeId)).ExecuteDeleteAsync(ct);
            await db.SalaryRevisions.IgnoreQueryFilters().Where(x => employees.Contains(x.EmployeeId)).ExecuteDeleteAsync(ct);
            await db.Resignations.IgnoreQueryFilters().Where(x => employees.Contains(x.EmployeeId)).ExecuteDeleteAsync(ct);
            await db.Attendances.IgnoreQueryFilters().Where(a => employees.Contains(a.EmployeeId)).ExecuteDeleteAsync(ct);
            await db.LeaveApplications.IgnoreQueryFilters().Where(a => employees.Contains(a.EmployeeId)).ExecuteDeleteAsync(ct);
            await db.EmployeeLeaveBalances.IgnoreQueryFilters().Where(b => employees.Contains(b.EmployeeId)).ExecuteDeleteAsync(ct);
            await db.BiometricPunches.IgnoreQueryFilters().Where(p => orgIds.Contains(p.OrganizationId)).ExecuteDeleteAsync(ct);
            await db.BiometricIdAssignments.IgnoreQueryFilters().Where(a => orgIds.Contains(a.OrganizationId)).ExecuteDeleteAsync(ct);
            await db.Employees.IgnoreQueryFilters().Where(e => orgIds.Contains(e.OrganizationId))
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.ReportingManagerId, (int?)null), ct);
            await db.Employees.IgnoreQueryFilters().Where(e => orgIds.Contains(e.OrganizationId)).ExecuteDeleteAsync(ct);
            await db.Holidays.IgnoreQueryFilters().Where(h => orgIds.Contains(h.OrganizationId)).ExecuteDeleteAsync(ct);
            await db.LeaveTypes.IgnoreQueryFilters().Where(l => orgIds.Contains(l.OrganizationId)).ExecuteDeleteAsync(ct);
            await db.Shifts.IgnoreQueryFilters().Where(s => orgIds.Contains(s.OrganizationId)).ExecuteDeleteAsync(ct);
            await db.ContactPersons.IgnoreQueryFilters().Where(c => orgIds.Contains(c.OrganizationId)).ExecuteDeleteAsync(ct);
            await db.Branches.IgnoreQueryFilters().Where(b => orgIds.Contains(b.OrganizationId)).ExecuteDeleteAsync(ct);
            await db.Organizations.IgnoreQueryFilters().Where(o => orgIds.Contains(o.UniqueId)).ExecuteDeleteAsync(ct);
        }

        // Identity's own tables (roles, claims, logins, tokens) cascade.
        await db.Users.Where(u => demoUserIds.Contains(u.Id) || extraUserIds.Contains(u.Id)).ExecuteDeleteAsync(ct);
    }

    private async Task CreateAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDay = new DateOnly(today.Year, today.Month, 1).AddMonths(-3);

        var accounts = new Dictionary<string, IdentityUser>();
        foreach (var login in Logins)
        {
            if (await users.FindByEmailAsync(login.Email) is not null)
                throw new InvalidOperationException($"{login.Email} is used by a login outside the demo. Remove it before the demo can be created.");

            var user = new IdentityUser { Id = login.UserId, UserName = login.Email, Email = login.Email, EmailConfirmed = true };
            Ensure(await users.CreateAsync(user, Password), login.Email);
            Ensure(await users.AddToRoleAsync(user, login.Role), login.Email);
            Ensure(await users.AddClaimAsync(user, new Claim(DemoClaim, "true")), login.Email);
            accounts[login.UserId] = user;
        }

        var organization = new Organization
        {
            Name = OrganizationName,
            RegisteredName = "Greenfield Education Trust",
            Industry = Industry.Education,
            Email = "admin@greenfield.test",
            Phone = "020 2400 0000",
            Address = new Address { Line1 = "12 College Road", Line2 = "Shivajinagar", City = "Pune", State = "Maharashtra", PinCode = "411005" },
            OwnerUserId = accounts["demo-admin"].Id,
            IsDemo = true,
            IsHolidayCalendarApplicable = true,
            OperatesInShifts = true,
            OnboardingCompletedAt = DateTime.UtcNow,
        };
        var branch = new Branch
        {
            Organization = organization, Name = "Head office", Code = "HO",
            Address = new Address { Line1 = "12 College Road", Line2 = "Shivajinagar", City = "Pune", State = "Maharashtra", PinCode = "411005" },
        };
        organization.Branches.Add(branch);
        organization.Contacts.Add(new ContactPerson
        {
            Organization = organization, Role = ContactRole.Primary, Name = "Demo Admin", Designation = "Principal",
            Phone = organization.Phone, Email = organization.Email,
        });

        var shifts = new Dictionary<string, Shift>
        {
            ["GEN"] = NewShift(organization, "General", "GEN", new(9, 0), new(17, 30)),
            ["EAR"] = NewShift(organization, "Early", "EAR", new(7, 0), new(15, 0)),
            ["NGT"] = NewShift(organization, "Night", "NGT", new(22, 0), new(6, 0)),
        };
        foreach (var shift in shifts.Values) organization.Shifts.Add(shift);

        var holidays = new[] { (1, 26, "Republic Day"), (8, 15, "Independence Day"), (10, 2, "Gandhi Jayanti") }
            .SelectMany(h => new[] { firstDay.Year, today.Year }.Distinct().Select(y => (Date: new DateOnly(y, h.Item1, h.Item2), Name: h.Item3)))
            .Where(h => h.Date >= firstDay && h.Date < today)
            .ToDictionary(h => h.Date, h => h.Name);
        foreach (var (date, name) in holidays)
            organization.Holidays.Add(new Holiday { Organization = organization, Date = date, Name = name });

        var employees = new List<Employee>();
        for (var i = 0; i < Staff.Length; i++)
        {
            var s = Staff[i];
            var employee = new Employee
            {
                Organization = organization,
                Branch = branch,
                Shift = shifts[s.Shift],
                EmpCode = $"E{i + 1:000}",
                FirstName = s.FirstName,
                LastName = s.LastName,
                Gender = s.Gender,
                Department = s.Department,
                Designation = s.Designation,
                Email = s.Login is { } id ? Logins.First(l => l.UserId == id).Email : null,
                Mobile = $"98765{43210 + i * 7:00000}",
                DateOfJoining = s.Joined ?? new DateOnly(today.Year, today.Month, 1).AddMonths(-1).AddDays(9),
                MonthlySalary = s.Salary,
                UserId = s.Login,
            };
            employees.Add(employee);
            organization.Employees.Add(employee);
        }

        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        for (var i = 0; i < employees.Count; i++)
        {
            var employee = employees[i];
            for (var day = firstDay; day < today; day = day.AddDays(1))
            {
                if (day < employee.DateOfJoining) continue;
                db.Attendances.Add(DayRecord(employee, i, day, holidays.ContainsKey(day)));
            }
        }
        await db.SaveChangesAsync(ct);

        await SeedLeaveAsync(employees, today, ct);
        await SeedPeopleAsync(employees, today, ct);
    }

    /// <summary>Leave types and balances; every seeded "On leave" day becomes an approved casual leave.</summary>
    private async Task SeedLeaveAsync(List<Employee> employees, DateOnly today, CancellationToken ct)
    {
        var casual = (await leave.TypesAsync(employees[0].OrganizationId, ct)).First(t => t.Code == "CL");
        var sick = (await leave.TypesAsync(employees[0].OrganizationId, ct)).First(t => t.Code == "SL");
        var leaveDays = await db.Attendances.Where(a => a.Status == AttendanceStatus.OnLeave && employees.Select(e => e.UniqueId).Contains(a.EmployeeId)).ToListAsync(ct);

        foreach (var employee in employees)
        {
            foreach (var year in leaveDays.Where(a => a.EmployeeId == employee.UniqueId).Select(a => a.AttendanceDate.Year).Append(today.Year).Distinct())
                await leave.BalancesAsync(employee, year, ct);
        }
        var balances = await db.EmployeeLeaveBalances.Where(b => employees.Select(e => e.UniqueId).Contains(b.EmployeeId)).ToListAsync(ct);

        foreach (var day in leaveDays)
        {
            db.LeaveApplications.Add(new LeaveApplication
            {
                EmployeeId = day.EmployeeId, LeaveTypeId = casual.UniqueId, FromDate = day.AttendanceDate, ToDate = day.AttendanceDate,
                TotalDays = 1, Reason = "Personal work", AppliedOn = day.AttendanceDate.AddDays(-3).ToDateTime(new TimeOnly(10, 0)).ToUniversalTime(),
                Status = LeaveStatus.Approved, ActionedOn = day.AttendanceDate.AddDays(-2).ToDateTime(new TimeOnly(11, 0)).ToUniversalTime(),
            });
            day.Remarks = "Casual leave (approved)";
            var balance = balances.First(b => b.EmployeeId == day.EmployeeId && b.LeaveTypeId == casual.UniqueId && b.Year == day.AttendanceDate.Year);
            balance.Used = Math.Min(balance.Used + 1, balance.Total);
        }

        // Waiting for HR: the demo employee next week, and a sick day tomorrow for E005.
        var monday = today.AddDays(((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7 + 7);
        Pending(employees[2], casual, monday, monday.AddDays(1), 2, "Cousin's wedding in Nagpur");
        var tomorrow = today.AddDays(today.DayOfWeek == DayOfWeek.Saturday ? 2 : 1);
        Pending(employees[4], sick, tomorrow, tomorrow, 1, "Dental appointment");
        await db.SaveChangesAsync(ct);

        void Pending(Employee e, LeaveType type, DateOnly from, DateOnly to, decimal days, string reason) =>
            db.LeaveApplications.Add(new LeaveApplication
            {
                EmployeeId = e.UniqueId, LeaveTypeId = type.UniqueId, FromDate = from, ToDate = to, TotalDays = days,
                Reason = reason, AppliedOn = DateTime.UtcNow.AddHours(-5),
            });
    }

    /// <summary>A full profile and an appraisal for the demo employee, one colleague on notice and one resignation to review.</summary>
    private async Task SeedPeopleAsync(List<Employee> employees, DateOnly today, CancellationToken ct)
    {
        var priya = employees[2];
        priya.DateOfBirth = new DateOnly(1992, 3, 14);
        priya.HighestQualification = "M.Sc. Physics, University of Pune";
        priya.CurrentAddress = "Flat 4B, Sai Residency, Baner Road, Pune 411045";
        priya.EmergencyContactName = "Suresh Nair (father)";
        priya.EmergencyContactPhone = "98220 11223";
        priya.Pan = "ABCDE1234F";
        priya.Aadhaar = "234567890123";
        priya.BankAccountHolder = "Priya Nair";
        priya.BankName = "State Bank of India";
        priya.BankAccountNumber = "30012345678";
        priya.BankIfsc = "SBIN0001234";
        db.EmployeeExperiences.AddRange(
            new EmployeeExperience { EmployeeId = priya.UniqueId, Company = "Sunrise Junior College", Designation = "Physics Teacher", FromDate = new(2016, 6, 1), ToDate = new(2019, 5, 31) },
            new EmployeeExperience { EmployeeId = priya.UniqueId, Company = "Apex Coaching Classes", Designation = "Senior Faculty", FromDate = new(2019, 6, 15), ToDate = new(2021, 6, 15) });
        priya.PriorExperienceMonths = 59;

        // Appraisal on 1 April: the salary before it was 48,000.
        var april = new DateOnly(today.Month >= 4 ? today.Year : today.Year - 1, 4, 1);
        db.SalaryRevisions.Add(new SalaryRevision
        {
            EmployeeId = priya.UniqueId, EffectiveFrom = april, PreviousSalary = 48000, NewSalary = priya.MonthlySalary ?? 52000,
            Remarks = $"Annual appraisal {april.Year}: rated Exceeds Expectations.",
        });

        // E004 resigned three weeks ago and is serving notice; E009 has just resigned.
        var arjun = employees[3];
        var submitted = today.AddDays(-10);
        var lastDay = submitted.AddDays(arjun.NoticePeriodDays);
        db.Resignations.Add(new Resignation
        {
            EmployeeId = arjun.UniqueId, SubmittedAt = submitted.ToDateTime(new TimeOnly(10, 30)).ToUniversalTime(),
            Reason = "Pursuing a PhD at IISER Pune.", RequestedLastDay = lastDay, LastWorkingDay = lastDay,
            Status = ResignationStatus.Accepted, ActionedAt = submitted.AddDays(1).ToDateTime(new TimeOnly(12, 0)).ToUniversalTime(),
            Remarks = "Accepted. Please complete the hand-over of lab records.",
        });
        arjun.DateOfLeaving = lastDay;
        arjun.Status = EmployeeStatus.OnNotice;

        var vikram = employees[8];
        db.Resignations.Add(new Resignation
        {
            EmployeeId = vikram.UniqueId, SubmittedAt = DateTime.UtcNow.AddHours(-20),
            Reason = "Relocating to my home town.", RequestedLastDay = today.AddDays(vikram.NoticePeriodDays),
        });
        await db.SaveChangesAsync(ct);
    }

    private static Shift NewShift(Organization organization, string name, string code, TimeOnly start, TimeOnly end)
    {
        const int breakMinutes = 30;
        var worked = (int)(end - start).TotalMinutes - breakMinutes; // wraps past midnight
        return new Shift
        {
            Organization = organization, Name = name, Code = code, StartTime = start, EndTime = end,
            BreakMinutes = breakMinutes, GraceMinutes = 15, FullDayMinutes = worked, HalfDayMinutes = worked / 2,
        };
    }

    /// <summary>A realistic day: mostly on time, some late arrivals, the odd half day, leave or absence. Same result every reset.</summary>
    private static Attendance DayRecord(Employee employee, int index, DateOnly day, bool holiday)
    {
        var record = new Attendance { Employee = employee, Shift = employee.Shift, AttendanceDate = day, Source = AttendanceSource.Biometric };
        if (holiday || day.DayOfWeek == DayOfWeek.Sunday)
        {
            record.Status = holiday ? AttendanceStatus.Holiday : AttendanceStatus.WeeklyOff;
            record.Source = AttendanceSource.Manual;
            return record;
        }

        var random = new Random(index * 100_003 + day.DayNumber);
        var roll = random.Next(100);
        if (roll < 7)
        {
            record.Status = roll < 3 ? AttendanceStatus.Absent : AttendanceStatus.OnLeave;
            record.Source = AttendanceSource.Manual;
            record.Remarks = roll < 3 ? null : "Casual leave";
            return record;
        }

        var shift = employee.Shift!;
        var lateBy = random.Next(100) < 12 ? random.Next(16, 46) : random.Next(-12, 11);
        var timeIn = shift.StartTime.AddMinutes(lateBy);
        var timeOut = roll < 10
            ? shift.StartTime.AddMinutes(shift.HalfDayMinutes + random.Next(10, 60)) // left early: half day
            : shift.EndTime.AddMinutes(random.Next(0, 31));

        var result = PunchRules.Evaluate(shift, day, timeIn, timeOut);
        record.LoginAt = result.LoginAt;
        record.LogoffAt = result.LogoffAt;
        record.WorkedMinutes = result.WorkedMinutes;
        record.IsLate = result.IsLate;
        record.Status = result.Status ?? AttendanceStatus.Present;
        return record;
    }

    private static void Ensure(IdentityResult result, string email)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not create demo login {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
    }
}
