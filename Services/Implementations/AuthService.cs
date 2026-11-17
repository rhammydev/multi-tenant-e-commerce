using ECommerce.Domain.Entities.Common;
using ECommerce.Domain.Entities.Identity;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Settings;
using ECommerce.DTOs.Auth;
using ECommerce.DTOs.Generic;
using ECommerce.Helpers;
using ECommerce.Repositories.Interfaces;
using ECommerce.Services.Interfaces;
using ECommerce.Utilities;
using Microsoft.Extensions.Options;

namespace ECommerce.Services.Implementations;

public class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAccountTokenRepository accountTokens,
    IAuditLogRepository audit,
    IEmailQueueRepository emailQueue,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IOptions<JwtSettings> jwtOptions,
    IOptions<AuthSettings> authOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidLoginMessage = "Invalid email or password.";
    private const string VerifyCategory = "EmailVerification";
    private const string ResetCategory = "PasswordReset";

    private readonly JwtSettings _jwt = jwtOptions.Value;
    private readonly AuthSettings _auth = authOptions.Value;

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // ---- AUTH-01 ----

    public async Task<ApiResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        if (await users.EmailExistsAsync(email, ct))
            return ApiResponse.Conflict("An account with this email already exists.");

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            Phone = request.Phone.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };
        await users.AddAsync(user, ct);

        await QueueVerificationEmailAsync(user, ct);
        await AuditAsync(user.Id, "UserRegistered", user.Id.ToString(), ip, ct: ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} registered", user.Id);
        return ApiResponse.Success("Registration successful. Check your email to verify your account.",
            ToResponse(user), 201);
    }

    public async Task<ApiResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
    {
        var token = await accountTokens.GetUsableAsync(TokenHelper.Hash(request.Token), TokenPurpose.EmailVerification, ct);
        if (token?.User is null)
            return ApiResponse.Fail("The verification link is invalid or has expired.", 400);

        var now = DateTime.UtcNow;
        token.UsedAtUtc = now;
        token.User.IsEmailVerified = true;
        token.User.EmailVerifiedAtUtc = now;
        token.User.UpdatedAtUtc = now;

        await AuditAsync(token.User.Id, "EmailVerified", token.User.Id.ToString(), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ApiResponse.Success("Email verified. You now have full access to your account.");
    }

    public async Task<ApiResponse> ResendVerificationAsync(EmailRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(Normalize(request.Email), ct);
        if (user is { IsActive: true, IsEmailVerified: false })
        {
            await QueueVerificationEmailAsync(user, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return ApiResponse.Success("If the account exists and is unverified, a new verification email has been sent.");
    }

    // ---- AUTH-02 ----

    public async Task<ApiResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var user = await users.GetByEmailAsync(Normalize(request.Email), ct);

        if (user is null)
        {
            await AuditAsync(null, "LoginFailed", null, ip, "Unknown email", ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Unauthorized();
        }

        if (user.LockoutEndUtc > now)
        {
            await AuditAsync(user.Id, "LoginBlocked", user.Id.ToString(), ip, "Account locked", ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Unauthorized();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            RegisterFailedLogin(user, now);
            await AuditAsync(user.Id, "LoginFailed", user.Id.ToString(), ip,
                user.LockoutEndUtc > now ? "Wrong password; account locked" : "Wrong password", ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Unauthorized();
        }

        if (!user.IsActive)
        {
            await AuditAsync(user.Id, "LoginBlocked", user.Id.ToString(), ip, "Account inactive", ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Unauthorized();
        }

        user.FailedLoginCount = 0;
        user.FirstFailedLoginAtUtc = null;
        user.LockoutEndUtc = null;

        var response = await IssueSessionAsync(user, ip, ct);
        await AuditAsync(user.Id, "LoginSucceeded", user.Id.ToString(), ip, ct: ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ApiResponse.Success("Login successful.", response);
    }

    public async Task<ApiResponse> RefreshAsync(RefreshTokenRequest request, string? ip, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var stored = await refreshTokens.GetByHashAsync(TokenHelper.Hash(request.RefreshToken), ct);

        if (stored?.User is null)
            return Unauthorized("Invalid refresh token.");

        // A rotated/revoked token showing up again means it was copied: kill every session of this user
        if (stored.RevokedAtUtc != null)
        {
            await refreshTokens.RevokeAllForUserAsync(stored.UserId, now, ct);
            await AuditAsync(stored.UserId, "RefreshTokenReuseDetected", stored.UserId.ToString(), ip, ct: ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Unauthorized("Invalid refresh token.");
        }

        if (stored.ExpiresAtUtc <= now || !stored.User.IsActive)
            return Unauthorized("Invalid refresh token.");

        var response = await IssueSessionAsync(stored.User, ip, ct, rotating: stored);
        await unitOfWork.SaveChangesAsync(ct);

        return ApiResponse.Success("Token refreshed.", response);
    }

    public async Task<ApiResponse> LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var stored = await refreshTokens.GetByHashAsync(TokenHelper.Hash(request.RefreshToken), ct);
        if (stored is { RevokedAtUtc: null })
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await AuditAsync(stored.UserId, "Logout", stored.UserId.ToString(), ct: ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        // Idempotent: logging out an unknown or already-revoked token is not an error
        return ApiResponse.Success("Logged out.");
    }

    // ---- AUTH-04 ----

    public async Task<ApiResponse> ForgotPasswordAsync(EmailRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(Normalize(request.Email), ct);

        if (user is { IsActive: true })
        {
            var token = await IssueAccountTokenAsync(user, TokenPurpose.PasswordReset,
                TimeSpan.FromMinutes(_auth.ResetTokenExpiryMinutes), ResetCategory, ct);

            var link = $"{_auth.ClientBaseUrl}/reset-password?token={token}";
            await emailQueue.AddAsync(new QueuedEmail
            {
                ToEmail = user.Email,
                Subject = "Reset your password",
                Body = MailUtils.PasswordReset(user.FirstName, link, token, _auth.ResetTokenExpiryMinutes),
                Category = ResetCategory,
                UserId = user.Id
            }, ct);
            await AuditAsync(user.Id, "PasswordResetRequested", user.Id.ToString(), ct: ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return ApiResponse.Success("If the email is registered, a password reset link has been sent.");
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var token = await accountTokens.GetUsableAsync(TokenHelper.Hash(request.Token), TokenPurpose.PasswordReset, ct);
        if (token?.User is not { IsActive: true } user)
            return ApiResponse.Fail("The reset link is invalid or has expired.", 400);

        var now = DateTime.UtcNow;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.FailedLoginCount = 0;
        user.FirstFailedLoginAtUtc = null;
        user.LockoutEndUtc = null;
        user.UpdatedAtUtc = now;
        token.UsedAtUtc = now;

        // A password reset must end every existing session
        await refreshTokens.RevokeAllForUserAsync(user.Id, now, ct);
        await AuditAsync(user.Id, "PasswordReset", user.Id.ToString(), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ApiResponse.Success("Password has been reset. You can now log in.");
    }

    public async Task<ApiResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null) return ApiResponse.NotFound("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return ApiResponse.Fail("Current password is incorrect.", 400);

        var now = DateTime.UtcNow;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = now;

        await refreshTokens.RevokeAllForUserAsync(user.Id, now, ct);
        await AuditAsync(user.Id, "PasswordChanged", user.Id.ToString(), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ApiResponse.Success("Password changed. Please log in again on your other devices.");
    }

    // ---- helpers ----

    private static ApiResponse Unauthorized(string message = InvalidLoginMessage) =>
        ApiResponse.Fail(message, 401, ResponseCodes.Unauthorized);

    private async Task<AuthResponse> IssueSessionAsync(User user, string? ip, CancellationToken ct,
        RefreshToken? rotating = null)
    {
        var now = DateTime.UtcNow;
        var (accessToken, accessExpires) = tokenService.CreateAccessToken(user);

        var rawRefresh = TokenHelper.GenerateToken();
        var refreshHash = TokenHelper.Hash(rawRefresh);
        var refreshExpires = now.AddDays(_jwt.RefreshTokenDays);

        await refreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAtUtc = refreshExpires,
            CreatedByIp = ip
        }, ct);

        if (rotating is not null)
        {
            rotating.RevokedAtUtc = now;
            rotating.ReplacedByTokenHash = refreshHash;
        }

        return new AuthResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessExpires,
            RefreshToken = rawRefresh,
            RefreshTokenExpiresAtUtc = refreshExpires,
            User = ToResponse(user)
        };
    }

    private async Task QueueVerificationEmailAsync(User user, CancellationToken ct)
    {
        var token = await IssueAccountTokenAsync(user, TokenPurpose.EmailVerification,
            TimeSpan.FromHours(_auth.VerificationTokenExpiryHours), VerifyCategory, ct);

        var link = $"{_auth.ClientBaseUrl}/verify-email?token={token}";
        await emailQueue.AddAsync(new QueuedEmail
        {
            ToEmail = user.Email,
            Subject = "Verify your email",
            Body = MailUtils.VerifyEmail(user.FirstName, link, token, _auth.VerificationTokenExpiryHours),
            Category = VerifyCategory,
            UserId = user.Id
        }, ct);
    }

    private async Task<string> IssueAccountTokenAsync(User user, TokenPurpose purpose, TimeSpan lifetime,
        string category, CancellationToken ct)
    {
        // A new link kills the previous one, and an older email still waiting in the queue must not go out
        await accountTokens.InvalidateActiveAsync(user.Id, purpose, ct);
        await emailQueue.CancelPendingAsync(user.Id, category,
            "Cancelled: a newer link was issued before this email was sent.", ct);

        var raw = TokenHelper.GenerateToken();
        await accountTokens.AddAsync(new AccountToken
        {
            UserId = user.Id,
            TokenHash = TokenHelper.Hash(raw),
            Purpose = purpose,
            ExpiresAtUtc = DateTime.UtcNow.Add(lifetime)
        }, ct);

        return raw;
    }

    private void RegisterFailedLogin(User user, DateTime now)
    {
        var windowStart = now.AddMinutes(-_auth.FailedLoginWindowMinutes);

        if (user.FirstFailedLoginAtUtc == null || user.FirstFailedLoginAtUtc < windowStart)
        {
            user.FirstFailedLoginAtUtc = now;
            user.FailedLoginCount = 1;
        }
        else
        {
            user.FailedLoginCount++;
        }

        if (user.FailedLoginCount >= _auth.MaxFailedLogins)
        {
            user.LockoutEndUtc = now.AddMinutes(_auth.LockoutMinutes);
            user.FailedLoginCount = 0;
            user.FirstFailedLoginAtUtc = null;
        }
    }

    private Task AuditAsync(Guid? actorId, string action, string? entityId, string? ip = null,
        string? details = null, CancellationToken ct = default) =>
        audit.AddAsync(new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityType = nameof(User),
            EntityId = entityId,
            IpAddress = ip,
            Metadata = details
        }, ct);

    internal static UserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Phone = user.Phone,
        IsEmailVerified = user.IsEmailVerified,
        IsPlatformAdmin = user.IsPlatformAdmin,
        CreatedAtUtc = user.CreatedAtUtc
    };
}
