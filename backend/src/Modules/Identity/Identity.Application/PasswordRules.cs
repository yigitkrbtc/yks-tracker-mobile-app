using FluentValidation;

namespace Nirengi.Modules.Identity.Application;

internal static class PasswordRules
{
    public const int MinLength = 10;

    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty()
        .Length(MinLength, MaxLength)
        .Matches(@"\p{L}").WithMessage("Şifre en az bir harf içermeli.")
        .Matches(@"\d").WithMessage("Şifre en az bir rakam içermeli.");
}
