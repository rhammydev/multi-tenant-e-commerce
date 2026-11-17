using ECommerce.DTOs.Auth;
using ECommerce.DTOs.Generic;

namespace ECommerce.Services.Interfaces;

public interface IUserService
{
    Task<ApiResponse> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<ApiResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task<ApiResponse> DeactivateAsync(Guid userId, DeactivateAccountRequest request, CancellationToken ct = default);
}
