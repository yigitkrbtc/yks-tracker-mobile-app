namespace Nirengi.Modules.Identity.Application.Features.Mfa;

internal static class RecoveryCodes
{
    /// <summary>Case and dashes do not matter when a person types the code back.</summary>
    public static string Normalize(string code) => code.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
}
