namespace ECommerce.Extensions;

public static class ServiceExtensions
{
    /// Business services, settings and hosted worker
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }

    /// Repositories
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services;
    }
}
