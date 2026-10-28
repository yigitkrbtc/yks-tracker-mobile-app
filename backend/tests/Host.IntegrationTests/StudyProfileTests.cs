using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Identity.Application.Features.Deletion;
using Nirengi.Modules.Identity.Infrastructure;
using Nirengi.Modules.Tracking.Domain.Profiles;
using Nirengi.Modules.Tracking.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class StudyProfileTests(NirengiApiFactory factory)
{
    private static readonly int NextExam = ExamCalendar.NextExamYear(DateTimeOffset.UtcNow);
    private readonly AuthApi _api = new(factory);

    [Fact]
    public async Task Profile_is_missing_until_onboarding_then_saved_and_replaced()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        await AuthApi.ShouldBeProblem(await _api.Send(HttpMethod.Get, "/v1/profile", tokens.AccessToken), HttpStatusCode.NotFound, "tracking.profile_not_found");

        var saved = await _api.Send(HttpMethod.Put, "/v1/profile", tokens.AccessToken, Profile("Twelfth", "SAY", secondary: "EA"));
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);

        var read = await AuthApi.Read<ProfileItem>(await _api.Send(HttpMethod.Get, "/v1/profile", tokens.AccessToken));
        read.ShouldBe(new ProfileItem("Twelfth", "SAY", "EA", null, NextExam, 300));

        (await _api.Send(HttpMethod.Put, "/v1/profile", tokens.AccessToken, Profile("Graduate", "DIL", language: "German"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var replaced = await AuthApi.Read<ProfileItem>(await _api.Send(HttpMethod.Get, "/v1/profile", tokens.AccessToken));
        replaced.ShouldBe(new ProfileItem("Graduate", "DIL", null, "German", NextExam, 300));
    }

    [Theory]
    [InlineData("SAY", "SAY", null, 0)]
    [InlineData("DIL", null, null, 0)]
    [InlineData("EA", null, "English", 0)]
    [InlineData("SAY", null, null, -1)]
    [InlineData("SAY", null, null, ExamCalendar.MaxYearsAhead + 1)]
    public async Task Inconsistent_profile_is_rejected(string primary, string? secondary, string? language, int yearOffset)
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var response = await _api.Send(HttpMethod.Put, "/v1/profile", tokens.AccessToken, Profile("Eleventh", primary, secondary, language, NextExam + yearOffset));

        await AuthApi.ShouldBeProblem(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Missing_score_type_is_rejected_not_defaulted()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var response = await _api.Send(HttpMethod.Put, "/v1/profile", tokens.AccessToken, new { grade = "Twelfth", targetExamYear = NextExam });

        await AuthApi.ShouldBeProblem(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Purged_account_loses_its_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = AuthApi.NewEmail();
        var tokens = await _api.RegisterAndVerify(email);
        (await _api.Send(HttpMethod.Put, "/v1/profile", tokens.AccessToken, Profile("Eleventh", "EA"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.Send(HttpMethod.Post, "/v1/me/deletion", tokens.AccessToken, new { password = AuthApi.Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await using var scope = factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await identity.Users.AsNoTracking().SingleAsync(u => u.Email == email, ct)).Id;
        await identity.Database.ExecuteSqlAsync(
            $"UPDATE identity.users SET deletion_requested_at = now() - interval '31 days' WHERE id = {userId}", ct);
        (await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new PurgeDeletedAccountsCommand(), ct)).IsSuccess.ShouldBeTrue();

        // UserDeleted travels through Identity's outbox (polled every 200 ms in tests).
        var tracking = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var exists = true;
        for (var i = 0; i < 50 && exists; i++)
        {
            await Task.Delay(100, ct);
            exists = await tracking.StudyProfiles.AsNoTracking().AnyAsync(p => p.Id == userId, ct);
        }

        exists.ShouldBeFalse();
    }

    private static object Profile(string grade, string primary, string? secondary = null, string? language = null, int? year = null) => new
    {
        grade,
        primaryScoreType = primary,
        secondaryScoreType = secondary,
        ydtLanguage = language,
        targetExamYear = year ?? NextExam,
        weeklyQuestionGoal = 300,
    };

    private sealed record ProfileItem(string Grade, string PrimaryScoreType, string? SecondaryScoreType, string? YdtLanguage, int TargetExamYear, int? WeeklyQuestionGoal);
}
