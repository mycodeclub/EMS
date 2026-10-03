using System.Security.Claims;
using EMS.Services.Auth;

namespace EMS.Services.Common;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public const string SystemUser = "system";

    /// <summary>The signed-in user; while a super admin views a customer's panel, the super admin (so audit columns show who acted).</summary>
    public string UserId =>
        httpContextAccessor.HttpContext?.User is { } user
            ? user.FindFirstValue(Impersonation.ImpersonatorIdClaim) ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? SystemUser
            : SystemUser;
}
