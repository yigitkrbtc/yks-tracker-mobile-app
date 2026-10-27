using System.Security.Cryptography;

namespace Nirengi.Host;

/// <summary>Random placeholders for settings that are validated on start; used only while generating contracts/openapi.</summary>
internal static class OpenApiBuildSecrets
{
    public static readonly IReadOnlyDictionary<string, string?> Values = new Dictionary<string, string?>
    {
        ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    };
}
