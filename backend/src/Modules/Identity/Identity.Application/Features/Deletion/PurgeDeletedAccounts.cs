using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Contracts;

namespace Nirengi.Modules.Identity.Application.Features.Deletion;

/// <summary>
/// Hard-deletes accounts whose deletion grace period ended and sign-ups never verified within the retention
/// period, announcing each with <see cref="UserDeleted"/>. The same hourly run also deletes refresh tokens that
/// expired more than <see cref="PurgeDeletedAccountsHandler.ExpiredTokenRetention"/> ago; the result counts accounts only.
/// </summary>
public sealed record PurgeDeletedAccountsCommand(int BatchSize = 100) : ICommand<int>;

internal sealed class PurgeDeletedAccountsHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentityOptions options) : ICommandHandler<PurgeDeletedAccountsCommand, int>
{
    /// <summary>Only expiry counts: a revoked token that has not expired yet is what reuse detection and the grace chain read.</summary>
    internal static readonly TimeSpan ExpiredTokenRetention = TimeSpan.FromDays(7);

    public async Task<Result<int>> Handle(PurgeDeletedAccountsCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = (await users.GetDeletionRequestedBefore(now - options.DeletionGracePeriod, command.BatchSize, cancellationToken))
            .Concat(await users.GetUnverifiedCreatedBefore(now - options.UnverifiedRetention, command.BatchSize, cancellationToken))
            .ToList();
        foreach (var user in due)
        {
            unitOfWork.Enqueue(new UserDeleted(Guid.CreateVersion7(now), now, user.Id));
            users.Remove(user);
        }

        await unitOfWork.SaveChanges(cancellationToken);
        await refreshTokens.DeleteExpiredBefore(now - ExpiredTokenRetention, cancellationToken);
        return due.Count;
    }
}
