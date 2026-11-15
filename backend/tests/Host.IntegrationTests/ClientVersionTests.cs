using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>ADR-0021 M4: clients below Clients:MinimumVersion get 426; a missing header passes.</summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class ClientVersionTests(NirengiApiFactory factory)
{
    // Without a token the real endpoint answers 401, so "401" means the request got past the gate.
    private const string Path = "/v1/me/export";

    private HttpClient ClientWithMinimum(string minimum) =>
        factory.WithWebHostBuilder(b => b.UseSetting("Clients:MinimumVersion", minimum)).CreateClient();

    private static async Task<HttpResponseMessage> Get(HttpClient client, string? version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        if (version is not null)
        {
            request.Headers.Add("X-Client-Version", version);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Older_client_gets_426_with_a_problem()
    {
        using var client = ClientWithMinimum("1.2.0");

        var response = await Get(client, "1.1.9");

        response.StatusCode.ShouldBe(HttpStatusCode.UpgradeRequired);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("type").GetString().ShouldBe("client.upgrade_required");
        body.RootElement.GetProperty("status").GetInt32().ShouldBe(426);
        body.RootElement.GetProperty("minimumVersion").GetString().ShouldBe("1.2.0");
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("1.2")]
    [InlineData("1.2.0+7")]
    [InlineData("1.10.0")]
    [InlineData("2.0.0")]
    public async Task Equal_or_newer_client_passes(string version)
    {
        using var client = ClientWithMinimum("1.2.0");

        var response = await Get(client, version);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_without_the_header_passes()
    {
        using var client = ClientWithMinimum("1.2.0");

        var response = await Get(client, version: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gate_is_off_when_no_minimum_is_configured()
    {
        var response = await Get(factory.CreateClient(), "0.0.1");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void Unreadable_minimum_stops_the_host()
    {
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Clients:MinimumVersion", "latest"));

        Should.Throw<InvalidOperationException>(() => host.CreateClient());
    }
}
