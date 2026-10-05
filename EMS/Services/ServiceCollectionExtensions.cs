using EMS.Services.Common;
using EMS.Services.Email;
using EMS.Services.Import;
using EMS.Services.Leave;
using EMS.Services.Onboarding;
using EMS.Services.People;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace EMS.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmsServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped(typeof(ICrudService<>), typeof(CrudService<>));
        services.AddSingleton<EmailTemplates>();
        services.AddTransient<IEmailService, SmtpEmailService>();
        services.AddTransient<IEmailSender, IdentityEmailSender>(); // Identity's built-in pages (forgot password, ...)
        services.AddScoped<OrganizationContext>();
        services.AddScoped<SetupService>();
        services.AddScoped<TrialService>();
        services.AddScoped<EmployeeImporter>();
        services.AddScoped<AttendanceImporter>();
        services.AddScoped<LeaveService>();
        services.AddScoped<ResignationService>();
        services.AddSingleton<PhotoStore>();
        services.AddSingleton<DocumentStore>();
        services.AddScoped<OnboardingChecklist>();
        services.AddScoped<EmployeeLoginService>();
        return services;
    }
}
