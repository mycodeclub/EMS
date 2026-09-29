using EMS.Services.Common;

namespace EMS.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmsServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped(typeof(ICrudService<>), typeof(CrudService<>));
        return services;
    }
}
