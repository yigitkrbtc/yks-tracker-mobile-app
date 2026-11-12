using FluentValidation;

namespace Nirengi.Modules.Identity.Application;

internal static class PasswordRules
{
    public const int MinLength = 10;

    public const int MaxLength = 128;

    public const string CommonPasswordMessage = "Bu şifre çok kolay tahmin edilir; daha az bilinen bir şey seç.";

    // Words people build "a word plus some digits" passwords from. Lower case, Turkish letters folded (see Fold).
    private static readonly HashSet<string> CommonWords = new(StringComparer.Ordinal)
    {
        "sifre", "sifrem", "parola", "parolam", "password", "passw0rd", "pass", "admin", "yonetici", "nirengi",
        "qwerty", "qwertyui", "qwertyuiop", "asdf", "asdfgh", "asdfghjkl", "zxcvbn", "abc", "abcd", "abcde", "abcdef",
        "galatasaray", "fenerbahce", "besiktas", "trabzonspor", "bursaspor", "turkiye", "istanbul", "ankara", "izmir",
        "ataturk", "yks", "tyt", "ayt", "osym", "sinav", "universite", "ogrenci", "okul", "kitap", "test", "deneme",
        "seviyorum", "askim", "canim", "hayat", "mustafa", "mehmet", "ahmet", "ali", "ayse", "fatma", "zeynep",
        "elif", "emre", "burak", "merhaba", "selam", "iloveyou", "welcome", "letmein", "monkey", "dragon", "football",
        "superman", "batman", "charlie", "master", "login", "user", "guest", "hello", "secret",
    };

    // Typed runs: the keyboard rows and the digit row, also reversed.
    private static readonly string[] Runs = ["qwertyuiop", "asdfghjkl", "zxcvbnm", "1234567890", "1q2w3e4r5t6y7u8i9o0p", "abcdefghijklmnopqrstuvwxyz"];

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty()
        .Length(MinLength, MaxLength)
        .Matches(@"\p{L}").WithMessage("Şifre en az bir harf içermeli.")
        .Matches(@"\d").WithMessage("Şifre en az bir rakam içermeli.");

    /// <summary>Refuses passwords that are a common word, a keyboard run or the person's own name plus digits (ADR-0021).</summary>
    public static IRuleBuilderOptions<T, string> NotCommonPassword<T>(this IRuleBuilderOptions<T, string> rule, params Func<T, string?>[] personal) => rule
        .Must((owner, password) => !IsCommon(password, personal.Select(p => p(owner))))
        .WithMessage(CommonPasswordMessage);

    internal static bool IsCommon(string? password, IEnumerable<string?>? personal = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var folded = Fold(password);
        if (IsRun(folded) || folded.Distinct().Count() <= 3)
        {
            return true;
        }

        // The word left after peeling digits and symbols off both ends ("Galatasaray1905!" -> "galatasaray").
        var core = folded.Trim("0123456789!@#$%^&*.,-_+=?/ ".ToCharArray());
        if (core.Length == 0 || CommonWords.Contains(core) || IsRun(core))
        {
            return true;
        }

        return personal is not null && personal
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Fold(p!).Replace(" ", string.Empty, StringComparison.Ordinal))
            .Any(p => p.Length >= 3 && (core == p || core.Contains(p, StringComparison.Ordinal) && core.Length - p.Length <= 2));
    }

    private static bool IsRun(string value) =>
        value.Length >= 6 && Runs.Any(r => r.Contains(value, StringComparison.Ordinal)
            || new string([.. r.Reverse()]).Contains(value, StringComparison.Ordinal));

    /// <summary>
    /// Lower case without Turkish letters, so "Şifre", "sifre" and "ŞİFRE" are one word. Culture-free: the host runs
    /// in globalization-invariant mode.
    /// </summary>
    private static string Fold(string value) =>
        new([.. value.ToLowerInvariant().Select(c => c switch
        {
            'ç' or 'Ç' => 'c',
            'ğ' or 'Ğ' => 'g',
            'ı' or 'İ' => 'i',
            'ö' or 'Ö' => 'o',
            'ş' or 'Ş' => 's',
            'ü' or 'Ü' => 'u',
            _ => c,
        })]);
}
