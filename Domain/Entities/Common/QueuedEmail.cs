using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities.Common;

public class QueuedEmail : BaseEntity
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public EmailStatus Status { get; set; } = EmailStatus.Queued;
    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? Category { get; set; }
    public Guid? UserId { get; set; }
}
