namespace EMS.Services.People;

/// <summary>
/// Employee profile photos, kept outside wwwroot (App_Data/photos) and served through the organization panel so only
/// signed-in users of the same organization see them. JPEG, PNG or WebP up to 2 MB, checked by content.
/// </summary>
public class PhotoStore(IWebHostEnvironment environment)
{
    public const long MaxBytes = 2 * 1024 * 1024;

    private string Folder => Path.Combine(environment.ContentRootPath, "App_Data", "photos");

    /// <summary>Saves the upload and returns its file name, or an error message.</summary>
    public async Task<(string? FileName, string? Error)> SaveAsync(int employeeId, IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0) return (null, "Choose a photo.");
        if (file.Length > MaxBytes) return (null, "The photo must be 2 MB or smaller.");

        var header = new byte[12];
        await using (var stream = file.OpenReadStream())
            _ = await stream.ReadAsync(header, ct);
        var extension = Extension(header);
        if (extension is null) return (null, "Upload a JPEG, PNG or WebP image.");

        Directory.CreateDirectory(Folder);
        var name = $"{employeeId}-{Guid.NewGuid():N}{extension}";
        await using (var target = File.Create(Path.Combine(Folder, name)))
            await file.CopyToAsync(target, ct);
        return (name, null);
    }

    /// <summary>The full path of a stored photo, or null when the name is not one of ours.</summary>
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
        _ => "image/jpeg",
    };

    private static string? Extension(byte[] h) =>
        h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF ? ".jpg"
        : h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 ? ".png"
        : h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50 ? ".webp"
        : null;
}
