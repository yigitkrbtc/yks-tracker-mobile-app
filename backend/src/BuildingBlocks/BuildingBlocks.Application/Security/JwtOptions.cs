using System.ComponentModel.DataAnnotations;

namespace Nirengi.BuildingBlocks.Application.Security;

/// <summary>Access token settings shared by the issuer (Identity) and the validator (Host).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "nirengi";

    [Required]
    public string Audience { get; set; } = "nirengi-api";

    /// <summary>HMAC-SHA256 key, base64; at least 32 bytes. Never committed for non-development environments.</summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public byte[] SigningKeyBytes() => Convert.FromBase64String(SigningKey);
}
