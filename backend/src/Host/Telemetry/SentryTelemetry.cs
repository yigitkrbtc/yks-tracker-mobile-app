using Sentry.AspNetCore;
using Sentry.Extensibility;

namespace Nirengi.Host.Telemetry;

/// <summary>
/// Error tracking (ADR-0022 §5). Host only: modules log through ILogger and never see Sentry (architecture test).
/// Off when <c>Sentry:Dsn</c> is empty (local runs, tests).
/// </summary>
public static class SentryTelemetry
{
    public static bool IsEnabled(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Sentry:Dsn"]);

    /// <summary>
    /// Layer 1: no personal data, request bodies, tracing, sessions or failed-request capture; every event and
    /// breadcrumb through <see cref="SentryScrubber"/>. Filters are set before the DSN.
    /// </summary>
    public static void Configure(SentryAspNetCoreOptions o, IConfiguration configuration, string version)
    {
        o.SetBeforeSend(SentryScrubber.ScrubEvent);
        o.SetBeforeBreadcrumb(SentryScrubber.ScrubBreadcrumb);
        o.SendDefaultPii = false;
        o.MaxRequestBodySize = RequestSize.None;
        o.TracesSampleRate = 0;
        o.AutoSessionTracking = false;
        o.CaptureFailedRequests = false;
        o.DisableDiagnosticSourceIntegration();
        o.ServerName = "api";
        o.Release = $"nirengi-api@{version}";
        o.Environment = configuration["Sentry:Environment"] ?? "production";
        o.Dsn = configuration["Sentry:Dsn"];
    }

    /// <summary>For the options test: the SDK keeps the before-send callback internal.</summary>
    internal static bool IsFilterInstalled(SentryAspNetCoreOptions o) =>
        typeof(Sentry.SentryOptions)
            .GetProperty("BeforeSendInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(o) is not null;
}
