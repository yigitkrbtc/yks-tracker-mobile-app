using System.Net;
using Npgsql;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class HostTests(NirengiApiFactory factory)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_green(string path)
    {
        var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Every_module_gets_its_own_schema_and_outbox()
    {
        _ = factory.Server; // starts the host, which runs the migrations

        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT table_schema FROM information_schema.tables WHERE table_name = 'outbox_messages' ORDER BY 1",
            connection);

        var schemas = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            schemas.Add(reader.GetString(0));
        }

        schemas.ShouldBe(["analytics", "curriculum", "engagement", "identity", "platform", "recommendation", "tracking"]);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_responses_carry_the_baseline_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldStartWith("default-src 'none'");
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/")]
    [InlineData("/admin/versions")]
    [InlineData("/admin/index.html")]
    public async Task Admin_panel_routes_get_index_with_a_fresh_nonce(string path)
    {
        var client = factory.CreateClient();

        var first = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var second = await client.GetAsync(path, TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        first.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        first.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var policy = first.Headers.GetValues("Content-Security-Policy").Single();
        policy.ShouldContain("script-src 'self';");
        policy.ShouldContain("frame-ancestors 'none'");
        var nonce = System.Text.RegularExpressions.Regex.Match(policy, "'nonce-([^']+)'").Groups[1].Value;
        nonce.ShouldNotBeNullOrEmpty();
        (await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain($"content=\"{nonce}\"");
        second.Headers.GetValues("Content-Security-Policy").Single().ShouldNotContain(nonce);
    }

    [Fact]
    public async Task Admin_assets_are_cached_and_missing_ones_are_not_routes()
    {
        var client = factory.CreateClient();

        var asset = await client.GetAsync("/admin/assets/app.js", TestContext.Current.CancellationToken);
        asset.StatusCode.ShouldBe(HttpStatusCode.OK);
        asset.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromDays(365));

        (await client.GetAsync("/admin/assets/missing.js", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/admin/../appsettings.json", TestContext.Current.CancellationToken)).StatusCode.ShouldNotBe(HttpStatusCode.OK);
    }
}
