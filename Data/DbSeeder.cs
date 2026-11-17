using ECommerce.Domain.Entities.Identity;
using ECommerce.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Data;

public static class DbSeeder
{
    /// <summary>
    /// Creates the bootstrap platform administrator when Seed:PlatformAdminEmail / PlatformAdminPassword are configured.
    /// No credentials are hard-coded; without configuration nothing is created.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, SeedSettings settings, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(settings.PlatformAdminEmail) ||
            string.IsNullOrWhiteSpace(settings.PlatformAdminPassword))
        {
            logger.LogInformation("No platform admin seed configured; skipping.");
            return;
        }

        var email = settings.PlatformAdminEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        db.Users.Add(new User
        {
            FirstName = "Platform",
            LastName = "Administrator",
            Email = email,
            Phone = "+0000000000",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(settings.PlatformAdminPassword),
            IsEmailVerified = true,
            EmailVerifiedAtUtc = DateTime.UtcNow,
            IsPlatformAdmin = true
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded platform administrator {Email}", email);
    }
}
