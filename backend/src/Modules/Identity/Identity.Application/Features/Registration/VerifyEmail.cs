using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Registration;

/// <summary>
/// Confirms the mailbox with the 6-digit code and signs the user in. The password chosen at sign-up must come along:
/// a later sign-up for the same address replaces it, and the code in the mailbox must not then activate the account
/// with someone else's password (ADR-0021, audit G1).
/// </summary>
public sealed record VerifyEmailCommand(string Email, string Code, string Password) : ICommand<SignInResult>;

internal sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
        RuleFor(c => c.Code).NotEmpty().Length(VerificationCodes.Digits).Matches("^[0-9]+$");
        RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}

internal sealed class VerifyEmailHandler(
    CredentialChecker credentials,
    VerificationCodes codes,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<VerifyEmailCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        // Wrong passwords count toward the same lockout as sign-in.
        var checkedUser = await credentials.Check(command.Email, command.Password, now, cancellationToken);
        if (checkedUser.IsFailure)
        {
            return checkedUser.Error;
        }

        var user = checkedUser.Value;
        if (user.Status != UserStatus.PendingVerification)
        {
            return IdentityErrors.InvalidCode;
        }

        var verified = await codes.Verify(user, CodePurpose.EmailVerification, command.Code, now, cancellationToken);
        if (verified.IsFailure)
        {
            await unitOfWork.SaveChanges(cancellationToken);
            return verified.Error;
        }

        user.VerifyEmail(now);
        var result = signIn.Complete(user, now);
        await unitOfWork.SaveChanges(cancellationToken);
        return result;
    }
}
