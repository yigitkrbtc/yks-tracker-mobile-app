using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Users;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Application.Features.Deletion;

/// <summary>Cancels a pending deletion with the account password and signs the user back in.</summary>
public sealed record RestoreAccountCommand(string Email, string Password) : ICommand<SignInResult>;

internal sealed class RestoreAccountValidator : AbstractValidator<RestoreAccountCommand>
{
    public RestoreAccountValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}

internal sealed class RestoreAccountHandler(
    CredentialChecker credentials,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<RestoreAccountCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(RestoreAccountCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var checkedUser = await credentials.Check(command.Email, command.Password, now, cancellationToken);
        if (checkedUser.IsFailure)
        {
            return checkedUser.Error;
        }

        var user = checkedUser.Value;
        if (user.Status != UserStatus.DeletionRequested)
        {
            return IdentityErrors.NotPendingDeletion;
        }

        user.CancelDeletion();
        var result = signIn.Complete(user, now);
        await unitOfWork.SaveChanges(cancellationToken);
        return result;
    }
}
