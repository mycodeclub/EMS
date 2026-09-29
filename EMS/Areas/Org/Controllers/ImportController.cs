using EMS.Areas.Org.Models;
using EMS.Data;
using EMS.Services.Import;
using EMS.Services.Onboarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EMS.Areas.Org.Controllers;

/// <summary>Bulk import of employees and attendance from CSV or Excel, with downloadable templates.</summary>
public class ImportController(
    OrganizationContext context, ApplicationDbContext db, SetupService setup,
    EmployeeImporter employeeImporter, AttendanceImporter attendanceImporter, ILogger<ImportController> logger)
    : OrgController(context)
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await PageAsync(null, ct));

    /// <summary>Template for <paramref name="kind"/>: CSV or Excel (<paramref name="format"/>), blank or with sample rows.</summary>
    public async Task<IActionResult> Template(ImportKind kind, string? format, bool samples, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind)) return NotFound();
        var shifts = await setup.ShiftsAsync(Organization.UniqueId, ct);
        var codes = samples && kind == ImportKind.Attendance
            ? await db.Employees.Where(e => e.OrganizationId == Organization.UniqueId && e.DateOfLeaving == null)
                .OrderBy(e => e.EmpCode).Select(e => e.EmpCode).Take(3).ToListAsync(ct)
            : [];
        var file = ImportTemplates.Build(kind, format == "xlsx", samples, shifts, codes);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(TabularFile.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Employees(IFormFile? file, bool updateExisting, CancellationToken ct)
    {
        var result = await RunAsync(ImportKind.Employees, file, sheet => employeeImporter.ImportAsync(Organization, sheet, file!.FileName, updateExisting, ct));
        return View("Index", await PageAsync(result, ct));
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(TabularFile.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Attendance(IFormFile? file, bool replaceExisting, CancellationToken ct)
    {
        var result = await RunAsync(ImportKind.Attendance, file, sheet => attendanceImporter.ImportAsync(Organization, sheet, file!.FileName, replaceExisting, ct));
        return View("Index", await PageAsync(result, ct));
    }

    private async Task<ImportResult> RunAsync(ImportKind kind, IFormFile? file, Func<TabularFile, Task<ImportResult>> import)
    {
        if (file is null || file.Length == 0) return ImportResult.Failed(kind, "", new ImportError(0, null, "Choose a file to upload."));
        if (file.Length > TabularFile.MaxBytes) return ImportResult.Failed(kind, file.FileName, new ImportError(0, null, "The file is larger than 5 MB. Split it into smaller files."));

        TabularFile sheet;
        try
        {
            await using var stream = file.OpenReadStream();
            sheet = TabularFile.Read(stream, file.FileName, kind.ToString());
        }
        catch (InvalidDataException ex)
        {
            return ImportResult.Failed(kind, file.FileName, new ImportError(0, null, ex.Message));
        }

        var result = await import(sheet);
        logger.LogInformation("{Kind} import of {File} for organization {Org}: {Added} added, {Updated} updated, {Skipped} skipped, {Errors} errors",
            kind, file.FileName, Organization.UniqueId, result.Added, result.Updated, result.Skipped, result.Errors.Count);
        return result;
    }

    private async Task<ImportPage> PageAsync(ImportResult? result, CancellationToken ct) => new(
        result,
        await db.Employees.CountAsync(e => e.OrganizationId == Organization.UniqueId, ct),
        (await setup.ShiftsAsync(Organization.UniqueId, ct)).Select(s => s.Code).ToList());
}
