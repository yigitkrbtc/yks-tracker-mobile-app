using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Security;
using Nirengi.Modules.Tracking.Api.Endpoints;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Security audit PR 3: what the API accepts at its edge (G6 rate limit keys, G7 limits, G12 production start, G18 OpenAPI).</summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class ApiBoundaryTests(NirengiApiFactory factory)
{
    private const string DevJwtKey = "PL5SvfS4nbW/tV69TxvZ2UCr04nEMZTY/dCg5DKqRjM=";

    private readonly AuthApi _api = new(factory);

    /// <summary>G6: a classroom behind one NAT is not locked out by one student's typos, and case or padding does not buy a fresh budget.</summary>
    [Fact]
    public async Task Credential_attempts_are_budgeted_per_address_and_per_ip_and_email_together()
    {
        using var host = Limited(("RateLimiting:Auth:PermitLimit", "3"), ("RateLimiting:Auth:PerIpPermitLimit", "1000"));
        var client = host.CreateClient();
        var known = AuthApi.NewEmail();
        await _api.RegisterAndVerify(known);
        var unknown = AuthApi.NewEmail();

        // The same sequence for a real and an unknown address: three answers, then 429, so the limit reveals nothing about the account.
        foreach (var email in new[] { known, unknown })
        {
            for (var i = 0; i < 3; i++)
            {
                (await Login(client, email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }

            (await Login(client, email)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            (await Login(client, $"  {email.ToUpperInvariant()} ")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        }

        // Same IP, other address: its own budget.
        (await Login(client, AuthApi.NewEmail())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The e-mail partitions cover every credential and code endpoint, not just login.
        var forgot = AuthApi.NewEmail();
        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/v1/auth/password/forgot", new { email = forgot }, AuthApi.Json, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        (await client.PostAsJsonAsync("/v1/auth/password/forgot", new { email = forgot }, AuthApi.Json, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>G6: many addresses from one IP still run into the per-IP ceiling.</summary>
    [Fact]
    public async Task One_ip_cannot_sidestep_the_budget_with_many_addresses()
    {
        using var host = Limited(("RateLimiting:Auth:PermitLimit", "3"), ("RateLimiting:Auth:PerIpPermitLimit", "5"));
        var client = host.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
        {
            statuses.Add((await Login(client, AuthApi.NewEmail())).StatusCode);
        }

        statuses.ShouldBe([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 5), HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests]);
    }

    /// <summary>G7: a signed-in student's general budget is per user; others and anonymous public reads are not affected.</summary>
    [Fact]
    public async Task Data_endpoints_have_a_per_user_budget()
    {
        using var host = Limited(("RateLimiting:Api:PermitLimit", "3"));
        var client = host.CreateClient();
        var a = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var b = await _api.RegisterAndVerify(AuthApi.NewEmail());

        for (var i = 0; i < 3; i++)
        {
            (await Get(client, "/v1/sync", a)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await Get(client, "/v1/sync", a)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await Get(client, "/v1/analytics/overview", a)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await Get(client, "/v1/sync", b)).StatusCode.ShouldBe(HttpStatusCode.OK);
        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync("/v1/curriculum/exam-calendar", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Endpoints with their own policy keep it (export: 3 an hour), not the general one.
        (await Get(client, "/v1/me/export", b)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>G7: no legitimate request is bigger than 64 KB; anything above is cut off before it is read.</summary>
    [Fact]
    public async Task Request_bodies_above_64_kb_are_refused()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();

        var huge = await Put(tokens, $"/v1/topic-tests/{id}", new string('x', RequestLimits.MaxBodyBytes + 1));
        var fits = await Put(tokens, $"/v1/topic-tests/{id}", new string('x', 1000));

        huge.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        fits.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _api.Client.PostAsync("/v1/auth/login", new StringContent(new string('x', RequestLimits.MaxBodyBytes + 1), Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>
    /// G7: the 64 KB cap is measured against the biggest body the API itself accepts: a mock exam with the maximum 300 topic
    /// marks (30 sections, 20-character codes, every optional field, counts at their ceiling, notes at their limit with two-byte letters).
    /// </summary>
    [Fact]
    public void The_largest_legitimate_body_fits_the_limit_with_room_to_spare()
    {
        var sectionCode = new string('S', 20);
        var request = new SaveMockExamRequest(
            Modules.Tracking.Domain.MockExams.MockExamKind.Branch,
            DateOnly.MaxValue,
            Guid.NewGuid(),
            null,
            new string('ş', 100),
            600,
            new string('ğ', 500),
            [.. Enumerable.Range(0, 30).Select(_ => new Modules.Tracking.Application.Features.MockExams.MockSectionInput(sectionCode, 80, 80, 80))],
            [.. Enumerable.Range(0, 300).Select(_ => new Modules.Tracking.Application.Features.MockExams.TopicMarkInput(sectionCode, Guid.NewGuid(), 80, 80, 80, 80, 80))],
            DateTimeOffset.UtcNow);

        var size = JsonSerializer.SerializeToUtf8Bytes(request, AuthApi.Json).Length;

        size.ShouldBeLessThan(RequestLimits.MaxBodyBytes * 9 / 10, $"largest body is {size} bytes");
    }

    /// <summary>G12: production refuses to start where every client would look like one address (no known proxy) or with the checked-in dev keys.</summary>
    [Fact]
    public void Production_refuses_to_start_without_known_proxies_or_with_development_keys()
    {
        var noProxy = Should.Throw<Exception>(() => Production().CreateClient());
        noProxy.ToString().ShouldContain("ForwardedHeaders");

        var devJwt = Should.Throw<Exception>(() => Production(("ForwardedHeaders:KnownProxies:0", "10.0.0.7"), ("Jwt:SigningKey", DevJwtKey)).CreateClient());
        devJwt.ToString().ShouldContain("Jwt:SigningKey");
        devJwt.ToString().ShouldNotContain(DevJwtKey);

        var devHash = Should.Throw<Exception>(() => Production(("ForwardedHeaders:KnownProxies:0", "10.0.0.7"), ("Identity:Security:HashKey", DevelopmentSecrets.Values[^1])).CreateClient());
        devHash.ToString().ShouldContain("Identity:Security:HashKey");
    }

    [Fact]
    public async Task Production_starts_with_a_proxy_or_a_network_and_has_no_openapi_endpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        using var withProxy = Production(("ForwardedHeaders:KnownProxies:0", "10.0.0.7"));
        using var withNetwork = Production(("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8"));

        (await withProxy.CreateClient().GetAsync("/health/live", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await withNetwork.CreateClient().GetAsync("/health/live", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await withProxy.CreateClient().GetAsync("/openapi/v1.json", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Outside production the contract is served as before (the test host runs as "Testing").
        (await _api.Client.GetAsync("/openapi/v1.json", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The dev keys that the production guard refuses are exactly the ones checked into appsettings.Development.json.</summary>
    [Fact]
    public void Every_secret_in_the_development_settings_is_on_the_production_blocklist()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Host/appsettings.Development.json"));
        using var settings = JsonDocument.Parse(File.ReadAllText(path));

        var checkedIn = new[]
        {
            settings.RootElement.GetProperty("Jwt").GetProperty("SigningKey").GetString()!,
            settings.RootElement.GetProperty("Identity").GetProperty("Security").GetProperty("HashKey").GetString()!,
        };

        DevelopmentSecrets.Values.ShouldBe(checkedIn, ignoreOrder: true);
    }

    private WebApplicationFactory<Program> Limited(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }
        });

    private WebApplicationFactory<Program> Production(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }
        });

    private static Task<HttpResponseMessage> Login(HttpClient client, string email) =>
        client.PostAsJsonAsync("/v1/auth/login", new { email, password = "yanlisSifre123" }, AuthApi.Json, TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> Get(HttpClient client, string path, AuthApi.Tokens tokens)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> Put(AuthApi.Tokens tokens, string path, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return _api.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
