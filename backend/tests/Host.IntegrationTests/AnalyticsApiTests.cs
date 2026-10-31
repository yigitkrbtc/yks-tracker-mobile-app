using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Analytics.Infrastructure;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Identity.Contracts;
using Nirengi.Modules.Tracking.Domain.TopicTests;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Analysis read model: recompute on change and on read, ETag, per-user isolation, deletion (S5, ADR-0016).</summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class AnalyticsApiTests(NirengiApiFactory factory)
{
    private static readonly Guid Paragraph = TopicIdentity.For("TYT.TUR.04");
    private static readonly Guid History = TopicIdentity.For("TYT.TAR.01");
    private static readonly Guid Publisher = SourceIdentity.For("OSYM");
    private readonly AuthApi _api = new(factory);

    private static DateOnly Today => TopicTestRules.TodayInTurkey(DateTimeOffset.UtcNow);

    [Fact]
    public async Task New_student_gets_an_empty_panel_and_a_304_for_the_same_etag()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var response = await _api.Send(HttpMethod.Get, "/v1/analytics/overview", tokens.AccessToken);
        var overview = await AuthApi.Read<OverviewItem>(response);

        overview.Topics.ShouldBeEmpty();
        overview.Sections.ShouldBeEmpty();
        overview.Priorities.ShouldBeEmpty();
        overview.LowDataTopics.Count.ShouldBe(2);
        overview.RuleSetVersion.ShouldBe("2026.1");
        overview.Tests.ShouldContain(t => t.Code == "TYT-TUR");
        response.Headers.ETag!.Tag.ShouldBe(overview.ETag);

        var again = await _api.Send(HttpMethod.Get, "/v1/analytics/overview", tokens.AccessToken, ifNoneMatch: overview.ETag);
        again.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Topic_tests_and_mock_exams_show_up_and_a_delete_takes_them_out()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var before = await Overview(tokens);
        var test = Guid.CreateVersion7();
        await PutTest(tokens, test, Today, (Paragraph, 20, 14, 5, 1));
        await PutTest(tokens, Guid.CreateVersion7(), Today.AddDays(-3), (Paragraph, 20, 12, 6, 2));
        await PutMock(tokens, Today.AddDays(-1));

        var overview = await Overview(tokens);

        overview.ETag.ShouldNotBe(before.ETag);
        var paragraph = overview.Topics.ShouldHaveSingleItem();
        paragraph.TopicId.ShouldBe(Paragraph);
        paragraph.Mastery!.Value.ShouldBeInRange(0.6, 0.75);
        paragraph.ConfidenceLevel.ShouldBe("Reasonable");
        overview.Sections.Select(s => s.Code).Order(StringComparer.Ordinal).ShouldBe(["TYT-SOS", "TYT-SOS.TAR", "TYT-TUR"]);
        overview.Sections.ShouldAllBe(s => !s.IsSufficient);

        var card = await Card(tokens, Paragraph);
        card.Analysis.Totals.ShouldBe(new Totals(2, 40, 26, 11, 3));
        card.Analysis.Recent.Count.ShouldBe(2);
        card.Analysis.Recent[0].Date.ShouldBe(Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        card.Analysis.Band.ShouldBe("Developing");

        (await _api.Send(HttpMethod.Delete, $"/v1/topic-tests/{test}?modifiedAt={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}", tokens.AccessToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Card(tokens, Paragraph)).Analysis.Totals.Records.ShouldBe(1);
    }

    [Fact]
    public async Task Topic_without_records_is_grey_and_unknown_topic_is_not_found()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var card = await Card(tokens, History);

        card.Analysis.ConfidenceLevel.ShouldBe("None");
        card.Analysis.Mastery.ShouldBeNull();
        card.Analysis.QuestionsToNextLevel.ShouldBe(11);
        await AuthApi.ShouldBeProblem(
            await _api.Send(HttpMethod.Get, $"/v1/analytics/topics/{Guid.NewGuid()}", tokens.AccessToken),
            HttpStatusCode.NotFound,
            "analytics.topic_not_found");
    }

    [Fact]
    public async Task A_student_never_sees_another_students_analysis()
    {
        var owner = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var other = await _api.RegisterAndVerify(AuthApi.NewEmail());
        await PutTest(owner, Guid.CreateVersion7(), Today, (History, 10, 7, 3, 0));

        (await Overview(owner)).Topics.ShouldHaveSingleItem();
        (await Overview(other)).Topics.ShouldBeEmpty();
        (await Card(other, History)).Analysis.Totals.Records.ShouldBe(0);
    }

    [Fact]
    public async Task Profile_score_types_decide_which_tests_are_active()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        (await _api.Send(HttpMethod.Put, "/v1/profile", tokens.AccessToken, new { Grade = "Twelfth", PrimaryScoreType = "SAY", TargetExamYear = Today.Month <= 6 ? Today.Year : Today.Year + 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var overview = await Overview(tokens);

        overview.Tests.Single(t => t.Code == "AYT-FEN").IsActive.ShouldBeTrue();
        overview.Tests.Single(t => t.Code == "AYT-SB2").IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Change_event_recomputes_without_a_read_and_user_deletion_removes_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var me = await AuthApi.Read<AuthApi.Profile>(await _api.Send(HttpMethod.Get, "/v1/me", tokens.AccessToken));
        await PutTest(tokens, Guid.CreateVersion7(), Today, (History, 10, 7, 3, 0));

        var stored = false;
        for (var i = 0; i < 50 && !stored; i++)
        {
            await Task.Delay(200, ct);
            await using var scope = factory.Services.CreateAsyncScope();
            stored = await scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().TopicSnapshots
                .AnyAsync(t => t.UserId == me.Id && t.TopicId == History, ct);
        }

        stored.ShouldBeTrue();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler<UserDeleted>>())
            {
                await handler.Handle(new UserDeleted(Guid.NewGuid(), DateTimeOffset.UtcNow, me.Id), ct);
            }

            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            (await db.UserAnalyses.AnyAsync(a => a.UserId == me.Id, ct)).ShouldBeFalse();
            (await db.TopicSnapshots.AnyAsync(t => t.UserId == me.Id, ct)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Analytics_requires_sign_in()
    {
        var response = await _api.Client.GetAsync("/v1/analytics/overview", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<OverviewItem> Overview(AuthApi.Tokens tokens) =>
        await AuthApi.Read<OverviewItem>(await _api.Send(HttpMethod.Get, "/v1/analytics/overview", tokens.AccessToken));

    private async Task<CardResponse> Card(AuthApi.Tokens tokens, Guid topic) =>
        await AuthApi.Read<CardResponse>(await _api.Send(HttpMethod.Get, $"/v1/analytics/topics/{topic}", tokens.AccessToken));

    private async Task PutTest(AuthApi.Tokens tokens, Guid id, DateOnly day, (Guid Topic, int Total, int Correct, int Wrong, int Blank) result)
    {
        var body = new
        {
            StudiedOn = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SourceId = Publisher,
            IsRepeat = false,
            Results = new[] { new { TopicId = result.Topic, result.Total, result.Correct, result.Wrong, result.Blank } },
            ClientModifiedAt = DateTimeOffset.UtcNow,
        };
        (await _api.Send(HttpMethod.Put, $"/v1/topic-tests/{id}", tokens.AccessToken, body)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task PutMock(AuthApi.Tokens tokens, DateOnly day)
    {
        var body = new
        {
            Kind = "Tyt",
            TakenOn = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SourceId = Publisher,
            Sections = new[] { new { SectionCode = "TYT-TUR", Correct = 30, Wrong = 8 }, new { SectionCode = "TYT-SOS.TAR", Correct = 3, Wrong = 1 } },
            Marks = new[] { new { SectionCode = "TYT-SOS.TAR", TopicId = History, Wrong = 1, Blank = 1 } },
            ClientModifiedAt = DateTimeOffset.UtcNow,
        };
        (await _api.Send(HttpMethod.Put, $"/v1/mock-exams/{Guid.CreateVersion7()}", tokens.AccessToken, body)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed record Aggregate(string Code, bool IsActive, double Mastery, double Coverage);

    private sealed record PriorityItem(Guid TopicId, double Priority);

    private sealed record Section(string Code, int Exams, bool IsSufficient, double Accuracy);

    private sealed record TopicStatus(Guid TopicId, double? Mastery, string ConfidenceLevel, string Band, string Trend);

    private sealed record OverviewItem(
        string ETag,
        string RuleSetVersion,
        List<Aggregate> Tests,
        List<Aggregate> Subjects,
        List<PriorityItem> Priorities,
        List<Guid> LowDataTopics,
        List<Section> Sections,
        List<TopicStatus> Topics);

    private sealed record Totals(int Records, int Questions, int Correct, int Wrong, int Blank);

    private sealed record Recent(string Date, int Total, int Correct);

    private sealed record Details(double? Mastery, string ConfidenceLevel, int? QuestionsToNextLevel, string Band, Totals Totals, List<Recent> Recent);

    private sealed record CardResponse(Guid TopicId, Details Analysis);
}
