using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Identity.Domain.Users;

/// <summary>An e-mail address as typed plus the form used for lookups and uniqueness.</summary>
public sealed record Email
{
    public const int MaxLength = 254;

    private Email(string value, string normalized)
    {
        Value = value;
        Normalized = normalized;
    }

    public string Value { get; }

    public string Normalized { get; }

    public static Result<Email> Create(string? input)
    {
        var value = input?.Trim() ?? string.Empty;
        if (!IsWellFormed(value))
        {
            return IdentityErrors.InvalidEmail;
        }

        return new Email(value, Normalize(value));
    }

    /// <summary>
    /// Culture-free lower case. Under tr-TR "I" would become "ı" and the same mailbox would get two accounts;
    /// "İ" (U+0130) is folded to "i" explicitly because invariant casing leaves it alone.
    /// </summary>
    public static string Normalize(string input) => input.Trim().Replace('İ', 'i').ToLowerInvariant();

    private static bool IsWellFormed(string value)
    {
        if (value.Length is 0 or > MaxLength || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        var domain = value[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        return dot > 0 && dot < domain.Length - 1 && !domain.Contains("..", StringComparison.Ordinal);
    }
}
