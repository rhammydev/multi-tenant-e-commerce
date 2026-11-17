namespace ECommerce.Domain.Entities.Common;

public class AuditLog : BaseEntity
{
    public Guid? ActorId { get; set; }
    public Guid? TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string? Metadata { get; set; }
}
