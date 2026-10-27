using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Mfa;

/// <summary>Second admin sign-in step: an authenticator code or one unused recovery code.</summary>
public sealed record CompleteTotpSignInCommand(string MfaToken, string? Code, string? RecoveryCode) : ICommand<AuthTokens>;

internal sealed class CompleteTotpSignInValidator : AbstractValidator<CompleteTotpSignInCommand>
{
    public CompleteTotpSignInValidator()
    {
        RuleFor(c => c.MfaToken).NotEmpty().MaximumLength(2000);
        RuleFor(c => c).Must(c => string.IsNullOrEmpty(c.Code) != string.IsNullOrEmpty(c.RecoveryCode))
            .WithName(nameof(CompleteTotpSignInCommand.Code))
            .WithMessage("Doğrulama kodu ya da kurtarma kodu gir (ikisinden biri).");
        RuleFor(c => c.Code).Length(6).Matches("^[0-9]+$").When(c => !string.IsNullOrEmpty(c.Code));
        RuleFor(c => c.RecoveryCode).MaximumLength(20).When(c => !string.IsNullOrEmpty(c.RecoveryCode));
    }
}

internal sealed class CompleteTotpSignInHandler(
    MfaGate gate,
    ITotpAuthenticator totp,
    ISecretProtector protector,
    IRecoveryCodeRepository recoveryCodes,
    IOneTimeSecrets secrets,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentityOptions options) : ICommandHandler<CompleteTotpSignInCommand, AuthTokens>
{
    public async Task<Result<AuthTokens>> Handle(CompleteTotpSignInCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var opened = await gate.Open(command.MfaToken, MfaStep.Totp, now, cancellationToken);
        if (opened.IsFailure)
        {
            return opened.Error;
        }

        var user = opened.Value;
        var accepted = string.IsNullOrEmpty(command.Code)
            ? await UseRecoveryCode(user, command.RecoveryCode!, now, cancellationToken)
            : UseAuthenticatorCode(user, command.Code, now);

        if (!accepted)
        {
            // Wrong second factors count toward the same lockout as wrong passwords.
            user.RecordFailedLogin(now, options.LockoutThreshold, options.LockoutDuration);
            await unitOfWork.SaveChanges(cancellationToken);
            return IdentityErrors.InvalidTotpCode;
        }

        user.RecordSuccessfulLogin();
        var tokens = signIn.IssueTokens(user, now);
        await unitOfWork.SaveChanges(cancellationToken);
        return tokens;
    }

    private bool UseAuthenticatorCode(User user, string code, DateTimeOffset now)
    {
        var step = totp.Verify(protector.Unprotect(user.TotpSecret!), code, now);
        return step is not null && user.AcceptTotpStep(step.Value).IsSuccess;
    }

    private async Task<bool> UseRecoveryCode(User user, string code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hash = secrets.Hash(RecoveryCodes.Normalize(code));
        foreach (var candidate in await recoveryCodes.GetUnused(user.Id, cancellationToken))
        {
            if (candidate.TryUse(hash, now))
            {
                return true;
            }
        }

        return false;
    }
}
