namespace ECommerce.DTOs.Auth;

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
}

public class DeactivateAccountRequest
{
    public string Password { get; set; } = string.Empty;
}
