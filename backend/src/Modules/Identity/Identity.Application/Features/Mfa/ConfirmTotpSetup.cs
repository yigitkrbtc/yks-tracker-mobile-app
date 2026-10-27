using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Tokens;

namespace Nirengi.Modules.Identity.Application.Features.Mfa;

/// <summary>Proves the authenticator works, enables TOTP and returns the recovery codes (shown only now).</summary>
public sealed record ConfirmTotpSetupCommand(string MfaToken, string Code) : ICommand<TotpEnrollment>;

public sealed record TotpEnrollment(AuthTokens Tokens, IReadOnlyList<string> RecoveryCodes);

internal sealed class ConfirmTotpSetupValidator : AbstractValidator<ConfirmTotpSetupCommand>
{
    public ConfirmTotpSetupValidator()
    {
        RuleFor(c => c.MfaToken).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.Code).NotEmpty().Length(6).Matches("^[0-9]+$");
    }
}

internal sealed class ConfirmTotpSetupHandler(
    MfaGate gate,
    ITotpAuthenticator totp,
    ISecretProtector protector,
    IRecoveryCodeRepository recoveryCodes,
    IOneTimeSecrets secrets,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ConfirmTotpSetupCommand, TotpEnrollment>
{
    public async Task<Result<TotpEnrollment>> Handle(ConfirmTotpSetupCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var opened = await gate.Open(command.MfaToken, MfaStep.TotpSetup, now, cancellationToken);
        if (opened.IsFailure)
        {
            return opened.Error;
        }

        var user = opened.Value;
        if (user.PendingTotpSecret is null)
        {
            return IdentityErrors.TotpSetupNotStarted;
        }

        var step = totp.Verify(protector.Unprotect(user.PendingTotpSecret), command.Code, now);
        if (step is null)
        {
            return IdentityErrors.InvalidTotpCode;
        }

        var confirmed = user.ConfirmTotpEnrollment(step.Value, now);
        if (confirmed.IsFailure)
        {
            return confirmed.Error;
        }

        var codes = Enumerable.Range(0, RecoveryCode.CountPerUser).Select(_ => secrets.NewRecoveryCode()).ToList();
        foreach (var code in codes)
        {
            recoveryCodes.Add(RecoveryCode.Create(user.Id, secrets.Hash(RecoveryCodes.Normalize(code)), now));
        }

        user.RecordSuccessfulLogin();
        var tokens = signIn.IssueTokens(user, now);
        await unitOfWork.SaveChanges(cancellationToken);
        return new TotpEnrollment(tokens, codes);
    }
}
