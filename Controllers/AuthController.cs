using ECommerce.DTOs.Auth;
using ECommerce.Extensions;
using ECommerce.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) =>
        Respond(await auth.RegisterAsync(request, ClientIp, ct));

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        Respond(await auth.LoginAsync(request, ClientIp, ct));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken ct) =>
        Respond(await auth.RefreshAsync(request, ClientIp, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken ct) =>
        Respond(await auth.LogoutAsync(request, ct));

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct) =>
        Respond(await auth.VerifyEmailAsync(request, ct));

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(EmailRequest request, CancellationToken ct) =>
        Respond(await auth.ResendVerificationAsync(request, ct));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct) =>
        Respond(await auth.ForgotPasswordAsync(request, ct));

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct) =>
        Respond(await auth.ResetPasswordAsync(request, ct));

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct) =>
        Respond(await auth.ChangePasswordAsync(User.GetUserId(), request, ct));
}
