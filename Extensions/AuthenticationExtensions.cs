using System.Security.Claims;
using System.Text;
using ECommerce.Domain.Settings;
using ECommerce.DTOs.Generic;
using ECommerce.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");
        var jwt = section.Get<JwtSettings>() ?? new JwtSettings();

        if (Encoding.UTF8.GetByteCount(jwt.Key) < 32 ||
            string.IsNullOrWhiteSpace(jwt.Issuer) ||
            string.IsNullOrWhiteSpace(jwt.Audience))
        {
            throw new InvalidOperationException(
                "JWT is not configured. Set Jwt:Key (at least 32 bytes), Jwt:Issuer and Jwt:Audience " +
                "(user-secrets locally, environment variables in deployment).");
        }

        services.Configure<JwtSettings>(section);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claim names as written in the token (NameIdentifier etc.), no legacy remapping
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var idClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!Guid.TryParse(idClaim, out var userId))
                        {
                            context.Fail("Token does not identify a user.");
                            return;
                        }

                        // Live check on every request: deactivated users lose access immediately,
                        // and role / verification state always comes from the database, never the token
                        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                        var state = await users.GetAuthStateAsync(userId, context.HttpContext.RequestAborted);
                        if (state is not { IsActive: true })
                        {
                            context.Fail("Account is inactive or no longer exists.");
                            return;
                        }

                        var identity = (ClaimsIdentity)context.Principal!.Identity!;
                        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
                            identity.RemoveClaim(claim);

                        if (state.IsPlatformAdmin)
                            identity.AddClaim(new Claim(ClaimTypes.Role, AuthPolicies.PlatformAdminRole));
                        if (state.IsEmailVerified)
                            identity.AddClaim(new Claim(AuthPolicies.EmailVerifiedClaim, "true"));
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
                            "Authentication is required. Provide a valid bearer token.",
                            401, ResponseCodes.Unauthorized));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
                            "You do not have permission to access this resource.",
                            403, ResponseCodes.Forbidden));
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.PlatformAdmin, p => p.RequireRole(AuthPolicies.PlatformAdminRole));
            options.AddPolicy(AuthPolicies.VerifiedUser, p => p
                .RequireAuthenticatedUser()
                .RequireClaim(AuthPolicies.EmailVerifiedClaim, "true"));
        });

        return services;
    }
}
