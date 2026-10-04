namespace EMS.Services.People;

/// <summary>
/// Files kept outside wwwroot (App_Data/&lt;folder&gt;) and served only through controllers that check who is asking.
/// The type is checked from the file's content, not its name.
/// </summary>
public abstract class UploadStore(IWebHostEnvironment environment, string folder, long maxBytes, bool allowPdf)
{
    public long MaxBytes => maxBytes;

    private string Folder => Path.Combine(environment.ContentRootPath, "App_Data", folder);

    /// <summary>Saves the upload and returns its stored file name and content type, or an error message.</summary>
    public async Task<(string? FileName, string? ContentType, string? Error)> SaveAsync(int employeeId, IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0) return (null, null, "Choose a file.");
        if (file.Length > maxBytes) return (null, null, $"The file must be {maxBytes / 1024 / 1024} MB or smaller.");

        var header = new byte[12];
        await using (var stream = file.OpenReadStream())
            _ = await stream.ReadAsync(header, ct);
        var extension = Extension(header);
        if (extension is null || (extension == ".pdf" && !allowPdf))
            return (null, null, allowPdf ? "Upload a PDF, JPEG, PNG or WebP file." : "Upload a JPEG, PNG or WebP image.");

        Directory.CreateDirectory(Folder);
        var name = $"{employeeId}-{Guid.NewGuid():N}{extension}";
        await using (var target = File.Create(Path.Combine(Folder, name)))
            await file.CopyToAsync(target, ct);
        return (name, ContentType(name), null);
    }

    /// <summary>Writes bytes directly (demo seed). Returns the stored name.</summary>
    public string Save(int employeeId, byte[] content, string extension)
    {
        Directory.CreateDirectory(Folder);
        var name = $"{employeeId}-{Guid.NewGuid():N}{extension}";
        File.WriteAllBytes(Path.Combine(Folder, name), content);
        return name;
    }

    /// <summary>The full path of a stored file, or null when the name is not one of ours.</summary>
    public string? PathOf(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName) || fileName != Path.GetFileName(fileName)) return null;
        var path = Path.Combine(Folder, fileName);
        return File.Exists(path) ? path : null;
    }

    public void Delete(string? fileName)
    {
        if (PathOf(fileName) is { } path) File.Delete(path);
    }

    public static string ContentType(string fileName) => Path.GetExtension(fileName) switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "image/jpeg",
    };

    private static string? Extension(byte[] h) =>
        h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF ? ".jpg"
        : h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 ? ".png"
        : h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50 ? ".webp"
        : h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46 ? ".pdf"
        : null;
}

/// <summary>Employee profile photos (App_Data/photos): JPEG, PNG or WebP up to 2 MB.</summary>
public class PhotoStore(IWebHostEnvironment environment) : UploadStore(environment, "photos", 2 * 1024 * 1024, allowPdf: false)
{
    public const long Limit = 2 * 1024 * 1024;
}

/// <summary>Joining documents (App_Data/documents): PDF, JPEG, PNG or WebP up to 5 MB.</summary>
public class DocumentStore(IWebHostEnvironment environment) : UploadStore(environment, "documents", 5 * 1024 * 1024, allowPdf: true)
{
    public const long Limit = 5 * 1024 * 1024;
}
