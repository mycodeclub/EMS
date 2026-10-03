using EMS.Models.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EMS.Data;

/// <summary>Creates the roles and the super admin account on startup. Safe to run on every start.</summary>
public static class IdentitySeeder
{
    public static async Task SeedIdentityAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentitySeeder));
        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = provider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = provider.GetRequiredService<IOptions<SuperAdminOptions>>().Value;

        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                EnsureSucceeded(await roles.CreateAsync(new IdentityRole(role)), $"create the {role} role");
        }

        if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
        {
            logger.LogWarning("No super admin configured: set SuperAdmin:Email and SuperAdmin:Password.");
            return;
        }

        // An existing account keeps its password, so a password changed after the first start is not reset.
        var user = await users.FindByEmailAsync(admin.Email);
        if (user is null)
        {
            user = new IdentityUser { UserName = admin.Email, Email = admin.Email, EmailConfirmed = true };
            EnsureSucceeded(await users.CreateAsync(user, admin.Password), $"create super admin {admin.Email}");
            logger.LogInformation("Super admin {Email} created.", admin.Email);
        }

        if (!await users.IsInRoleAsync(user, AppRoles.SuperAdmin))
            EnsureSucceeded(await users.AddToRoleAsync(user, AppRoles.SuperAdmin), $"add {admin.Email} to the SuperAdmin role");
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not {action}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
    }
}
