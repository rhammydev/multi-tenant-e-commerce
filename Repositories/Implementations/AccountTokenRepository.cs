using ECommerce.Data;
using ECommerce.Domain.Entities.Identity;
using ECommerce.Domain.Enums;
using ECommerce.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Repositories.Implementations;

public class AccountTokenRepository(AppDbContext context) : IAccountTokenRepository
{
    public async Task AddAsync(AccountToken token, CancellationToken ct = default) =>
        await context.AccountTokens.AddAsync(token, ct);

    public Task<AccountToken?> GetUsableAsync(string tokenHash, TokenPurpose purpose, CancellationToken ct = default) =>
        context.AccountTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash
                                      && t.Purpose == purpose
                                      && t.UsedAtUtc == null
                                      && t.InvalidatedAtUtc == null
                                      && t.ExpiresAtUtc > DateTime.UtcNow, ct);

    public async Task InvalidateActiveAsync(Guid userId, TokenPurpose purpose, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tokens = await context.AccountTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose
                                           && t.UsedAtUtc == null && t.InvalidatedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.InvalidatedAtUtc = now;
    }
}
