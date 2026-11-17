using ECommerce.Data;
using ECommerce.Domain.Entities.Common;
using ECommerce.Domain.Enums;
using ECommerce.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Repositories.Implementations;

public class EmailQueueRepository(AppDbContext context) : IEmailQueueRepository
{
    public async Task AddAsync(QueuedEmail email, CancellationToken ct = default) =>
        await context.QueuedEmails.AddAsync(email, ct);

    public async Task CancelPendingAsync(Guid userId, string category, string reason, CancellationToken ct = default)
    {
        var pending = await context.QueuedEmails
            .Where(e => e.UserId == userId && e.Category == category && e.Status == EmailStatus.Queued)
            .ToListAsync(ct);

        foreach (var email in pending)
        {
            email.Status = EmailStatus.Cancelled;
            email.LastError = reason;
            email.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
