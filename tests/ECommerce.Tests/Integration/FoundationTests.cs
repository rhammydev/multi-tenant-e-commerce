using System.Net;
using ECommerce.Tests.Infrastructure;

namespace ECommerce.Tests.Integration;

public class FoundationTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public FoundationTests(TestApp app) => _app = app;

    [Fact]
    public async Task Ping_returns_the_standard_envelope()
    {
        var result = await _app.Anonymous().Get("/system/ping");

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Equal("00", result.Body.GetProperty("responseCode").GetString());
        Assert.Equal("pong", result.Message);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _app.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var response = await _app.CreateClient().GetAsync("/api/v1/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
