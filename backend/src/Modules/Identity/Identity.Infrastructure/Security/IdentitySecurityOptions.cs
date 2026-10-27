using System.ComponentModel.DataAnnotations;

namespace Nirengi.Modules.Identity.Infrastructure.Security;

/// <summary>Secrets for Identity; bound from "Identity:Security". Supply via environment, never commit for production.</summary>
public sealed class IdentitySecurityOptions
{
    public const string SectionName = "Identity:Security";

    /// <summary>HMAC-SHA256 key (base64, at least 32 bytes) for code, refresh token and recovery code hashes.</summary>
    [Required]
    public string HashKey { get; set; } = string.Empty;

    /// <summary>Issuer label shown in authenticator apps.</summary>
    public string TotpIssuer { get; set; } = "Nirengi";
}
