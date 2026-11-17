using ECommerce.DTOs.Auth;
using ECommerce.DTOs.Generic;

namespace ECommerce.Services.Interfaces;

public interface IAuthService
{
    Task<ApiResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default);
    Task<ApiResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);
    Task<ApiResponse> RefreshAsync(RefreshTokenRequest request, string? ip, CancellationToken ct = default);
    Task<ApiResponse> LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default);
    Task<ApiResponse> ResendVerificationAsync(EmailRequest request, CancellationToken ct = default);
    Task<ApiResponse> ForgotPasswordAsync(EmailRequest request, CancellationToken ct = default);
    Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<ApiResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}
