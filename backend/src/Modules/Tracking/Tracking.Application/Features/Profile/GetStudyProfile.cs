using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Domain;

namespace Nirengi.Modules.Tracking.Application.Features.Profile;

/// <summary>Not found means onboarding has not been completed yet.</summary>
public sealed record GetStudyProfileQuery(Guid UserId) : IQuery<StudyProfileResponse>;

internal sealed class GetStudyProfileHandler(IStudyProfileRepository profiles) : IQueryHandler<GetStudyProfileQuery, StudyProfileResponse>
{
    public async Task<Result<StudyProfileResponse>> Handle(GetStudyProfileQuery query, CancellationToken cancellationToken)
    {
        var profile = await profiles.Get(query.UserId, cancellationToken);
        return profile is null ? TrackingErrors.ProfileNotFound : StudyProfileResponse.From(profile);
    }
}
