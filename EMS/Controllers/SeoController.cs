using System.Text;
using EMS.Models.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EMS.Controllers;

/// <summary>
/// Files crawlers look for at the site root: robots.txt and sitemap.xml for search engines, and llms.txt, a plain summary
/// for AI assistants (ChatGPT, Claude, Perplexity, Gemini). Only the public pages are listed; the app itself is not indexed.
/// </summary>
[ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
public class SeoController(IOptions<CompanyOptions> options) : Controller
{
    /// <summary>Public pages, in the order they matter.</summary>
    private static readonly (string Path, string ChangeFrequency, string Priority)[] Pages =
    [
        ("/", "weekly", "1.0"),
        ("/Home/Privacy", "yearly", "0.3"),
    ];

    private CompanyOptions Company => options.Value;
    private string SiteUrl => Company.PublicUrl(Request);

    [HttpGet("/robots.txt")]
    public ContentResult Robots() => Text(
        $"""
        User-agent: *
        Allow: /
        Disallow: /Admin
        Disallow: /Org
        Disallow: /Onboarding
        Disallow: /Identity

        Sitemap: {SiteUrl}/sitemap.xml
        """);

    [HttpGet("/sitemap.xml")]
    public ContentResult Sitemap()
    {
        var xml = new StringBuilder("""<?xml version="1.0" encoding="UTF-8"?>""").AppendLine()
            .AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");
        foreach (var (path, frequency, priority) in Pages)
            xml.AppendLine($"  <url><loc>{SiteUrl}{path}</loc><changefreq>{frequency}</changefreq><priority>{priority}</priority></url>");
        xml.AppendLine("</urlset>");
        return Content(xml.ToString(), "application/xml; charset=utf-8");
    }

    [HttpGet("/llms.txt")]
    public ContentResult Llms()
    {
        var text = new StringBuilder()
            .AppendLine($"# {Company.ProductName} by {Company.Name}")
            .AppendLine()
            .AppendLine($"> {LandingContent.Description}")
            .AppendLine()
            .AppendLine("## Who it is for")
            .AppendLine();
        foreach (var industry in LandingContent.Industries) text.AppendLine($"- {industry}");
        text.AppendLine().AppendLine("## Features").AppendLine();
        foreach (var feature in LandingContent.Features) text.AppendLine($"- {feature}");
        text.AppendLine()
            .AppendLine("## Pricing")
            .AppendLine()
            .AppendLine("- Starter: free for the first month, up to 20 employees and one office. No card required, no automatic charges.")
            .AppendLine("- Business: priced in Indian rupees for your team size; multiple offices, any number of employees, data migration and onboarding help.")
            .AppendLine()
            .AppendLine("## Questions and answers")
            .AppendLine();
        foreach (var faq in LandingContent.Faqs) text.AppendLine($"### {faq.Question}").AppendLine().AppendLine(faq.Answer).AppendLine();
        text.AppendLine("## Links").AppendLine()
            .AppendLine($"- [Home, features and pricing]({SiteUrl}/)")
            .AppendLine($"- [Start a free trial or book a demo]({SiteUrl}/#contact)")
            .AppendLine($"- [Privacy policy]({SiteUrl}/Home/Privacy)");
        if (!string.IsNullOrWhiteSpace(Company.Email)) text.AppendLine($"- Email: {Company.Email}");
        if (!string.IsNullOrWhiteSpace(Company.Phone)) text.AppendLine($"- Phone: {Company.Phone}");
        return Text(text.ToString());
    }

    private ContentResult Text(string body) => Content(body.ReplaceLineEndings("\n"), "text/plain; charset=utf-8");
}
