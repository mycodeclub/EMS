using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using EMS.Models;
using EMS.Models.Landing;
using EMS.Services.Common;

namespace EMS.Controllers;

public class HomeController(ICrudService<Enquiry> enquiries, ILogger<HomeController> logger) : Controller
{
    public const string EnquiryRateLimit = "enquiry";
    private const string EnquirySentKey = "EnquirySent";

    public IActionResult Index()
    {
        ViewData["EnquirySent"] = TempData[EnquirySentKey] as string;
        return View(new EnquiryInput());
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(EnquiryRateLimit)]
    public async Task<IActionResult> Enquiry(EnquiryInput input, CancellationToken ct)
    {
        // Honeypot filled: pretend success so bots learn nothing.
        if (!string.IsNullOrEmpty(input.Website))
        {
            logger.LogInformation("Enquiry honeypot triggered; submission dropped.");
            return ThankYou(input.Name);
        }

        if (ModelState.IsValid)
        {
            var result = await enquiries.CreateAsync(input.ToEntity(), ct);
            if (result.Succeeded)
            {
                logger.LogInformation("New {Interest} enquiry #{Id} from {Organization}", input.Interest, result.Data!.UniqueId, input.OrganizationName);
                return ThankYou(input.Name);
            }

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
        }

        return View(nameof(Index), input);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // Post/Redirect/Get so a refresh does not resubmit the form.
    private IActionResult ThankYou(string name)
    {
        TempData[EnquirySentKey] = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
        return Redirect(Url.Action(nameof(Index)) + "#contact");
    }
}
