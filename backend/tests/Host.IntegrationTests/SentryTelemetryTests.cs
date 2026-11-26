using Microsoft.Extensions.Configuration;
using Nirengi.Host.Telemetry;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>ADR-0022 §5 layer 1: the SDK starts with every personal-data feature off; no DSN, no SDK.</summary>
public sealed class SentryTelemetryTests
{
    private static IConfiguration Config(string? dsn) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Sentry:Dsn"] = dsn, ["Sentry:Environment"] = "production" })
        .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Without_a_dsn_tracking_is_off(string? dsn) =>
        SentryTelemetry.IsEnabled(Config(dsn)).ShouldBeFalse();

    [Fact]
    public void Options_turn_personal_data_and_tracing_off()
    {
        var o = new SentryAspNetCoreOptions();

        SentryTelemetry.Configure(o, Config("https://key@o1.ingest.de.sentry.io/1"), "1.2.3");

        o.Dsn.ShouldBe("https://key@o1.ingest.de.sentry.io/1");
        o.Environment.ShouldBe("production");
        o.Release.ShouldBe("nirengi-api@1.2.3");
        o.ServerName.ShouldBe("api");
        o.SendDefaultPii.ShouldBeFalse();
        o.MaxRequestBodySize.ShouldBe(RequestSize.None);
        o.TracesSampleRate.ShouldBe(0);
        o.AutoSessionTracking.ShouldBeFalse();
        o.CaptureFailedRequests.ShouldBeFalse();
    }

    [Fact]
    public void Options_send_events_through_the_filter()
    {
        var o = new SentryAspNetCoreOptions();
        SentryTelemetry.Configure(o, Config("https://key@o1.ingest.de.sentry.io/1"), "1.2.3");

        SentryTelemetry.IsFilterInstalled(o).ShouldBeTrue();
    }
}
