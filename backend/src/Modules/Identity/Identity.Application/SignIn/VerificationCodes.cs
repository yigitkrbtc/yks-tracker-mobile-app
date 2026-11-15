using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.SignIn;

/// <summary>A freshly issued code: the plain value to mail and what ties that mail to this very code.</summary>
internal sealed record IssuedCode(string Code, EmailCodeBinding Binding);

/// <summary>Issues and checks 8-digit codes. The hash binds user and purpose, so a code never works elsewhere.</summary>
internal sealed class VerificationCodes(
    IVerificationCodeRepository codes,
    IOneTimeSecrets secrets,
    IdentityOptions options)
{
    public const int Digits = 8;

    /// <summary>Returns the code to mail, or null while the resend cooldown is running.</summary>
    public async Task<IssuedCode?> Issue(User user, CodePurpose purpose, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var code = secrets.NewNumericCode(Digits);
        var hash = Hash(user.Id, purpose, code);
        var existing = await codes.Get(user.Id, purpose, cancellationToken);
        if (existing is null)
        {
            codes.Add(VerificationCode.Issue(user.Id, purpose, hash, now, options.CodeLifetime));
            return new IssuedCode(code, new EmailCodeBinding(user.Id, purpose, hash));
        }

        if (!existing.CanResend(now, options.CodeResendCooldown))
        {
            return null;
        }

        existing.Reissue(hash, now, options.CodeLifetime);
        return new IssuedCode(code, new EmailCodeBinding(user.Id, purpose, hash));
    }

    /// <summary>Checks a code; failed attempts are counted on the entity, so callers save either way.</summary>
    public async Task<Result> Verify(User user, CodePurpose purpose, string code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await codes.Get(user.Id, purpose, cancellationToken);
        if (existing is null)
        {
            return Domain.IdentityErrors.CodeExpired;
        }

        return existing.Verify(Hash(user.Id, purpose, code), now, options.MaxCodeAttempts);
    }

    private string Hash(Guid userId, CodePurpose purpose, string code) => secrets.Hash($"{userId:N}:{purpose}:{code}");
}
