using ECommerce.Domain.Settings;
using ECommerce.Services.Implementations;
using ECommerce.Services.Interfaces;

namespace ECommerce.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthSettings>(configuration.GetSection("Auth"));
        services.Configure<SeedSettings>(configuration.GetSection("Seed"));

        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
