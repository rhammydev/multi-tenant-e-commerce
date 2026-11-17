using ECommerce.Domain.Entities.Identity;

namespace ECommerce.Services.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user);
}
