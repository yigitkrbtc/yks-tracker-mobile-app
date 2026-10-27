using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Contracts;

namespace Nirengi.Modules.Identity.Application.Features.Deletion;

/// <summary>Hard-deletes accounts whose grace period ended and announces each with <see cref="UserDeleted"/>.</summary>
public sealed record PurgeDeletedAccountsCommand(int BatchSize = 100) : ICommand<int>;

internal sealed class PurgeDeletedAccountsHandler(
    IUserRepository users,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentityOptions options) : ICommandHandler<PurgeDeletedAccountsCommand, int>
{
    public async Task<Result<int>> Handle(PurgeDeletedAccountsCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = await users.GetDeletionRequestedBefore(now - options.DeletionGracePeriod, command.BatchSize, cancellationToken);
        foreach (var user in due)
        {
            unitOfWork.Enqueue(new UserDeleted(Guid.CreateVersion7(now), now, user.Id));
            users.Remove(user);
        }

        await unitOfWork.SaveChanges(cancellationToken);
        return due.Count;
    }
}
