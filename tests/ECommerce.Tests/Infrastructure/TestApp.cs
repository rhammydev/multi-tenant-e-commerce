using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ECommerce.Data;
using ECommerce.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ECommerce.Tests.Infrastructure;

/// <summary>
/// The real API (routing, JWT, validation, services, repositories) hosted in memory.
/// Uses EF InMemory for now; persistence and concurrency tests must move to real SQL Server (SRS 13.2)
/// once Docker is available. Each instance gets its own database.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ecommerce-tests-{Guid.NewGuid():N}";

    public const string Password = "Password@123!";
    public const string AdminEmail = "admin@platform.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing": no user-secrets, so no real credentials are ever loaded
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "not-used");
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-that-is-long-enough-123");
        builder.UseSetting("Jwt:Issuer", "ecommerce-tests");
        builder.UseSetting("Jwt:Audience", "ecommerce-tests");
        builder.UseSetting("Auth:ClientBaseUrl", "https://client.test");
        builder.UseSetting("Seed:PlatformAdminEmail", AdminEmail);
        builder.UseSetting("Seed:PlatformAdminPassword", Password);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // Probe endpoints that exist only in the test assembly, to exercise authorization policies
            services.AddControllers().AddApplicationPart(typeof(TestApp).Assembly);
        });
    }

    public ApiClient Anonymous() => new(CreateClient());

    /// <summary>Registers a user and (optionally) verifies the email. Returns the user's email.</summary>
    public async Task<string> RegisterUser(string? email = null, bool verify = true)
    {
        email ??= $"user-{Guid.NewGuid():N}@shop.test";
        var result = await Anonymous().Post("/auth/register", new
        {
            firstName = "Ada", lastName = "Okoro", email, phone = "+2348012345678", password = Password
        });
        Assert.Equal(HttpStatusCode.Created, result.Status);

        if (verify)
        {
            var verified = await Anonymous().Post("/auth/verify-email", new { token = EmailToken(email, "verify-email") });
            Assert.Equal(HttpStatusCode.OK, verified.Status);
        }
        return email;
    }

    public async Task<AuthSession> Login(string email, string password = Password)
    {
        var result = await Anonymous().Post("/auth/login", new { email, password });
        Assert.True(result.Code == 200, $"Login failed for {email}: {result.Code} {result.Message}");
        return new AuthSession(
            result.Data.GetProperty("accessToken").GetString()!,
            result.Data.GetProperty("refreshToken").GetString()!);
    }

    public ApiClient As(AuthSession session)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new ApiClient(client);
    }

    public async Task<ApiClient> AsUser(string email) => As(await Login(email));
    public async Task<ApiClient> AsPlatformAdmin() => As(await Login(AdminEmail));

    /// <summary>The raw token from the newest queued email to this address (as the user would read it from their inbox).</summary>
    public string EmailToken(string email, string pathHint) =>
        Db(db =>
        {
            var body = db.QueuedEmails
                .Where(e => e.ToEmail == email.ToLowerInvariant() && e.Status == EmailStatus.Queued)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Select(e => e.Body)
                .First();
            Assert.Contains(pathHint, body);
            return Regex.Match(body, @"token=([A-F0-9]+)").Groups[1].Value;
        });

    /// <summary>Read the database directly (to check stored state).</summary>
    public T Db<T>(Func<AppDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

public sealed record AuthSession(string AccessToken, string RefreshToken);

public sealed class ApiClient(HttpClient http)
{
    public Task<ApiResult> Get(string path) => Send(HttpMethod.Get, path, null);
    public Task<ApiResult> Post(string path, object? body = null) => Send(HttpMethod.Post, path, body);
    public Task<ApiResult> Put(string path, object body) => Send(HttpMethod.Put, path, body);
    public Task<ApiResult> Patch(string path, object body) => Send(HttpMethod.Patch, path, body);
    public Task<ApiResult> Delete(string path) => Send(HttpMethod.Delete, path, null);

    private async Task<ApiResult> Send(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, "/api/v1" + path);
        if (body != null)
            request.Content = JsonContent.Create(body);
        return await ApiResult.From(await http.SendAsync(request));
    }
}

public sealed record ApiResult(HttpStatusCode Status, JsonElement Body, string RawBody)
{
    public int Code => (int)Status;
    public JsonElement Data => Body.GetProperty("data");
    public string Message => Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("responseMessage", out var m)
        ? m.GetString() ?? string.Empty
        : string.Empty;

    public static async Task<ApiResult> From(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        var body = response.Content.Headers.ContentType?.MediaType == "application/json" && raw.Length > 0
            ? JsonSerializer.Deserialize<JsonElement>(raw)
            : default;
        return new ApiResult(response.StatusCode, body, raw);
    }
}
