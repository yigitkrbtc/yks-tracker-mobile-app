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

/// <summary>Confirms the mailbox with the 6-digit code and signs the user in.</summary>
public sealed record VerifyEmailCommand(string Email, string Code) : ICommand<SignInResult>;

internal sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
        RuleFor(c => c.Code).NotEmpty().Length(VerificationCodes.Digits).Matches("^[0-9]+$");
    }
}

internal sealed class VerifyEmailHandler(
    IUserRepository users,
    VerificationCodes codes,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<VerifyEmailCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmail(Email.Normalize(command.Email), cancellationToken);
        if (user is null || user.Status != UserStatus.PendingVerification)
        {
            return IdentityErrors.InvalidCode;
        }

        var now = clock.UtcNow;
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
