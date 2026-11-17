using ECommerce.Data;
using ECommerce.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerce.Repositories.Implementations;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        context.Database.BeginTransactionAsync(ct);
}
