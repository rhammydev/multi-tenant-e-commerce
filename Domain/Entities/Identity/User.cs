namespace ECommerce.Domain.Entities.Identity;


public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }
    public DateTime? EmailVerifiedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }

    public int FailedLoginCount { get; set; }
    public DateTime? FirstFailedLoginAtUtc { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string FullName => $"{FirstName} {LastName}".Trim();
}
