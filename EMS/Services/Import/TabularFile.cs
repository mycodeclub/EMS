using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace EMS.Services.Import;

/// <summary>One data row of an uploaded file. <see cref="Number"/> is the row number the user sees in Excel (header = 1).</summary>
public record FileRow(int Number, IReadOnlyList<string> Cells)
{
    public bool IsBlank => Cells.All(string.IsNullOrWhiteSpace);
}

/// <summary>The header row and data rows of an uploaded CSV or Excel file, every cell as text.</summary>
public record TabularFile(IReadOnlyList<string> Headers, IReadOnlyList<FileRow> Rows)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const int MaxRows = 20_000;

    /// <summary>Reads a .csv or .xlsx file. Excel date and time cells come back as yyyy-MM-dd and HH:mm.</summary>
    /// <exception cref="InvalidDataException">The file cannot be read; the message is shown to the user.</exception>
    public static TabularFile Read(Stream stream, string fileName, string preferredSheet)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var file = extension switch
        {
            ".csv" or ".txt" => ReadCsv(stream),
            ".xlsx" or ".xlsm" => ReadExcel(stream, preferredSheet),
            ".xls" => throw new InvalidDataException("Old .xls files are not supported. In Excel, use File › Save As › Excel Workbook (.xlsx), or CSV."),
            _ => throw new InvalidDataException("Upload a .csv or .xlsx file."),
        };
        if (file.Headers.All(string.IsNullOrWhiteSpace)) throw new InvalidDataException("The file is empty. The first row must be the column headings.");
        if (file.Rows.Count > MaxRows) throw new InvalidDataException($"The file has {file.Rows.Count:N0} rows. Upload at most {MaxRows:N0} rows at a time.");
        return file;
    }

    private static TabularFile ReadExcel(Stream stream, string preferredSheet)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("This Excel file could not be opened. Save it again as .xlsx (or CSV) and upload that.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.Equals(preferredSheet, StringComparison.OrdinalIgnoreCase))
                        ?? workbook.Worksheets.First(s => !s.Name.Equals("Instructions", StringComparison.OrdinalIgnoreCase));
            var used = sheet.RangeUsed();
            if (used is null) return new([], []);

            var lastColumn = used.LastColumn().ColumnNumber();
            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            if (lastRow - firstRow > MaxRows) throw new InvalidDataException($"The sheet has more than {MaxRows:N0} rows. Upload at most {MaxRows:N0} rows at a time.");

            List<string> Cells(int row) => Enumerable.Range(1, lastColumn).Select(c => CellText(sheet.Cell(row, c))).ToList();
            var rows = Enumerable.Range(firstRow + 1, lastRow - firstRow).Select(r => new FileRow(r, Cells(r))).Where(r => !r.IsBlank).ToList();
            return new(Cells(firstRow), rows);
        }
    }

    private static string CellText(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsDateTime)
        {
            var dt = value.GetDateTime();
            // Excel stores a time on its own as a date of 30/31 Dec 1899.
            if (dt.Year < 1901) return dt.ToString("HH:mm", CultureInfo.InvariantCulture);
            return dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        if (value.IsTimeSpan) return value.GetTimeSpan().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        if (value.IsNumber) return value.GetNumber().ToString(CultureInfo.InvariantCulture);
        if (value.IsBoolean) return value.GetBoolean() ? "TRUE" : "FALSE";
        if (value.IsError) return string.Empty;
        return value.IsText ? value.GetText().Trim() : string.Empty;
    }

    /// <summary>RFC 4180 CSV with quoted fields. The delimiter (comma, semicolon or tab) is taken from the header row.</summary>
    private static TabularFile ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var firstLine = text.Split('\n', 2)[0];
        var delimiter = new[] { ',', ';', '\t' }.OrderByDescending(d => firstLine.Count(c => c == d)).First();

        var records = new List<List<string>>();
        var cells = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void EndField() { cells.Add(field.ToString().Trim()); field.Clear(); }
        void EndRecord() { EndField(); records.Add(cells); cells = []; }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) EndField();
            else if (c == '\r') { }
            else if (c == '\n') EndRecord();
            else field.Append(c);
        }
        if (field.Length > 0 || cells.Count > 0) EndRecord();

        if (records.Count == 0) return new([], []);
        // Row numbers as a spreadsheet shows them: the header is row 1.
        var rows = records.Skip(1).Select((r, i) => new FileRow(i + 2, r)).Where(r => !r.IsBlank).ToList();
        return new(records[0], rows);
    }
}
