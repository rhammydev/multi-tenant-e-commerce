namespace ECommerce.Domain.Enums;

public enum EmailStatus
{
    Queued = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    Cancelled = 5
}
