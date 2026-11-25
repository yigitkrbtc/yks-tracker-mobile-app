namespace Nirengi.Host;

internal static class SecretFiles
{
    public const string DefaultDirectory = "/run/secrets";

    /// <summary>
    /// Reads compose file secrets (ADR-0022): a file named <c>ConnectionStrings__Database</c> becomes the setting
    /// <c>ConnectionStrings:Database</c>. Added after the other sources, so a secret file wins. A missing directory is fine.
    /// </summary>
    public static IConfigurationBuilder AddSecretFiles(this IConfigurationBuilder configuration, string directory = DefaultDirectory) =>
        configuration.AddKeyPerFile(directory, optional: true);
}
