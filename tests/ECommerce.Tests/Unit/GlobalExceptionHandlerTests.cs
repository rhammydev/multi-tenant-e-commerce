using System.Text.Json;
using ECommerce.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Tests.Unit;

public class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, JsonElement Body)> Handle(Exception ex)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, ex, CancellationToken.None);

        Assert.True(handled);
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task Concurrency_conflict_returns_409()
    {
        var (status, body) = await Handle(new DbUpdateConcurrencyException());
        Assert.Equal(409, status);
        Assert.Equal("49", body.GetProperty("responseCode").GetString());
    }

    [Fact]
    public async Task Unknown_exception_returns_500_without_leaking_details()
    {
        var (status, body) = await Handle(new InvalidOperationException("secret connection string"));
        Assert.Equal(500, status);
        Assert.DoesNotContain("secret", body.ToString());
    }
}
