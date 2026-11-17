using ECommerce.Domain.Entities.Identity;

namespace ECommerce.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);
}
