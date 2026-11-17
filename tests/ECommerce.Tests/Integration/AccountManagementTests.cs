using System.Net;
using ECommerce.Tests.Infrastructure;

namespace ECommerce.Tests.Integration;

public class AccountManagementTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    private const string NewPassword = "Brand-New#Pass42";

    [Fact]
    public async Task Forgot_password_gives_the_same_answer_for_known_and_unknown_emails()
    {
        var email = await _app.RegisterUser();

        var known = await _app.Anonymous().Post("/auth/forgot-password", new { email });
        var unknown = await _app.Anonymous().Post("/auth/forgot-password", new { email = "ghost@shop.test" });

        Assert.Equal(HttpStatusCode.OK, known.Status);
        Assert.Equal(HttpStatusCode.OK, unknown.Status);
        Assert.Equal(known.Message, unknown.Message);
        Assert.Empty(_app.Db(db => db.QueuedEmails.Where(e => e.ToEmail == "ghost@shop.test").ToList()));
    }

    [Fact]
    public async Task Reset_password_changes_the_password_ends_all_sessions_and_is_single_use()
    {
        var email = await _app.RegisterUser();
        var session = await _app.Login(email);
        await _app.Anonymous().Post("/auth/forgot-password", new { email });
        var token = _app.EmailToken(email, "reset-password");

        var reset = await _app.Anonymous().Post("/auth/reset-password",
            new { token, newPassword = NewPassword, confirmNewPassword = NewPassword });
        var reuse = await _app.Anonymous().Post("/auth/reset-password",
            new { token, newPassword = NewPassword, confirmNewPassword = NewPassword });

        Assert.Equal(HttpStatusCode.OK, reset.Status);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.Status);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _app.Anonymous().Post("/auth/login", new { email, password = TestApp.Password })).Status);
        Assert.Equal(HttpStatusCode.OK,
            (await _app.Anonymous().Post("/auth/login", new { email, password = NewPassword })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _app.Anonymous().Post("/auth/refresh", new { refreshToken = session.RefreshToken })).Status);
    }

    [Fact]
    public async Task Reset_password_rejects_weak_passwords_mismatches_and_bad_tokens()
    {
        var email = await _app.RegisterUser();
        await _app.Anonymous().Post("/auth/forgot-password", new { email });
        var token = _app.EmailToken(email, "reset-password");

        var weak = await _app.Anonymous().Post("/auth/reset-password",
            new { token, newPassword = "weak", confirmNewPassword = "weak" });
        var mismatch = await _app.Anonymous().Post("/auth/reset-password",
            new { token, newPassword = NewPassword, confirmNewPassword = "Different#Pass42" });
        var bogus = await _app.Anonymous().Post("/auth/reset-password",
            new { token = "NOPE", newPassword = NewPassword, confirmNewPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, weak.Status);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.Status);
        Assert.Equal(HttpStatusCode.BadRequest, bogus.Status);
    }

    [Fact]
    public async Task A_newer_reset_link_kills_the_older_one_and_cancels_its_queued_email()
    {
        var email = await _app.RegisterUser();
        await _app.Anonymous().Post("/auth/forgot-password", new { email });
        var oldToken = _app.EmailToken(email, "reset-password");
        await _app.Anonymous().Post("/auth/forgot-password", new { email });

        var result = await _app.Anonymous().Post("/auth/reset-password",
            new { token = oldToken, newPassword = NewPassword, confirmNewPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.Single(_app.Db(db => db.QueuedEmails
            .Where(e => e.ToEmail == email && e.Category == "PasswordReset" && e.Status == ECommerce.Domain.Enums.EmailStatus.Queued)
            .ToList()));
    }

    [Fact]
    public async Task Change_password_requires_the_current_password_and_ends_other_sessions()
    {
        var email = await _app.RegisterUser();
        var session = await _app.Login(email);
        var client = _app.As(session);

        var wrong = await client.Post("/auth/change-password",
            new { currentPassword = "WrongPassword1!", newPassword = NewPassword, confirmNewPassword = NewPassword });
        var ok = await client.Post("/auth/change-password",
            new { currentPassword = TestApp.Password, newPassword = NewPassword, confirmNewPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.Status);
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _app.Anonymous().Post("/auth/refresh", new { refreshToken = session.RefreshToken })).Status);
        Assert.Equal(HttpStatusCode.OK,
            (await _app.Anonymous().Post("/auth/login", new { email, password = NewPassword })).Status);
    }

    [Fact]
    public async Task Change_password_requires_authentication()
    {
        var result = await _app.Anonymous().Post("/auth/change-password",
            new { currentPassword = TestApp.Password, newPassword = NewPassword, confirmNewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
    }

    [Fact]
    public async Task Get_and_patch_profile_only_touch_permitted_fields()
    {
        var email = await _app.RegisterUser();
        var client = await _app.AsUser(email);

        var me = await client.Get("/users/me");
        Assert.Equal(email, me.Data.GetProperty("email").GetString());
        Assert.DoesNotContain("passwordHash", me.RawBody, StringComparison.OrdinalIgnoreCase);

        // Mass-assignment attempt: email / admin flag in the body must be ignored
        var patch = await client.Patch("/users/me", new
        {
            firstName = "Chidi", phone = "+2348099999999",
            email = "hijack@shop.test", isPlatformAdmin = true, isEmailVerified = false
        });

        Assert.Equal(HttpStatusCode.OK, patch.Status);
        Assert.Equal("Chidi", patch.Data.GetProperty("firstName").GetString());
        Assert.Equal("Okoro", patch.Data.GetProperty("lastName").GetString());     // untouched
        Assert.Equal(email, patch.Data.GetProperty("email").GetString());
        Assert.False(patch.Data.GetProperty("isPlatformAdmin").GetBoolean());
        Assert.True(patch.Data.GetProperty("isEmailVerified").GetBoolean());
    }

    [Fact]
    public async Task Patch_profile_validates_input()
    {
        var client = await _app.AsUser(await _app.RegisterUser());

        var result = await client.Patch("/users/me", new { phone = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
    }

    [Fact]
    public async Task Deactivate_requires_the_password_and_then_locks_the_user_out()
    {
        var email = await _app.RegisterUser();
        var session = await _app.Login(email);
        var client = _app.As(session);

        var wrong = await client.Post("/users/me/deactivate", new { password = "WrongPassword1!" });
        var ok = await client.Post("/users/me/deactivate", new { password = TestApp.Password });

        Assert.Equal(HttpStatusCode.BadRequest, wrong.Status);
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.Get("/users/me")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _app.Anonymous().Post("/auth/login", new { email, password = TestApp.Password })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _app.Anonymous().Post("/auth/refresh", new { refreshToken = session.RefreshToken })).Status);
    }

    [Fact]
    public async Task Platform_admin_cannot_self_deactivate()
    {
        var admin = await _app.AsPlatformAdmin();

        var result = await admin.Post("/users/me/deactivate", new { password = TestApp.Password });

        Assert.Equal(HttpStatusCode.Forbidden, result.Status);
    }
}
