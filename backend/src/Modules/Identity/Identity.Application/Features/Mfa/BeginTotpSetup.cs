using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;

namespace Nirengi.Modules.Identity.Application.Features.Mfa;

/// <summary>Creates a pending authenticator secret for an admin who has none yet; calling again replaces it.</summary>
public sealed record BeginTotpSetupCommand(string MfaToken) : ICommand<TotpSetup>;

/// <param name="OtpAuthUri">Render as a QR code; <paramref name="Secret"/> is the manual-entry fallback.</param>
public sealed record TotpSetup(string Secret, string OtpAuthUri);

internal sealed class BeginTotpSetupValidator : AbstractValidator<BeginTotpSetupCommand>
{
    public BeginTotpSetupValidator() => RuleFor(c => c.MfaToken).NotEmpty().MaximumLength(2000);
}

internal sealed class BeginTotpSetupHandler(
    MfaGate gate,
    ITotpAuthenticator totp,
    ISecretProtector protector,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<BeginTotpSetupCommand, TotpSetup>
{
    public async Task<Result<TotpSetup>> Handle(BeginTotpSetupCommand command, CancellationToken cancellationToken)
    {
        var opened = await gate.Open(command.MfaToken, MfaStep.TotpSetup, clock.UtcNow, cancellationToken);
        if (opened.IsFailure)
        {
            return opened.Error;
        }

        var user = opened.Value;
        var secret = totp.NewSecret();
        var started = user.StartTotpEnrollment(protector.Protect(secret));
        if (started.IsFailure)
        {
            return started.Error;
        }

        await unitOfWork.SaveChanges(cancellationToken);
        return new TotpSetup(secret, totp.ProvisioningUri(secret, user.Email));
    }
}
