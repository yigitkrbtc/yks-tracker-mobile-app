using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Identity.Contracts;
using Nirengi.Modules.Recommendation.Infrastructure;
using Nirengi.Modules.Tracking.Domain.TopicTests;
using Nirengi.Modules.Tracking.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>S6: exam calendar, topic progress marks, weekly plan and personal data export (ADR-0017, ADR-0018).</summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class StudentToolsApiTests(NirengiApiFactory factory)
{
    private static readonly Guid Paragraph = TopicIdentity.For("TYT.TUR.04");
    private static readonly Guid History = TopicIdentity.For("TYT.TAR.01");
    private static readonly Guid Publisher = SourceIdentity.For("OSYM");
    private readonly AuthApi _api = new(factory);

    private static DateOnly Today => TopicTestRules.TodayInTurkey(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Exam_calendar_is_public_and_cached_by_etag()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _api.Client.GetAsync("/v1/curriculum/exam-calendar", ct);
        var calendar = await AuthApi.Read<CalendarResponse>(response);

        var year = calendar.Calendars.ShouldHaveSingleItem();
        year.ExamYear.ShouldBe(2027);
        year.Events.Select(e => e.Code).ShouldBe(["tyt", "ayt", "ydt"]);
        year.Events.ShouldAllBe(e => e.Status == "Estimated");

        using var again = new HttpRequestMessage(HttpMethod.Get, "/v1/curriculum/exam-calendar");
        again.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        (await _api.Client.SendAsync(again, ct)).StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Topic_progress_is_last_writer_wins_and_syncs()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var first = DateTimeOffset.UtcNow.AddMinutes(-5);

        var saved = await AuthApi.Read<ProgressItem>(await PutProgress(tokens, History, "InProgress", first));
        saved.Status.ShouldBe("InProgress");

        // An older edit loses: the server answers with what it keeps.
        var stale = await AuthApi.Read<ProgressItem>(await PutProgress(tokens, History, "Done", first.AddMinutes(-1)));
        stale.ShouldBe(saved);

        var done = await AuthApi.Read<ProgressItem>(await PutProgress(tokens, History, "Done", first.AddMinutes(1)));
        done.ChangeSeq.ShouldBeGreaterThan(saved.ChangeSeq);

        var sync = await AuthApi.Read<SyncItem>(await _api.Send(HttpMethod.Get, "/v1/sync?since=0", tokens.AccessToken));
        sync.TopicProgress.ShouldHaveSingleItem().Status.ShouldBe("Done");
        (await AuthApi.Read<SyncItem>(await _api.Send(HttpMethod.Get, $"/v1/sync?since={done.ChangeSeq}", tokens.AccessToken)))
            .TopicProgress.ShouldBeEmpty();

        await AuthApi.ShouldBeProblem(
            await PutProgress(tokens, Guid.NewGuid(), "Done", DateTimeOffset.UtcNow),
            HttpStatusCode.BadRequest,
            "tracking.unknown_topic");
    }

    [Fact]
    public async Task New_student_gets_a_starter_plan_frozen_for_the_week()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var response = await _api.Send(HttpMethod.Get, "/v1/recommendations/week", tokens.AccessToken);
        var plan = await AuthApi.Read<PlanItemsResponse>(response);

        plan.RuleSetVersion.ShouldBe("R2026.1");
        DateOnly.Parse(plan.WeekStart, CultureInfo.InvariantCulture).DayOfWeek.ShouldBe(DayOfWeek.Monday);
        plan.Items.ShouldNotBeEmpty();
        // Heavy topics without data are measured first, then starter topics fill the plan.
        plan.Items.Count.ShouldBe(5);
        plan.Items.Select(i => i.Kind).Distinct().ShouldBe(["CollectData", "FirstRecord"]);
        response.Headers.ETag!.Tag.ShouldBe(plan.ETag);

        (await _api.Send(HttpMethod.Get, "/v1/recommendations/week", tokens.AccessToken, ifNoneMatch: plan.ETag))
            .StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Untagged_mock_exam_comes_first_and_a_dismissed_item_is_replaced()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        await PutTest(tokens, Today, (Paragraph, 20, 10, 8, 2));
        await PutMock(tokens, Today);

        var plan = await Plan(tokens);

        var tag = plan.Items[0];
        tag.Kind.ShouldBe("TagMockExam");
        tag.Completed.ShouldBeFalse();
        var focus = plan.Items.FirstOrDefault(i => i.TopicId == Paragraph);
        focus.ShouldNotBeNull();
        focus.Done.ShouldBe(20);

        var after = await AuthApi.Read<PlanItemsResponse>(
            await _api.Send(HttpMethod.Post, $"/v1/recommendations/items/{Uri.EscapeDataString(focus.Key)}/dismiss", tokens.AccessToken, new { Reason = "AlreadyKnow" }));
        after.Items.ShouldNotContain(i => i.Key == focus.Key);
        after.Items[0].Key.ShouldBe(tag.Key);
        (await Plan(tokens)).Items.ShouldNotContain(i => i.Key == focus.Key);

        // A retried dismissal (e.g. lost response) is idempotent: same plan, no 404.
        var again = await _api.Send(HttpMethod.Post, $"/v1/recommendations/items/{Uri.EscapeDataString(focus.Key)}/dismiss", tokens.AccessToken, new { Reason = "AlreadyKnow" });
        (await AuthApi.Read<PlanItemsResponse>(again)).Items.Select(i => i.Key).ShouldBe(after.Items.Select(i => i.Key));

        await AuthApi.ShouldBeProblem(
            await _api.Send(HttpMethod.Post, "/v1/recommendations/items/focus%3Anope/dismiss", tokens.AccessToken, new { Reason = "NotNow" }),
            HttpStatusCode.NotFound,
            "recommendation.item_not_found");
    }

    [Fact]
    public async Task Concurrent_dismissals_are_both_kept()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var plan = await Plan(tokens);
        var keys = plan.Items.Take(3).Select(i => i.Key).ToList();

        var responses = await Task.WhenAll(keys.Select(k =>
            _api.Send(HttpMethod.Post, $"/v1/recommendations/items/{Uri.EscapeDataString(k)}/dismiss", tokens.AccessToken, new { Reason = "NotNow" })));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        var after = await Plan(tokens);
        after.Items.ShouldNotContain(i => keys.Contains(i.Key));
    }

    [Fact]
    public async Task Tagging_item_of_a_deleted_exam_is_not_completed()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var exam = await PutMock(tokens, Today);
        var tag = (await Plan(tokens)).Items[0];
        tag.Kind.ShouldBe("TagMockExam");

        var modifiedAt = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        (await _api.Send(HttpMethod.Delete, $"/v1/mock-exams/{exam}?modifiedAt={modifiedAt}", tokens.AccessToken)).IsSuccessStatusCode.ShouldBeTrue();

        (await Plan(tokens)).Items.Single(i => i.Key == tag.Key).Completed.ShouldBeFalse();
    }

    [Fact]
    public async Task Export_is_a_zip_of_the_students_own_data_only()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var other = await _api.RegisterAndVerify(AuthApi.NewEmail());
        await PutTest(owner, Today, (Paragraph, 20, 14, 5, 1));
        await PutTest(other, Today, (History, 10, 7, 3, 0));
        await PutProgress(owner, Paragraph, "Done", DateTimeOffset.UtcNow);

        var response = await _api.Send(HttpMethod.Get, "/v1/me/export", owner.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/zip");
        response.Content.Headers.ContentDisposition!.FileName!.ShouldStartWith("nirengi-verilerim-");
        await using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync(ct));
        var files = zip.Entries.ToDictionary(e => e.FullName, Read);

        files.Keys.ShouldBe(
            ["BENIOKU.txt", "izinler.csv", "profil.csv", "calisma_profili.csv", "deneme_bolumleri.csv", "deneme_isaretleri.csv", "denemeler.csv", "konu_ilerlemesi.csv", "konu_testi_satirlari.csv", "konu_testleri.csv", "oneri_geri_bildirimi.csv"],
            ignoreOrder: false);
        files["profil.csv"].ShouldStartWith("﻿ad;soyad;e_posta");
        files["profil.csv"].ShouldNotContain("hash", Case.Insensitive);
        var rows = files["konu_testi_satirlari.csv"].Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        rows.Length.ShouldBe(2);
        rows[1].ShouldContain("TYT.TUR.04");
        rows[1].ShouldContain(";14;5;1;12,75;");
        files["konu_ilerlemesi.csv"].ShouldContain("TYT.TUR.04");
        files.Values.ShouldAllBe(f => !f.Contains("TYT.TAR.01", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_and_plan_require_sign_in()
    {
        var ct = TestContext.Current.CancellationToken;

        (await _api.Client.GetAsync("/v1/me/export", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.Client.GetAsync("/v1/recommendations/week", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task User_deletion_removes_plans_feedback_and_progress()
    {
        var ct = TestContext.Current.CancellationToken;
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var me = await AuthApi.Read<AuthApi.Profile>(await _api.Send(HttpMethod.Get, "/v1/me", tokens.AccessToken));
        await PutProgress(tokens, History, "InProgress", DateTimeOffset.UtcNow);
        var plan = await Plan(tokens);
        await _api.Send(HttpMethod.Post, $"/v1/recommendations/items/{Uri.EscapeDataString(plan.Items[0].Key)}/dismiss", tokens.AccessToken, new { Reason = "NotNow" });

        await using var scope = factory.Services.CreateAsyncScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler<UserDeleted>>())
        {
            await handler.Handle(new UserDeleted(Guid.NewGuid(), DateTimeOffset.UtcNow, me.Id), ct);
        }

        var recommendation = scope.ServiceProvider.GetRequiredService<RecommendationDbContext>();
        (await recommendation.Plans.AnyAsync(p => p.UserId == me.Id, ct)).ShouldBeFalse();
        (await recommendation.Feedback.AnyAsync(f => f.UserId == me.Id, ct)).ShouldBeFalse();
        (await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().TopicProgress.AnyAsync(p => p.UserId == me.Id, ct)).ShouldBeFalse();
    }

    private static string Read(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false);
        return reader.ReadToEnd();
    }

    private async Task<PlanItemsResponse> Plan(AuthApi.Tokens tokens) =>
        await AuthApi.Read<PlanItemsResponse>(await _api.Send(HttpMethod.Get, "/v1/recommendations/week", tokens.AccessToken));

    private Task<HttpResponseMessage> PutProgress(AuthApi.Tokens tokens, Guid topic, string status, DateTimeOffset at) =>
        _api.Send(HttpMethod.Put, $"/v1/topic-progress/{topic}", tokens.AccessToken, new { Status = status, ClientModifiedAt = at });

    private async Task PutTest(AuthApi.Tokens tokens, DateOnly day, (Guid Topic, int Total, int Correct, int Wrong, int Blank) result)
    {
        var body = new
        {
            StudiedOn = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SourceId = Publisher,
            IsRepeat = false,
            Results = new[] { new { TopicId = result.Topic, result.Total, result.Correct, result.Wrong, result.Blank } },
            ClientModifiedAt = DateTimeOffset.UtcNow,
        };
        (await _api.Send(HttpMethod.Put, $"/v1/topic-tests/{Guid.CreateVersion7()}", tokens.AccessToken, body)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<Guid> PutMock(AuthApi.Tokens tokens, DateOnly day)
    {
        var id = Guid.CreateVersion7();
        var body = new
        {
            Kind = "Tyt",
            TakenOn = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            SourceId = Publisher,
            Sections = new[] { new { SectionCode = "TYT-TUR", Correct = 30, Wrong = 8 } },
            ClientModifiedAt = DateTimeOffset.UtcNow,
        };
        (await _api.Send(HttpMethod.Put, $"/v1/mock-exams/{id}", tokens.AccessToken, body)).StatusCode.ShouldBe(HttpStatusCode.OK);
        return id;
    }

    private sealed record CalendarEvent(string Code, string Status);

    private sealed record CalendarYear(int ExamYear, List<CalendarEvent> Events);

    private sealed record CalendarResponse(string Version, List<CalendarYear> Calendars);

    private sealed record ProgressItem(Guid TopicId, string Status, long ChangeSeq);

    private sealed record SyncItem(List<ProgressItem> TopicProgress, long Cursor);

    private sealed record PlanItem(string Key, string Kind, Guid? TopicId, int? TargetQuestions, string Title, int Done, bool Completed);

    private sealed record PlanItemsResponse(string ETag, string WeekStart, string RuleSetVersion, List<PlanItem> Items);
}
