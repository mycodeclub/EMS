using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace EMS.Services.Auth;

/// <summary>
/// "Sign in as customer" for the super admin. The customer's owner is signed in with a separate cookie that the browser
/// sends only to /Org, so the super admin stays signed in to the admin console in the other tab. Requests to /Org use
/// that cookie while it exists; everything else uses the normal Identity cookie.
/// </summary>
public static class Impersonation
{
    public const string Scheme = "Impersonation";

    /// <summary>Picks the impersonation cookie or the Identity cookie per request.</summary>
    public const string SelectorScheme = "EmsAuth";

    public const string CookieName = ".EMS.Impersonation";
    public const string PathPrefix = "/Org";

    /// <summary>Claims on the impersonated principal: who is really signed in.</summary>
    public const string ImpersonatorIdClaim = "ems:impersonator_id";
    public const string ImpersonatorNameClaim = "ems:impersonator";

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    public static AuthenticationBuilder AddImpersonation(this IServiceCollection services) =>
        services.AddAuthentication(options =>
            {
                // Overrides AddDefaultIdentity's default (the Identity cookie); the selector forwards to it outside /Org.
                options.DefaultScheme = SelectorScheme;
            })
            .AddPolicyScheme(SelectorScheme, null, options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Path.StartsWithSegments(PathPrefix) && context.Request.Cookies.ContainsKey(CookieName)
                        ? Scheme
                        : IdentityConstants.ApplicationScheme;
            })
            .AddCookie(Scheme, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.Path = PathPrefix;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = Lifetime;
                options.SlidingExpiration = false;
                options.LoginPath = "/Identity/Account/Login";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            });

    public static bool IsImpersonated(this ClaimsPrincipal user) => user.HasClaim(c => c.Type == ImpersonatorIdClaim);

    /// <summary>Signs in <paramref name="owner"/> with the impersonation cookie, recording <paramref name="admin"/> as the real user.</summary>
    public static async Task SignInAsync(HttpContext http, SignInManager<IdentityUser> signIn, IdentityUser owner, ClaimsPrincipal admin)
    {
        var principal = await signIn.CreateUserPrincipalAsync(owner);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(ImpersonatorIdClaim, admin.FindFirstValue(ClaimTypes.NameIdentifier)!));
        identity.AddClaim(new Claim(ImpersonatorNameClaim, admin.Identity?.Name ?? ""));

        await http.SignInAsync(Scheme, principal, new AuthenticationProperties
        {
            // Persistent so the browser drops the cookie itself when the session ends.
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(Lifetime),
        });
    }

    public static Task SignOutAsync(HttpContext http) => http.SignOutAsync(Scheme);
}
