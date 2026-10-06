namespace EMS.Models.Landing;

/// <summary>Public company details shown on the landing page ("Company" section of appsettings.json). Empty values are hidden.</summary>
public class CompanyOptions
{
    public const string Section = "Company";

    public string Name { get; set; } = "BitProSoftTech";
    public string ProductName { get; set; } = "EMS";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    /// <summary>Public URL of the site, used for canonical and social-sharing links, e.g. https://ems.example.com</summary>
    public string? SiteUrl { get; set; }

    /// <summary><see cref="SiteUrl"/> without a trailing slash, or the current request's scheme and host when it is not set.</summary>
    public string PublicUrl(HttpRequest request) =>
        (string.IsNullOrWhiteSpace(SiteUrl) ? $"{request.Scheme}://{request.Host}" : SiteUrl).TrimEnd('/');
}
