using ECommerce.Domain.Entities.Common;
using ECommerce.Domain.Entities.Identity;
using ECommerce.DTOs.Auth;
using ECommerce.DTOs.Generic;
using ECommerce.Repositories.Interfaces;
using ECommerce.Services.Interfaces;

namespace ECommerce.Services.Implementations;

public class UserService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAuditLogRepository audit,
    IUnitOfWork unitOfWork) : IUserService
{
    public async Task<ApiResponse> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct);
        return user is null
            ? ApiResponse.NotFound("User not found.")
            : ApiResponse.Success("Profile retrieved successfully.", AuthService.ToResponse(user));
    }

    public async Task<ApiResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null) return ApiResponse.NotFound("User not found.");

        if (request.FirstName != null) user.FirstName = request.FirstName.Trim();
        if (request.LastName != null) user.LastName = request.LastName.Trim();
        if (request.Phone != null) user.Phone = request.Phone.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(ct);
        return ApiResponse.Success("Profile updated successfully.", AuthService.ToResponse(user));
    }

    public async Task<ApiResponse> DeactivateAsync(Guid userId, DeactivateAccountRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null) return ApiResponse.NotFound("User not found.");

        if (user.IsPlatformAdmin)
            return ApiResponse.Forbidden("Platform administrator accounts cannot be self-deactivated.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return ApiResponse.Fail("Password is incorrect.", 400);

        // TODO: refuse while the user is the last active owner of any tenant 
        var now = DateTime.UtcNow;
        user.IsActive = false;
        user.UpdatedAtUtc = now;

        await refreshTokens.RevokeAllForUserAsync(user.Id, now, ct);
        await audit.AddAsync(new AuditLog
        {
            ActorId = user.Id,
            Action = "AccountDeactivated",
            EntityType = nameof(User),
            EntityId = user.Id.ToString()
        }, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ApiResponse.Success("Account deactivated.");
    }
}
