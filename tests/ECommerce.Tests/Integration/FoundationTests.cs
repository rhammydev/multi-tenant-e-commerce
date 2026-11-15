using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Tests.Infrastructure;

namespace ECommerce.Tests.Integration;

public class FoundationTests : IClassFixture<TestApp>
{
    private readonly HttpClient _client;

    public FoundationTests(TestApp app) => _client = app.CreateClient();

    [Fact]
    public async Task Ping_returns_the_standard_envelope()
    {
        var response = await _client.GetAsync("/api/v1/system/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("00", body.GetProperty("responseCode").GetString());
        Assert.Equal("pong", body.GetProperty("responseMessage").GetString());
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
