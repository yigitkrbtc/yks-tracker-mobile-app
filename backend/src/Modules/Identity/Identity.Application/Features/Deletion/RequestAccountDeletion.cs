using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Tokens;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Deletion;

/// <summary>Starts the grace period: every device is signed out, data is purged when it ends unless restored.</summary>
public sealed record RequestAccountDeletionCommand(Guid UserId, string Password) : ICommand<DeletionScheduled>;

public sealed record DeletionScheduled(DateTimeOffset PurgeAt);

internal sealed class RequestAccountDeletionValidator : AbstractValidator<RequestAccountDeletionCommand>
{
    public RequestAccountDeletionValidator() => RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
}

internal sealed class RequestAccountDeletionHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    SignInService signIn,
    IEmailSender email,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentityOptions options) : ICommandHandler<RequestAccountDeletionCommand, DeletionScheduled>
{
    public async Task<Result<DeletionScheduled>> Handle(RequestAccountDeletionCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetById(command.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return IdentityErrors.UserNotFound;
        }

        // Same lockout as sign-in, so a stolen access token cannot be used to guess the password.
        var now = clock.UtcNow;
        if (user.IsLockedOut(now))
        {
            return IdentityErrors.InvalidPassword;
        }

        if (hasher.Verify(user.PasswordHash, command.Password) == PasswordCheck.Failed)
        {
            user.RecordFailedLogin(now, options.LockoutThreshold, options.LockoutDuration);
            await unitOfWork.SaveChanges(cancellationToken);
            return IdentityErrors.InvalidPassword;
        }

        user.RequestDeletion(now);
        await signIn.RevokeAll(user.Id, RevokeReason.AccountDeletion, now, cancellationToken);
        await unitOfWork.SaveChanges(cancellationToken);

        var purgeAt = user.PurgeAt(options.DeletionGracePeriod)!.Value;
        await email.Send(IdentityEmails.DeletionScheduled(user.Email, user.FirstName, purgeAt), cancellationToken);
        return new DeletionScheduled(purgeAt);
    }
}
