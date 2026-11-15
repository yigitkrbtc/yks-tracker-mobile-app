namespace Nirengi.Host;

/// <summary>
/// Refuses clients older than <c>Clients:MinimumVersion</c> (ADR-0021 M4). The mobile app sends its version in
/// <see cref="HeaderName"/> on every request; a request below the minimum gets 426 Upgrade Required with a problem
/// the app turns into an "update required" screen. A missing (or unreadable) header passes: the admin panel, probes
/// and scripts send none, and the gate is a compatibility aid, not an access control (a client can send any value).
/// </summary>
internal static class ClientVersionGate
{
    public const string HeaderName = "X-Client-Version";
    public const string ProblemType = "client.upgrade_required";

    public static WebApplication UseNirengiClientVersion(this WebApplication app)
    {
        var configured = app.Configuration["Clients:MinimumVersion"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return app;
        }

        var minimum = TryParse(configured)
            ?? throw new InvalidOperationException($"Clients:MinimumVersion '{configured}' is not a version such as 1.2.3.");

        app.Use(async (context, next) =>
        {
            if (TryParse(context.Request.Headers[HeaderName].ToString()) is { } client && client < minimum)
            {
                await Results.Problem(
                    statusCode: StatusCodes.Status426UpgradeRequired,
                    title: "Bu uygulama sürümü artık desteklenmiyor. Lütfen güncelleyin.",
                    type: ProblemType,
                    extensions: new Dictionary<string, object?> { ["minimumVersion"] = minimum.ToString() })
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });
        return app;
    }

    /// <summary>Reads "1.2", "1.2.3" or "1.2.3+4" / "1.2.3-beta" (build metadata and pre-release tags are ignored).</summary>
    internal static Version? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var core = value.Trim().Split('+', '-')[0];
        return Version.TryParse(core, out var version) && version.Revision == -1
            ? new Version(version.Major, version.Minor, Math.Max(version.Build, 0)) // "1.2" equals "1.2.0"
            : null;
    }
}
