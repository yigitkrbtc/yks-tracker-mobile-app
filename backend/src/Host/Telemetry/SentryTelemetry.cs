using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Serilog;
using Serilog.Events;

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
    /// Serilog (configuration + console) for the Host, and with a DSN the Sentry SDK and sink: error logs become
    /// events, warnings and up breadcrumbs. Not for <c>--migrate</c>. True when error tracking is on.
    /// </summary>
    public static bool AddLoggingAndErrorTracking(WebApplicationBuilder builder, bool migrateOnly, Action<SentryAspNetCoreOptions>? testOptions = null)
    {
        var enabled = IsEnabled(builder.Configuration) && !migrateOnly;
        if (enabled)
        {
            var version = typeof(SentryTelemetry).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion ?? "0.0.0";
            builder.WebHost.UseSentry(o =>
            {
                Configure(o, builder.Configuration, version);
                testOptions?.Invoke(o);
            });
        }

        builder.Host.UseSerilog((context, logger) =>
        {
            logger
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console();
            if (enabled)
            {
                logger.WriteTo.Sentry(o =>
                {
                    o.MinimumEventLevel = LogEventLevel.Error;
                    o.MinimumBreadcrumbLevel = LogEventLevel.Warning;
                    o.RestrictedToMinimumLevel = LogEventLevel.Warning; // Information never reaches the sink
                    o.TextFormatter = new TemplateOnlyFormatter(); // "Failed for {Name}", never the values
                });
            }
        });
        return enabled;
    }

    /// <summary>Starts the SDK before the first request, so start-up failures (seed, configuration) are reported.</summary>
    public static void StartEarly(WebApplication app) => _ = app.Services.GetRequiredService<IHub>();

    /// <summary>
    /// Layer 1: no personal data, request bodies, tracing, sessions or failed-request capture; every event and
    /// breadcrumb through <see cref="SentryScrubber"/>. The Sentry Logs and Metrics products are off: since SDK 7
    /// every log line the sink receives is also sent as a structured log, which no event filter sees
    /// (SentryPipelineTests). Filters are set before the DSN.
    /// </summary>
    public static void Configure(SentryAspNetCoreOptions o, IConfiguration configuration, string version)
    {
        o.SetBeforeSend(SentryScrubber.ScrubEvent);
        o.SetBeforeBreadcrumb(SentryScrubber.ScrubBreadcrumb);
        o.SetBeforeSendLog(_ => null);
        o.SetBeforeSendMetric(_ => null);
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

/// <summary>Writes a log event's message template, not its rendered text: property values never reach Sentry.</summary>
internal sealed class TemplateOnlyFormatter : Serilog.Formatting.ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output) => output.Write(logEvent.MessageTemplate.Text);
}
