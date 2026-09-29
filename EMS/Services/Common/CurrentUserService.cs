using System.Security.Claims;

namespace EMS.Services.Common;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public const string SystemUser = "system";

    public string UserId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? SystemUser;
}
