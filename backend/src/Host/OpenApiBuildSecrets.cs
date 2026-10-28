using System.Security.Cryptography;

namespace Nirengi.Host;

/// <summary>
/// Random placeholders for settings that are validated on start, and no database work; used only while generating
/// contracts/openapi, where no database exists.
/// </summary>
internal static class OpenApiBuildSecrets
{
    public static readonly IReadOnlyDictionary<string, string?> Values = new Dictionary<string, string?>
    {
        ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["Identity:Security:HashKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["Smtp:Host"] = "localhost",
        ["Smtp:FromAddress"] = "openapi@localhost",
        ["Database:MigrateOnStartup"] = "false",
        ["Database:SeedOnStartup"] = "false",
    };
}
