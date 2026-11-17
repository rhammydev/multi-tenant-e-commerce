using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities.Identity;

public class AccountToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public TokenPurpose Purpose { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? InvalidatedAtUtc { get; set; }
}
