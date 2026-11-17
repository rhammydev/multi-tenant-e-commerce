using ECommerce.Domain.Entities.Common;

namespace ECommerce.Repositories.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);
}
