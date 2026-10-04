using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using EMS.Models;
using EMS.Services.Timekeeping;

namespace EMS.Services.Import;

public record TemplateFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Blank or sample-filled import templates, as CSV or Excel. The Excel file has an Instructions sheet and
/// drop-down lists for status, gender and the organization's own shift codes.
/// </summary>
public static class ImportTemplates
{
    public const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <param name="shifts">The organization's shifts, offered in the Shift drop-down and used in sample rows.</param>
    /// <param name="employeeCodes">Up to three current employee codes, used in attendance sample rows (E001… when there are none).</param>
    public static TemplateFile Build(ImportKind kind, bool excel, bool withSamples, IReadOnlyList<Shift> shifts, IReadOnlyList<string> employeeCodes)
    {
        var columns = kind == ImportKind.Employees ? EmployeeImporter.Columns : AttendanceImporter.Columns;
        // The attendance template uses employee codes; the Biometric ID column is for device exports.
        if (kind == ImportKind.Attendance) columns = columns.Where(c => c.Key != "biometric").ToList();
        var rows = withSamples ? (kind == ImportKind.Employees ? EmployeeSamples(shifts) : AttendanceSamples(employeeCodes)) : [];
        var name = $"ems-{kind.ToString().ToLowerInvariant()}-{(withSamples ? "sample" : "template")}";

        return excel
            ? new(Excel(kind, columns, rows, shifts), ExcelType, name + ".xlsx")
            : new(Csv(columns, rows), "text/csv", name + ".csv");
    }

    /// <summary>A sample cell: text, or a date / time so Excel shows it as one.</summary>
    private record Cell(string Text, DateOnly? Date = null, TimeOnly? Time = null, decimal? Number = null)
    {
        public static implicit operator Cell(string text) => new(text);
        public static Cell On(DateOnly date) => new(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Date: date);
        public static Cell At(int hour, int minute) => new($"{hour:00}:{minute:00}", Time: new TimeOnly(hour, minute));
        public static Cell Amount(decimal value) => new(value.ToString(CultureInfo.InvariantCulture), Number: value);
    }

    private static List<Cell[]> EmployeeSamples(IReadOnlyList<Shift> shifts)
    {
        // Employee 2 is on a night shift when there is one, to match the attendance sample.
        var day = shifts.FirstOrDefault(s => !s.CrossesMidnight) ?? shifts.FirstOrDefault();
        var night = shifts.FirstOrDefault(s => s.CrossesMidnight) ?? day;
        string ShiftCode(int i) => (i == 1 ? night : day)?.Code ?? "";
        // Columns: code, first, last, gender, dob, mobile, email, department, designation, joined, left, shift, salary, biometric
        return
        [
            ["", "Sample", "Employee 1", "Female", Cell.On(new(1994, 6, 15)), "+91 90000 00001", "employee1@example.com", "Operations", "Executive", Cell.On(new(2026, 4, 1)), "", ShiftCode(0), Cell.Amount(25000), ""],
            ["", "Sample", "Employee 2", "Male", Cell.On(new(1990, 1, 20)), "+91 90000 00002", "", "Operations", "Supervisor", Cell.On(new(2025, 11, 10)), "", ShiftCode(1), Cell.Amount(32000), "1002"],
            ["", "Sample", "Employee 3", "", "", "+91 90000 00003", "", "Support", "Assistant", Cell.On(new(2026, 7, 1)), "", ShiftCode(2), Cell.Amount(18000), ""],
        ];
    }

    private static List<Cell[]> AttendanceSamples(IReadOnlyList<string> employeeCodes)
    {
        string Code(int i) => employeeCodes.Count > i ? employeeCodes[i] : $"E{i + 1:000}";
        var today = DateOnly.FromDateTime(AppClock.Today);
        var day1 = today.AddDays(-2);
        var day2 = today.AddDays(-1);
        // Columns: code, date, in, out, status, remarks
        return
        [
            [Code(0), Cell.On(day1), Cell.At(9, 0), Cell.At(18, 0), "", "Status worked out from the times"],
            [Code(2), Cell.On(day1), Cell.At(9, 25), Cell.At(18, 10), "", "Late arrival"],
            [Code(1), Cell.On(day1), Cell.At(22, 0), Cell.At(6, 0), "", "Night shift: out is the next morning"],
            [Code(0), Cell.On(day2), Cell.At(9, 5), Cell.At(13, 30), "HD", "Half day"],
            [Code(2), Cell.On(day2), "", "", "A", ""],
            [Code(1), Cell.On(day2), "", "", "L", "Casual leave"],
        ];
    }

    private static byte[] Csv(IReadOnlyList<ImportColumn> columns, List<Cell[]> rows)
    {
        static string Quote(string value) => value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", columns.Select(c => Quote(c.Header))));
        foreach (var row in rows) csv.AppendLine(string.Join(",", row.Select(c => Quote(c.Text))));
        // UTF-8 with a byte order mark, so Excel opens ₹ and non-English names correctly.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static byte[] Excel(ImportKind kind, IReadOnlyList<ImportColumn> columns, List<Cell[]> rows, IReadOnlyList<Shift> shifts)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(kind.ToString());

        for (var c = 0; c < columns.Count; c++)
        {
            var header = sheet.Cell(1, c + 1);
            header.Value = columns[c].Header;
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = columns[c].Required ? XLColor.FromHtml("#E8F1FC") : XLColor.FromHtml("#F5F5F7");
            if (!string.IsNullOrEmpty(columns[c].Help)) header.CreateComment().AddText(columns[c].Help);

            // Formats for the whole column, so typed dates and times are recognised.
            var column = sheet.Column(c + 1);
            switch (columns[c].Key)
            {
                case "dob" or "joined" or "left" or "date":
                    column.Style.NumberFormat.Format = "dd-mmm-yyyy";
                    break;
                case "in" or "out":
                    column.Style.NumberFormat.Format = "hh:mm";
                    break;
                case "salary":
                    column.Style.NumberFormat.Format = "#,##0";
                    break;
                case "code" or "biometric" or "mobile":
                    column.Style.NumberFormat.Format = "@"; // text, so leading zeros and +91 are kept
                    break;
            }
        }
        sheet.Row(1).Style.Font.Bold = true;

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                var value = rows[r][c];
                if (value.Date is { } date) cell.Value = date.ToDateTime(TimeOnly.MinValue);
                else if (value.Time is { } time) cell.Value = time.ToTimeSpan();
                else if (value.Number is { } number) cell.Value = number;
                else if (value.Text.Length > 0) cell.Value = value.Text;
            }
        }

        // Drop-down lists (typing another value is refused by Excel, so blanks stay allowed).
        var lastRow = Math.Max(rows.Count + 1, 500);
        void List(string key, IEnumerable<string> values)
        {
            var index = columns.ToList().FindIndex(c => c.Key == key);
            var options = values.ToList();
            if (index < 0 || options.Count == 0) return;
            var validation = sheet.Range(2, index + 1, lastRow, index + 1).CreateDataValidation();
            validation.IgnoreBlanks = true;
            validation.List("\"" + string.Join(",", options) + "\"", true);
        }
        List("status", AttendanceCodes.All.Select(c => c.Code));
        List("gender", Enum.GetNames<Gender>());
        List("shift", shifts.Select(s => s.Code));

        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, columns.Count).AdjustToContents(1, Math.Max(rows.Count + 1, 1));
        foreach (var column in sheet.Columns(1, columns.Count)) column.Width = Math.Clamp(column.Width + 2, 12, 40);

        AddInstructions(workbook, kind, columns, shifts);
        return Save(workbook);
    }

    private static void AddInstructions(XLWorkbook workbook, ImportKind kind, IReadOnlyList<ImportColumn> columns, IReadOnlyList<Shift> shifts)
    {
        var sheet = workbook.AddWorksheet("Instructions");
        sheet.Cell(1, 1).Value = kind == ImportKind.Employees ? "Importing employees" : "Importing attendance";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(2, 1).Value = kind == ImportKind.Employees
            ? "Fill one row per employee on the Employees sheet, then upload this file on the Import page. Blue headings are required."
            : "Fill one row per employee per day on the Attendance sheet, then upload this file on the Import page. Blue headings are required.";
        sheet.Cell(3, 1).Value = "Dates are read day first (05-09-2026 is 5 September). Every row is checked before anything is saved.";

        var r = 5;
        string[] headings = ["Column", "Required", "What to enter"];
        for (var c = 0; c < headings.Length; c++)
        {
            sheet.Cell(r, c + 1).Value = headings[c];
            sheet.Cell(r, c + 1).Style.Font.Bold = true;
            sheet.Cell(r, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F7");
        }
        foreach (var column in columns)
        {
            r++;
            sheet.Cell(r, 1).Value = column.Header;
            sheet.Cell(r, 2).Value = column.Required ? "Yes" : "";
            sheet.Cell(r, 3).Value = column.Help;
        }

        if (kind == ImportKind.Attendance)
        {
            r += 2;
            sheet.Cell(r, 1).Value = "Status codes";
            sheet.Cell(r, 1).Style.Font.Bold = true;
            foreach (var (_, code, name) in AttendanceCodes.All)
            {
                r++;
                sheet.Cell(r, 1).Value = code;
                sheet.Cell(r, 2).Value = name;
            }
        }
        if (shifts.Count > 0)
        {
            r += 2;
            sheet.Cell(r, 1).Value = "Your shifts";
            sheet.Cell(r, 1).Style.Font.Bold = true;
            foreach (var shift in shifts)
            {
                r++;
                sheet.Cell(r, 1).Value = shift.Code;
                sheet.Cell(r, 2).Value = shift.Name;
                sheet.Cell(r, 3).Value = $"{PunchRules.Clock(shift.StartTime)}–{PunchRules.Clock(shift.EndTime)}";
            }
        }

        sheet.Column(1).Width = 18;
        sheet.Column(2).Width = 14;
        sheet.Column(3).Width = 90;
        sheet.Column(3).Style.Alignment.WrapText = true;
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
