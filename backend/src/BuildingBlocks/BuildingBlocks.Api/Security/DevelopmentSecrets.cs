using Microsoft.Extensions.Configuration;

namespace Nirengi.BuildingBlocks.Api.Security;

/// <summary>
/// The signing and hashing keys checked into <c>appsettings.Development.json</c>. They are public, so a production host that
/// runs with one has no secret at all; <see cref="ProductionSafetyValidator"/> refuses to start with them (security audit G12).
/// A test keeps this list equal to the file.
/// </summary>
public static class DevelopmentSecrets
{
    public static readonly IReadOnlyList<string> Values =
    [
        "PL5SvfS4nbW/tV69TxvZ2UCr04nEMZTY/dCg5DKqRjM=",
        "1niXSTF3Yp/XRGuBX0S6PHSHYHNmtpAOQsGUotnR2zU=",
    ];

    /// <summary>Settings that hold a secret and so must not hold one of <see cref="Values"/> in production.</summary>
    public static readonly IReadOnlyList<string> SecretSettings = ["Jwt:SigningKey", "Identity:Security:HashKey"];

    public static IEnumerable<string> InUse(IConfiguration configuration) =>
        SecretSettings.Where(key => configuration[key] is { } value && Values.Contains(value, StringComparer.Ordinal));
}
