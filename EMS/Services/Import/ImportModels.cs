namespace EMS.Services.Import;

public enum ImportKind { Employees = 1, Attendance }

/// <summary>A column of an import file. Headings match ignoring case, spaces and punctuation, or by any alias.</summary>
public record ImportColumn(string Key, string Header, bool Required, string Help, string Example, params string[] Aliases)
{
    public bool Matches(string heading)
    {
        var key = Normalize(heading);
        return key.Length > 0 && (key == Normalize(Header) || Aliases.Any(a => Normalize(a) == key));
    }

    public static string Normalize(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}

/// <summary>A problem with one row (or the whole file, when <see cref="Row"/> is 0).</summary>
public record ImportError(int Row, string? Column, string Message);

public record ImportResult(ImportKind Kind, string FileName, int Added, int Updated, int Skipped, IReadOnlyList<ImportError> Errors, IReadOnlyList<string> Notes)
{
    public bool Succeeded => Errors.Count == 0;

    public static ImportResult Failed(ImportKind kind, string fileName, params ImportError[] errors) => new(kind, fileName, 0, 0, 0, errors, []);
}

/// <summary>Finds each known column in the file's header row and reads cells by column key.</summary>
public class ColumnMap
{
    private readonly Dictionary<string, int> _index = new();

    private ColumnMap() { }

    public List<string> Unknown { get; } = [];

    public static ColumnMap Build(TabularFile file, IReadOnlyList<ImportColumn> columns)
    {
        var map = new ColumnMap();
        for (var i = 0; i < file.Headers.Count; i++)
        {
            var heading = file.Headers[i];
            if (string.IsNullOrWhiteSpace(heading)) continue;
            var column = columns.FirstOrDefault(c => c.Matches(heading));
            if (column is null) map.Unknown.Add(heading.Trim());
            else map._index.TryAdd(column.Key, i);
        }
        return map;
    }

    public bool Has(string key) => _index.ContainsKey(key);

    public string? Get(FileRow row, string key)
    {
        if (!_index.TryGetValue(key, out var i) || i >= row.Cells.Count) return null;
        var value = row.Cells[i].Trim();
        return value.Length == 0 ? null : value;
    }
}
