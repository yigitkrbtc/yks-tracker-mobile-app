// Plain logger calls on purpose: the test feeds personal data through the same paths the modules use.
#pragma warning disable CA1848, CA1873
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nirengi.Host.Telemetry;
using Sentry;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;
using Serilog;
using Serilog.Context;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SentryGlobalState
{
    public const string Name = "Sentry SDK (process-wide state)";
}

/// <summary>
/// ADR-0022 §5 end to end, without Docker: the Host's own logging and Sentry wiring, a fake DSN and a capturing
/// transport. Only error events (and SDK client reports) leave the process, and none of the seeded personal data
/// does — whatever telemetry a future SDK version turns on by default.
/// </summary>
[Collection(SentryGlobalState.Name)]
public sealed class SentryPipelineTests
{
    private static readonly string[] Secrets =
    [
        "info.user@example.com", "warn.user@example.com", "err.user@example.com", "startup@example.com",
        "q.user@example.com", "secret-note-text", "ctx-secret-note", "body-secret", "Ayse Yilmaz", "198.51.100.7",
        "eyJabc.eyJdef.sig", "0198c2a4-7d1e-7b3a-9f00-123456789abc",
    ];

    [Fact]
    public async Task Only_scrubbed_error_events_leave_the_process()
    {
        var transport = new CapturingTransport();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sentry:Dsn"] = "https://key@o1.ingest.de.sentry.io/1",
            ["Sentry:Environment"] = "production",
            ["Serilog:MinimumLevel:Default"] = "Information",
        });
        SentryTelemetry.AddLoggingAndErrorTracking(builder, migrateOnly: false, testOptions: o => o.Transport = transport).ShouldBeTrue();
        builder.Services.AddProblemDetails();

        await using var app = builder.Build();
        SentryTelemetry.StartEarly(app);
        app.Services.GetRequiredService<ILogger<SentryPipelineTests>>()
            .LogError("Startup failed for {Email}", "startup@example.com");
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();
        app.MapGet("/v1/info/{id}", (string id, ILogger<SentryPipelineTests> log) =>
        {
            log.LogInformation("Login for {Email} record {Id}", "info.user@example.com", id);
            return "ok";
        });
        app.MapGet("/v1/warn", (ILogger<SentryPipelineTests> log) =>
        {
            log.LogWarning("Slow for {Email} note {Note}", "warn.user@example.com", "secret-note-text");
            using (LogContext.PushProperty("UserNote", "ctx-secret-note"))
            {
                log.LogError(new InvalidOperationException("boom for err.user@example.com"), "Failed for {Name}", "Ayse Yilmaz");
            }

            return "ok";
        });
        app.MapPost("/v1/throw/{id}", (string id) => Results.Text(id.Length > 0 ? throw new FormatException($"bad {id}") : ""));
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer eyJabc.eyJdef.sig");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.7");
        await client.GetAsync("/v1/info/0198c2a4-7d1e-7b3a-9f00-123456789abc", TestContext.Current.CancellationToken);
        await client.GetAsync("/v1/warn", TestContext.Current.CancellationToken);
        await client.PostAsync(
            "/v1/throw/0198c2a4-7d1e-7b3a-9f00-123456789abc?email=q.user@example.com",
            new StringContent("{\"note\":\"body-secret\"}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken); // structured logs are batched
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(10));
        await app.StopAsync(TestContext.Current.CancellationToken);

        var items = transport.Items;
        items.Select(i => i.Type).Distinct().Order().ToArray().ShouldBe(["client_report", "event"], customMessage: string.Join(", ", items.Select(i => i.Type)));
        items.Count(i => i.Type == "event").ShouldBeGreaterThanOrEqualTo(3, "startup error, logged error, unhandled exception");
        var sent = string.Join("\n", transport.Envelopes);
        foreach (var secret in Secrets)
        {
            sent.ShouldNotContain(secret, customMessage: secret);
        }
    }

    private sealed class CapturingTransport : ITransport
    {
        private readonly Lock _gate = new();
        public List<string> Envelopes { get; } = [];
        public List<(string Type, string Body)> Items { get; } = [];

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            var text = Encoding.UTF8.GetString(stream.ToArray());
            lock (_gate)
            {
                Envelopes.Add(text);
                foreach (var item in envelope.Items)
                {
                    Items.Add((item.Header.TryGetValue("type", out var type) ? type?.ToString() ?? "?" : "?", text));
                }
            }
        }
    }
}
