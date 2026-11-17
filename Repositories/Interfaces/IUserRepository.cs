using ECommerce.Domain.Entities.Identity;

namespace ECommerce.Repositories.Interfaces;

public record UserAuthState(bool IsActive, bool IsEmailVerified, bool IsPlatformAdmin);

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);

    /// <summary>Lightweight live lookup used on every authenticated request. Null if the user no longer exists.</summary>
    Task<UserAuthState?> GetAuthStateAsync(Guid id, CancellationToken ct = default);
}
