using System.Net;
using ECommerce.Domain.Enums;
using ECommerce.Tests.Infrastructure;

namespace ECommerce.Tests.Integration;

public class AuthRegistrationTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    private static object Valid(string email) => new
    {
        firstName = "Ada", lastName = "Okoro", email, phone = "+2348012345678", password = TestApp.Password
    };

    [Fact]
    public async Task Register_creates_an_unverified_user_and_queues_a_verification_email() // AUTH-01.3/.4
    {
        var result = await _app.Anonymous().Post("/auth/register", Valid("New.User@Shop.test"));

        Assert.Equal(HttpStatusCode.Created, result.Status);
        Assert.False(result.Data.GetProperty("isEmailVerified").GetBoolean());
        Assert.DoesNotContain("password", result.RawBody, StringComparison.OrdinalIgnoreCase);

        var user = _app.Db(db => db.Users.Single(u => u.Email == "new.user@shop.test"));
        Assert.NotEqual(TestApp.Password, user.PasswordHash);          // hashed, never plain
        Assert.StartsWith("$2", user.PasswordHash);                    // bcrypt

        var email = _app.Db(db => db.QueuedEmails.Single(e => e.ToEmail == "new.user@shop.test"));
        Assert.Equal(EmailStatus.Queued, email.Status);
        Assert.Contains("verify-email", email.Body);
    }

    [Fact]
    public async Task Duplicate_email_returns_409_regardless_of_case() // AUTH-01.1/.6
    {
        await _app.RegisterUser("dup@shop.test", verify: false);

        var result = await _app.Anonymous().Post("/auth/register", Valid("DUP@shop.test"));

        Assert.Equal(HttpStatusCode.Conflict, result.Status);
    }

    [Theory]
    [InlineData("", "Okoro", "a@b.test", "+2348012345678", "Password@123!")]
    [InlineData("Ada", "Okoro", "not-an-email", "+2348012345678", "Password@123!")]
    [InlineData("Ada", "Okoro", "a@b.test", "123", "Password@123!")]
    [InlineData("Ada", "Okoro", "a@b.test", "+2348012345678", "short")]
    [InlineData("Ada", "Okoro", "a@b.test", "+2348012345678", "alllowercase123!")]
    [InlineData("Ada", "Okoro", "a@b.test", "+2348012345678", "NoSymbolsHere123")]
    public async Task Invalid_input_returns_400_with_field_errors(string first, string last, string email, string phone, string password) // AUTH-01.2/.7
    {
        var result = await _app.Anonymous().Post("/auth/register",
            new { firstName = first, lastName = last, email, phone, password });

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.True(result.Body.GetProperty("errors").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Verify_email_works_once_and_rejects_bad_tokens() // AUTH-01.4
    {
        var email = await _app.RegisterUser(verify: false);
        var token = _app.EmailToken(email, "verify-email");

        var first = await _app.Anonymous().Post("/auth/verify-email", new { token });
        var second = await _app.Anonymous().Post("/auth/verify-email", new { token });
        var bogus = await _app.Anonymous().Post("/auth/verify-email", new { token = "NOTAREALTOKEN" });

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.BadRequest, second.Status);   // single use
        Assert.Equal(HttpStatusCode.BadRequest, bogus.Status);
        Assert.True(_app.Db(db => db.Users.Single(u => u.Email == email).IsEmailVerified));
    }

    [Fact]
    public async Task Resend_verification_supersedes_the_old_link_and_does_not_reveal_unknown_emails()
    {
        var email = await _app.RegisterUser(verify: false);
        var oldToken = _app.EmailToken(email, "verify-email");

        var resend = await _app.Anonymous().Post("/auth/resend-verification", new { email });
        var unknown = await _app.Anonymous().Post("/auth/resend-verification", new { email = "nobody@shop.test" });
        var newToken = _app.EmailToken(email, "verify-email");

        Assert.Equal(HttpStatusCode.OK, resend.Status);
        Assert.Equal(HttpStatusCode.OK, unknown.Status);
        Assert.Equal(resend.Message, unknown.Message);
        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _app.Anonymous().Post("/auth/verify-email", new { token = oldToken })).Status);
        Assert.Equal(HttpStatusCode.OK,
            (await _app.Anonymous().Post("/auth/verify-email", new { token = newToken })).Status);
    }

    [Fact]
    public async Task Unverified_users_are_blocked_from_protected_customer_operations() // AUTH-01.5
    {
        var email = await _app.RegisterUser(verify: false);
        var session = await _app.Login(email);
        var client = _app.As(session);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.Get("/_probe/verified")).Status);

        await _app.Anonymous().Post("/auth/verify-email", new { token = _app.EmailToken(email, "verify-email") });

        // Same token, no re-login: verification state is read live, so it takes effect immediately
        Assert.Equal(HttpStatusCode.OK, (await client.Get("/_probe/verified")).Status);
    }
}
