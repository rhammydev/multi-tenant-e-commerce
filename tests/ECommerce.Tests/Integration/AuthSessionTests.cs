using System.Net;
using ECommerce.Tests.Infrastructure;

namespace ECommerce.Tests.Integration;

public class AuthSessionTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Login_returns_access_and_refresh_tokens_without_secrets() // AUTH-02.1/.4
    {
        var email = await _app.RegisterUser();

        var result = await _app.Anonymous().Post("/auth/login", new { email = email.ToUpperInvariant(), password = TestApp.Password });

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.False(string.IsNullOrEmpty(result.Data.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(result.Data.GetProperty("refreshToken").GetString()));
        Assert.DoesNotContain("passwordHash", result.RawBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wrong_password_unknown_email_and_inactive_account_get_the_same_generic_401() // AUTH-02.2/.3
    {
        var email = await _app.RegisterUser();
        var inactive = await _app.RegisterUser();
        _app.Db(db => { db.Users.Single(u => u.Email == inactive).IsActive = false; db.SaveChanges(); return 0; });

        var wrong = await _app.Anonymous().Post("/auth/login", new { email, password = "WrongPassword1!" });
        var unknown = await _app.Anonymous().Post("/auth/login", new { email = "ghost@shop.test", password = TestApp.Password });
        var disabled = await _app.Anonymous().Post("/auth/login", new { email = inactive, password = TestApp.Password });

        Assert.All(new[] { wrong, unknown, disabled }, r => Assert.Equal(HttpStatusCode.Unauthorized, r.Status));
        Assert.Equal(wrong.Message, unknown.Message);
        Assert.Equal(wrong.Message, disabled.Message);
    }

    [Fact]
    public async Task Five_failed_logins_lock_the_account_even_for_the_right_password()
    {
        var email = await _app.RegisterUser();
        for (var i = 0; i < 5; i++)
            await _app.Anonymous().Post("/auth/login", new { email, password = "WrongPassword1!" });

        var correct = await _app.Anonymous().Post("/auth/login", new { email, password = TestApp.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, correct.Status);
        Assert.Contains(_app.Db(db => db.AuditLogs.Select(a => a.Action).ToList()), a => a == "LoginBlocked");
    }

    [Fact]
    public async Task Failed_and_successful_logins_are_audited_without_secrets()
    {
        var email = await _app.RegisterUser();
        await _app.Anonymous().Post("/auth/login", new { email, password = "WrongPassword1!" });
        await _app.Login(email);

        var entries = _app.Db(db => db.AuditLogs.ToList());
        Assert.Contains(entries, a => a.Action == "LoginFailed");
        Assert.Contains(entries, a => a.Action == "LoginSucceeded");
        Assert.DoesNotContain(entries, a => (a.Metadata ?? "").Contains("WrongPassword1!"));
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reuse_of_the_old_one_revokes_everything() // AUTH-02.7
    {
        var email = await _app.RegisterUser();
        var session = await _app.Login(email);

        var rotated = await _app.Anonymous().Post("/auth/refresh", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotated.Status);
        var newRefresh = rotated.Data.GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(session.RefreshToken, newRefresh);

        // Replaying the old token = theft signal: rejected AND the new token is killed too
        var replay = await _app.Anonymous().Post("/auth/refresh", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.Status);

        var afterReplay = await _app.Anonymous().Post("/auth/refresh", new { refreshToken = newRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.Status);
        Assert.Contains(_app.Db(db => db.AuditLogs.Select(a => a.Action).ToList()), a => a == "RefreshTokenReuseDetected");
    }

    [Fact]
    public async Task Refresh_tokens_are_stored_hashed_only()
    {
        var email = await _app.RegisterUser();
        var session = await _app.Login(email);

        var stored = _app.Db(db => db.RefreshTokens.Select(t => t.TokenHash).ToList());
        Assert.DoesNotContain(session.RefreshToken, stored);
    }

    [Fact]
    public async Task Unknown_refresh_token_returns_401()
    {
        var result = await _app.Anonymous().Post("/auth/refresh", new { refreshToken = "deadbeef" });
        Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_is_idempotent()
    {
        var session = await _app.Login(await _app.RegisterUser());

        var first = await _app.Anonymous().Post("/auth/logout", new { refreshToken = session.RefreshToken });
        var second = await _app.Anonymous().Post("/auth/logout", new { refreshToken = session.RefreshToken });
        var refresh = await _app.Anonymous().Post("/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.Status);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_valid_token()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.Anonymous().Get("/users/me")).Status);

        var tampered = _app.As(new AuthSession("not.a.jwt", "x"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await tampered.Get("/users/me")).Status);
    }

    [Fact]
    public async Task A_deactivated_account_loses_access_with_its_existing_token_immediately() // STF-02.4 groundwork
    {
        var email = await _app.RegisterUser();
        var client = await _app.AsUser(email);
        Assert.Equal(HttpStatusCode.OK, (await client.Get("/users/me")).Status);

        _app.Db(db => { db.Users.Single(u => u.Email == email).IsActive = false; db.SaveChanges(); return 0; });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.Get("/users/me")).Status);
    }

    [Fact]
    public async Task Platform_admin_policy_is_separate_from_ordinary_users() // AUTH-03.5/.6
    {
        var customer = await _app.AsUser(await _app.RegisterUser());
        var admin = await _app.AsPlatformAdmin();

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.Get("/_probe/platform-admin")).Status);
        Assert.Equal(HttpStatusCode.OK, (await admin.Get("/_probe/platform-admin")).Status);
    }
}
