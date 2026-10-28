using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Domain;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application.Features.Profile;

public sealed record GetMeQuery(Guid UserId) : IQuery<UserProfile>;

/// <param name="CanGiveMarketingConsent">False under 18: the client must not show the marketing opt-in.</param>
public sealed record UserProfile(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    int BirthYear,
    UserRole Role,
    bool MarketingConsent,
    bool CanGiveMarketingConsent);

internal sealed class GetMeHandler(IUserRepository users, IClock clock) : IQueryHandler<GetMeQuery, UserProfile>
{
    public async Task<Result<UserProfile>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetById(query.UserId, cancellationToken);

        // An access token outlives a deletion request by up to its lifetime; the account is gone for the client.
        if (user is null || user.Status != UserStatus.Active)
        {
            return IdentityErrors.UserNotFound;
        }

        return new UserProfile(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.BirthYear,
            user.Role,
            user.MarketingConsent,
            AgePolicy.IsAdult(user.BirthYear, AgePolicy.CurrentYear(clock.UtcNow)));
    }
}
