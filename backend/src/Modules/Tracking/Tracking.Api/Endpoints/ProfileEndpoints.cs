using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Tracking.Application.Features.Profile;
using Nirengi.Modules.Tracking.Domain.Profiles;

namespace Nirengi.Modules.Tracking.Api.Endpoints;

public sealed record SaveStudyProfileRequest(
    Grade? Grade,
    ScoreType? PrimaryScoreType,
    ScoreType? SecondaryScoreType,
    YdtLanguage? YdtLanguage,
    int? TargetExamYear,
    int? WeeklyQuestionGoal);

internal static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder v1)
    {
        var profile = v1.MapGroup("/profile").RequireAuthorization();

        profile.MapGet("/", async (ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Query(new GetStudyProfileQuery(user.UserId), ct)).Match(Results.Ok))
            .WithName("GetStudyProfile")
            .Produces<StudyProfileResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        profile.MapPut("/", async (SaveStudyProfileRequest request, ICurrentUser user, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(
                    new SaveStudyProfileCommand(
                        user.UserId,
                        request.Grade,
                        request.PrimaryScoreType,
                        request.SecondaryScoreType,
                        request.YdtLanguage,
                        request.TargetExamYear,
                        request.WeeklyQuestionGoal),
                    ct)).Match(Results.Ok))
            .WithName("SaveStudyProfile")
            .Produces<StudyProfileResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
