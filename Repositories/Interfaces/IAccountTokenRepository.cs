using ECommerce.Domain.Entities.Identity;
using ECommerce.Domain.Enums;

namespace ECommerce.Repositories.Interfaces;

public interface IAccountTokenRepository
{
    Task AddAsync(AccountToken token, CancellationToken ct = default);
    Task<AccountToken?> GetUsableAsync(string tokenHash, TokenPurpose purpose, CancellationToken ct = default);
    Task InvalidateActiveAsync(Guid userId, TokenPurpose purpose, CancellationToken ct = default);
}
