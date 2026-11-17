using ECommerce.Domain.Entities.Common;

namespace ECommerce.Repositories.Interfaces;

public interface IEmailQueueRepository
{
    Task AddAsync(QueuedEmail email, CancellationToken ct = default);

    /// <summary>Cancels still-queued emails of a category for a user (a newer link supersedes them).</summary>
    Task CancelPendingAsync(Guid userId, string category, string reason, CancellationToken ct = default);
}
