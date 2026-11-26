using System.Text.RegularExpressions;
using Sentry;
using Sentry.Protocol;

namespace Nirengi.Host.Telemetry;

/// <summary>
/// ADR-0022 §5 layer 2: allow-list filter. The event is cleaned in place (its id stays): user, request, extra,
/// modules, threads, device and custom contexts, frame variables and exception data are emptied; messages and
/// breadcrumbs are masked. The real guard is not logging personal data; masking is defence in depth. When the SDK's
/// collections stop being clearable the event is dropped rather than sent unfiltered (SentryScrubberTests break).
/// </summary>
public static partial class SentryScrubber
{
    private const string Redacted = "[redacted]";
    private static readonly HashSet<string> AllowedTags = ["route", "method", "status"];

    /// <summary>
    /// Replaces e-mail, GUIDs (record ids in paths), IPv4/IPv6, JWT-like tokens and runs of 8+ digits with
    /// <c>[redacted]</c>.
    /// </summary>
    public static string Mask(string text) =>
        LongDigits().Replace(Ipv6().Replace(Ipv4().Replace(Guid().Replace(Jwt().Replace(Email().Replace(text, Redacted), Redacted), Redacted), Redacted), Redacted), Redacted);

    public static SentryEvent? ScrubEvent(SentryEvent e)
    {
        if (e.Extra is not IDictionary<string, object?> extra || e.Breadcrumbs is not IList<Breadcrumb> crumbs)
        {
            return null;
        }

        extra.Clear();
        e.Modules.Clear();
        e.User = new SentryUser();
        e.Request = new SentryRequest();
        e.ServerName = "api";
        e.SentryThreads = null;
        e.Contexts = ScrubContexts(e.Contexts);

        foreach (var tag in e.Tags.ToList())
        {
            if (AllowedTags.Contains(tag.Key))
            {
                e.SetTag(tag.Key, Mask(tag.Value));
            }
            else
            {
                e.UnsetTag(tag.Key);
            }
        }

        if (e.Message is { } message)
        {
            // A real template ("User {Id} failed") is unchanged by masking; an interpolated one is not a template.
            e.Message = new SentryMessage { Message = MaskOrNull(message.Message), Formatted = MaskOrNull(message.Formatted) };
        }

        e.SentryExceptions = e.SentryExceptions?.Select(ScrubException).ToList();

        var kept = crumbs.Select(ScrubBreadcrumb).OfType<Breadcrumb>().ToList();
        crumbs.Clear();
        foreach (var crumb in kept)
        {
            crumbs.Add(crumb);
        }

        return e;
    }

    /// <summary>Only warning-or-worse log breadcrumbs, masked and without data.</summary>
    public static Breadcrumb? ScrubBreadcrumb(Breadcrumb crumb)
    {
        if (crumb.Level < BreadcrumbLevel.Warning)
        {
            return null;
        }

        return crumb.Data is null or { Count: 0 } && Mask(crumb.Message ?? "") == (crumb.Message ?? "")
            ? crumb
            : new Breadcrumb(MaskOrNull(crumb.Message)!, crumb.Type!, data: null, category: crumb.Category, level: crumb.Level);
    }

    private static string? MaskOrNull(string? text) => text is null ? null : Mask(text);

    private static SentryException ScrubException(SentryException ex) => new()
    {
        Type = ex.Type,
        Value = MaskOrNull(ex.Value),
        Module = ex.Module,
        ThreadId = ex.ThreadId,
        Stacktrace = ex.Stacktrace is null ? null : new SentryStackTrace
        {
            AddressAdjustment = ex.Stacktrace.AddressAdjustment,
            Frames = [.. ex.Stacktrace.Frames.Select(ScrubFrame)],
        },
        Mechanism = ex.Mechanism is not { } m ? null : new Mechanism
        {
            Type = m.Type,
            Handled = m.Handled,
            Synthetic = m.Synthetic,
            IsExceptionGroup = m.IsExceptionGroup,
            ExceptionId = m.ExceptionId,
            ParentId = m.ParentId,
            Source = m.Source,
        },
    };

    private static SentryStackFrame ScrubFrame(SentryStackFrame f) => new()
    {
        Module = f.Module,
        Function = f.Function,
        FileName = f.FileName,
        AbsolutePath = f.AbsolutePath,
        LineNumber = f.LineNumber,
        ColumnNumber = f.ColumnNumber,
        InApp = f.InApp,
        Package = f.Package,
        Platform = f.Platform,
        ImageAddress = f.ImageAddress,
        SymbolAddress = f.SymbolAddress,
        InstructionAddress = f.InstructionAddress,
        AddressMode = f.AddressMode,
        FunctionId = f.FunctionId,
    };

    /// <summary>Runtime, operating system and app only; the device context (host name) and custom ones go.</summary>
    private static SentryContexts ScrubContexts(SentryContexts c)
    {
        var clean = new SentryContexts();
        clean.Runtime.Name = c.Runtime.Name;
        clean.Runtime.Version = c.Runtime.Version;
        clean.Runtime.Build = c.Runtime.Build;
        clean.Runtime.Identifier = c.Runtime.Identifier;
        clean.OperatingSystem.Name = c.OperatingSystem.Name;
        clean.OperatingSystem.Version = c.OperatingSystem.Version;
        clean.OperatingSystem.Build = c.OperatingSystem.Build;
        clean.App.Name = c.App.Name;
        clean.App.Version = c.App.Version;
        clean.App.Build = c.App.Build;
        clean.App.BuildType = c.App.BuildType;
        clean.App.StartTime = c.App.StartTime;
        return clean;
    }

    [GeneratedRegex(@"[\p{L}\p{N}._%+\-]+(?:@|%40)[\p{L}\p{N}.\-]+\.\p{L}{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\b")]
    private static partial Regex Guid();

    // Assembly versions ("Version=10.0.0.0") are not addresses.
    [GeneratedRegex(@"(?<!Version=)\b(?:\d{1,3}\.){3}\d{1,3}\b")]
    private static partial Regex Ipv4();

    // Full form (8 groups) or a compressed one with "::"; times (12:30:45) and file:line:column do not match.
    [GeneratedRegex(@"(?<![0-9A-Fa-f:])(?:(?:[0-9A-Fa-f]{1,4}:){7}[0-9A-Fa-f]{1,4}|(?:[0-9A-Fa-f]{1,4}:){0,6}[0-9A-Fa-f]{0,4}::(?:[0-9A-Fa-f]{1,4}:){0,6}[0-9A-Fa-f]{0,4})(?![0-9A-Fa-f:])")]
    private static partial Regex Ipv6();

    [GeneratedRegex(@"\d{8,}")]
    private static partial Regex LongDigits();
}
