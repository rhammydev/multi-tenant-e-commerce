using ECommerce.Data;
using ECommerce.Domain.Entities.Common;
using ECommerce.Repositories.Interfaces;

namespace ECommerce.Repositories.Implementations;

public class AuditLogRepository(AppDbContext context) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry, CancellationToken ct = default) =>
        await context.AuditLogs.AddAsync(entry, ct);
}
