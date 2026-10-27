using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.SignIn;

public sealed record LoginCommand(string Email, string Password) : ICommand<SignInResult>;

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}

internal sealed class LoginHandler(
    CredentialChecker credentials,
    SignInService signIn,
    IIdentityUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<LoginCommand, SignInResult>
{
    public async Task<Result<SignInResult>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var checkedUser = await credentials.Check(command.Email, command.Password, now, cancellationToken);
        if (checkedUser.IsFailure)
        {
            return checkedUser.Error;
        }

        // States below are revealed only to someone who knows the password.
        var user = checkedUser.Value;
        Result<SignInResult> result = user.Status switch
        {
            UserStatus.PendingVerification => IdentityErrors.EmailNotVerified,
            UserStatus.DeletionRequested => IdentityErrors.DeletionPending,
            _ => signIn.Complete(user, now),
        };

        await unitOfWork.SaveChanges(cancellationToken);
        return result;
    }
}
