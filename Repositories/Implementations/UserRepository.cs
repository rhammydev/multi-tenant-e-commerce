using ECommerce.Data;
using ECommerce.Domain.Entities.Identity;
using ECommerce.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Repositories.Implementations;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await context.Users.AddAsync(user, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default) =>
        context.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public Task<UserAuthState?> GetAuthStateAsync(Guid id, CancellationToken ct = default) =>
        context.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserAuthState(u.IsActive, u.IsEmailVerified, u.IsPlatformAdmin))
            .FirstOrDefaultAsync(ct);
}
