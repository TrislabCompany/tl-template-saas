using Integration.Tests.Infrastructure;

namespace Integration.Tests.Tests.Health;

/// <summary>The endpoints the platform relies on: its probes, and the info the front ends' /api proxy reaches.</summary>
[Collection(FshCollectionDefinition.Name)]
public sealed class PlatformProbesTests
{
    private readonly FshWebApplicationFactory _factory;

    public PlatformProbesTests(FshWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    public async Task Probe_Should_Return200_WithoutAuthentication(string path)
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Info_Should_Return_Name_And_Environment_WithoutAuthentication()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health/info");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"name\":\"App.Api\"");
        body.ShouldContain("\"environment\":\"local\"");
    }
}
