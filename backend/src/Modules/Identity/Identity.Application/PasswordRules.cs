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

    /// <summary>The characters people put around a word: digits and common symbols.</summary>
    private static readonly char[] Peelable = [.. "0123456789!@#$%^&*.,-_+=?/ "];

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

        // The word left after peeling digits and symbols off both ends ("Galatasaray1905!" -> "galatasaray"), read the
        // way people leet-spell it ("P@ssw0rd1" -> "password"). A digit at an end may be part of the word ("S1fr3-2024"),
        // so every amount of peeling is tried. Leet folding is for word matching only.
        var names = personal is null
            ? []
            : personal
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => LeetFold(Fold(p!).Replace(" ", string.Empty, StringComparison.Ordinal)))
                .Where(p => p.Length >= 3)
                .ToList();

        var nothingButPeel = folded.Trim(Peelable).Length == 0;
        return nothingButPeel || Cores(folded).Any(core =>
            CommonWords.Contains(core)
            || IsRun(core)
            || names.Any(p => core == p || (core.Contains(p, StringComparison.Ordinal) && core.Length - p.Length <= 2)));
    }

    /// <summary>Leet-folded cores of <paramref name="folded"/>: the word between 0..n peeled characters at each end.</summary>
    private static IEnumerable<string> Cores(string folded)
    {
        var lead = folded.Length - folded.TrimStart(Peelable).Length;
        var trail = folded.Length - folded.TrimEnd(Peelable).Length;
        var wordLength = folded.Length - lead - trail;
        if (wordLength <= 0)
        {
            yield break;
        }

        for (var start = 0; start <= lead; start++)
        {
            for (var end = folded.Length - trail; end <= folded.Length; end++)
            {
                yield return LeetFold(folded[start..end]);
            }
        }
    }

    private static bool IsRun(string value) =>
        value.Length >= 6 && Runs.Any(r => r.Contains(value, StringComparison.Ordinal)
            || new string([.. r.Reverse()]).Contains(value, StringComparison.Ordinal));

    private static string LeetFold(string value) =>
        new([.. value.Select(c => c switch
        {
            '0' => 'o',
            '1' => 'i',
            '3' => 'e',
            '4' => 'a',
            '5' => 's',
            '7' => 't',
            '@' => 'a',
            '$' => 's',
            _ => c,
        })]);

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
