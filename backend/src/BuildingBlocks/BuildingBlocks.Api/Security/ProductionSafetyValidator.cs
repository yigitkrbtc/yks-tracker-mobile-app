using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Nirengi.BuildingBlocks.Api.Security;

/// <summary>Marker for the start-up check below; there is nothing to configure.</summary>
public sealed class ProductionSafetyOptions;

/// <summary>
/// Production start-up guard (security audit G12). A production host fails fast, before it serves a request, when
/// (1) no proxy or network is listed under <c>ForwardedHeaders</c>: client IPs, and so every per-IP rate limit, would be the
/// proxy's own address or spoofable through X-Forwarded-For, and (2) a secret is one of the development keys that are
/// public in the repository. Messages name the setting, never the value.
/// </summary>
internal sealed class ProductionSafetyValidator(IHostEnvironment environment, IConfiguration configuration) : IValidateOptions<ProductionSafetyOptions>
{
    public ValidateOptionsResult Validate(string? name, ProductionSafetyOptions options)
    {
        if (!environment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (ForwardedHeadersSettings.Proxies(configuration).Count == 0 && ForwardedHeadersSettings.Networks(configuration).Count == 0)
        {
            failures.Add("ForwardedHeaders:KnownProxies or ForwardedHeaders:KnownNetworks must list the reverse proxy in Production; otherwise every client shares the proxy's IP and the per-IP rate limits are meaningless.");
        }

        failures.AddRange(DevelopmentSecrets.InUse(configuration).Select(key => $"{key} is the development key from the repository; set a real secret for Production."));
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

internal static class ForwardedHeadersSettings
{
    public static IReadOnlyList<string> Proxies(IConfiguration configuration) =>
        configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? [];

    /// <summary>CIDR ranges such as <c>10.0.0.0/8</c>.</summary>
    public static IReadOnlyList<string> Networks(IConfiguration configuration) =>
        configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>()?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [];
}
