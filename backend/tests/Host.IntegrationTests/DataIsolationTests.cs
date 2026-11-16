using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Tracking.Domain.TopicTests;
using Nirengi.Modules.Tracking.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>
/// Security audit M1 (ADR-0004, ADR-0021): student B's token never reaches student A's records. Every endpoint that takes a
/// record id answers 404 and leaves the data alone; every caller-scoped endpoint (the id is the token) answers with B's own
/// state, never A's. A's records carry a marker string so a leak anywhere in a response, export included, is visible.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class DataIsolationTests(NirengiApiFactory factory)
{
    private const string Marker = "yalniz-A-gorur";
    private static readonly Guid Paragraph = TopicIdentity.For("TYT.TUR.04");
    private static readonly Guid History = TopicIdentity.For("TYT.TAR.01");
    private static readonly Guid Publisher = SourceIdentity.For("OSYM");
    private readonly AuthApi _api = new(factory);

    private static DateOnly Today => TopicTestRules.TodayInTurkey(DateTimeOffset.UtcNow);

    private static string Day => Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Record_ids_of_another_student_answer_404_on_every_verb_and_change_nothing()
    {
        var a = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var b = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var test = Guid.CreateVersion7();
        var exam = Guid.CreateVersion7();
        (await _api.Send(HttpMethod.Put, $"/v1/topic-tests/{test}", a.AccessToken, TestBody(DateTimeOffset.UtcNow.AddMinutes(-2)))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.Send(HttpMethod.Put, $"/v1/mock-exams/{exam}", a.AccessToken, ExamBody(DateTimeOffset.UtcNow.AddMinutes(-2)))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var before = await SyncText(a);

        // A newer edit and a newer delete from B would both win by edit time if the row were reachable at all.
        await AuthApi.ShouldBeProblem(await _api.Send(HttpMethod.Put, $"/v1/topic-tests/{test}", b.AccessToken, TestBody(DateTimeOffset.UtcNow)), HttpStatusCode.NotFound, "tracking.topic_test_not_found");
        await AuthApi.ShouldBeProblem(await Delete(b, $"topic-tests/{test}"), HttpStatusCode.NotFound, "tracking.topic_test_not_found");
        await AuthApi.ShouldBeProblem(await _api.Send(HttpMethod.Put, $"/v1/mock-exams/{exam}", b.AccessToken, ExamBody(DateTimeOffset.UtcNow)), HttpStatusCode.NotFound, "tracking.mock_exam_not_found");
        await AuthApi.ShouldBeProblem(await Delete(b, $"mock-exams/{exam}"), HttpStatusCode.NotFound, "tracking.mock_exam_not_found");

        (await SyncText(a)).ShouldBe(before);
        var seenByB = await SyncText(b);
        seenByB.ShouldNotContain(Marker);
        seenByB.ShouldNotContain(test.ToString());
        seenByB.ShouldNotContain(exam.ToString());

        // Nothing of B's was written under A's ids either.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        (await db.TopicTests.AsNoTracking().SingleAsync(t => t.Id == test, TestContext.Current.CancellationToken)).DeletedAt.ShouldBeNull();
        (await db.MockExams.AsNoTracking().SingleAsync(e => e.Id == exam, TestContext.Current.CancellationToken)).DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Caller_scoped_endpoints_answer_with_the_callers_own_state()
    {
        var a = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var b = await _api.RegisterAndVerify(AuthApi.NewEmail());
        (await _api.Send(HttpMethod.Put, $"/v1/topic-tests/{Guid.CreateVersion7()}", a.AccessToken, TestBody(DateTimeOffset.UtcNow))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.Send(HttpMethod.Put, "/v1/profile", a.AccessToken, new { Grade = "Twelfth", PrimaryScoreType = "SAY", TargetExamYear = 2027, WeeklyQuestionGoal = 777 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var progress = await ReadJson(await _api.Send(HttpMethod.Put, $"/v1/topic-progress/{History}", a.AccessToken, new { Status = "Done", ClientModifiedAt = DateTimeOffset.UtcNow }));
        var planA = await ReadJson(await _api.Send(HttpMethod.Get, "/v1/recommendations/week", a.AccessToken));

        // Profile: B has none yet (404, not A's); B's own save leaves A's alone.
        await AuthApi.ShouldBeProblem(await _api.Send(HttpMethod.Get, "/v1/profile", b.AccessToken), HttpStatusCode.NotFound, "tracking.profile_not_found");
        (await _api.Send(HttpMethod.Put, "/v1/profile", b.AccessToken, new { Grade = "Graduate", PrimaryScoreType = "DIL", YdtLanguage = "German", TargetExamYear = 2027, WeeklyQuestionGoal = 50 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadJson(await _api.Send(HttpMethod.Get, "/v1/profile", a.AccessToken))).GetProperty("weeklyQuestionGoal").GetInt32().ShouldBe(777);

        // Topic marks are keyed (user, topic): B marking the same topic leaves A's mark and change position alone.
        (await _api.Send(HttpMethod.Put, $"/v1/topic-progress/{History}", b.AccessToken, new { Status = "InProgress", ClientModifiedAt = DateTimeOffset.UtcNow.AddMinutes(1) })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var progressA = (await ReadJson(await _api.Send(HttpMethod.Get, "/v1/sync?since=0", a.AccessToken))).GetProperty("topicProgress").EnumerateArray().Single();
        progressA.GetProperty("status").GetString().ShouldBe("Done");
        progressA.GetProperty("changeSeq").GetInt64().ShouldBe(progress.GetProperty("changeSeq").GetInt64());

        // Analysis, plan, export: nothing of A's shows up for B.
        (await ReadJson(await _api.Send(HttpMethod.Get, "/v1/analytics/overview", b.AccessToken))).GetProperty("topics").GetArrayLength().ShouldBe(0);
        (await ReadJson(await _api.Send(HttpMethod.Get, $"/v1/analytics/topics/{Paragraph}", b.AccessToken)))
            .GetProperty("analysis").GetProperty("totals").GetProperty("records").GetInt32().ShouldBe(0);
        // The plans are separate rows: B dropping an item (same key as A's, plans of new students coincide) leaves A's plan whole.
        var planB = await ReadJson(await _api.Send(HttpMethod.Get, "/v1/recommendations/week", b.AccessToken));
        var aKeys = planA.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!).ToList();
        var bKeys = planB.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!).ToList();
        aKeys.ShouldNotBeEmpty();
        var shared = aKeys.Intersect(bKeys).First();
        (await _api.Send(HttpMethod.Post, $"/v1/recommendations/items/{Uri.EscapeDataString(shared)}/dismiss", b.AccessToken, new { Reason = "NotNow" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var planAAfter = await ReadJson(await _api.Send(HttpMethod.Get, "/v1/recommendations/week", a.AccessToken));
        planAAfter.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!).ShouldBe(aKeys);

        await using var zip = new ZipArchive(await (await _api.Send(HttpMethod.Get, "/v1/me/export", b.AccessToken)).Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldNotContain(Marker, customMessage: entry.FullName);
        }
    }

    [Fact]
    public async Task Deleted_records_keep_a_tombstone_without_the_students_free_text()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var test = Guid.CreateVersion7();
        var exam = Guid.CreateVersion7();
        var ownBook = new { StudiedOn = Day, SourceName = Marker, IsRepeat = false, Note = Marker, Results = new[] { new { TopicId = Paragraph, Total = 5, Correct = 5, Wrong = 0, Blank = 0 } }, ClientModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-2) };
        var ownExam = new { Kind = "Tyt", TakenOn = Day, SourceName = Marker, Title = Marker, Note = Marker, Sections = new[] { new { SectionCode = "TYT-TUR", Correct = 30, Wrong = 8 } }, ClientModifiedAt = DateTimeOffset.UtcNow.AddMinutes(-2) };
        (await _api.Send(HttpMethod.Put, $"/v1/topic-tests/{test}", a.AccessToken, ownBook)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _api.Send(HttpMethod.Put, $"/v1/mock-exams/{exam}", a.AccessToken, ownExam)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Delete(a, $"topic-tests/{test}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Delete(a, $"mock-exams/{exam}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Other devices still learn of the delete (tombstones stay in the change feed) but get none of the text back.
        var feed = await SyncText(a);
        feed.ShouldNotContain(Marker);
        var sync = JsonDocument.Parse(feed).RootElement;
        sync.GetProperty("topicTests").EnumerateArray().Single().GetProperty("deleted").GetBoolean().ShouldBeTrue();
        sync.GetProperty("mockExams").EnumerateArray().Single().GetProperty("deleted").GetBoolean().ShouldBeTrue();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        var storedTest = await db.TopicTests.AsNoTracking().SingleAsync(t => t.Id == test, ct);
        (storedTest.Note, storedTest.SourceName).ShouldBe((null, null));
        var storedExam = await db.MockExams.AsNoTracking().SingleAsync(e => e.Id == exam, ct);
        (storedExam.Note, storedExam.SourceName, storedExam.Title).ShouldBe((null, null, null));
    }

    [Fact]
    public async Task Without_a_token_every_data_endpoint_is_401()
    {
        var id = Guid.CreateVersion7();
        var calls = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Get, "/v1/sync"),
            (HttpMethod.Get, "/v1/profile"),
            (HttpMethod.Put, "/v1/profile"),
            (HttpMethod.Put, $"/v1/topic-tests/{id}"),
            (HttpMethod.Delete, $"/v1/topic-tests/{id}?modifiedAt=2026-01-01T00:00:00Z"),
            (HttpMethod.Put, $"/v1/mock-exams/{id}"),
            (HttpMethod.Delete, $"/v1/mock-exams/{id}?modifiedAt=2026-01-01T00:00:00Z"),
            (HttpMethod.Put, $"/v1/topic-progress/{id}"),
            (HttpMethod.Get, "/v1/analytics/overview"),
            (HttpMethod.Get, $"/v1/analytics/topics/{id}"),
            (HttpMethod.Get, "/v1/recommendations/week"),
            (HttpMethod.Post, "/v1/recommendations/items/x/dismiss"),
            (HttpMethod.Get, "/v1/me"),
            (HttpMethod.Get, "/v1/me/export"),
        };

        foreach (var (method, path) in calls)
        {
            (await _api.Client.SendAsync(new HttpRequestMessage(method, path), TestContext.Current.CancellationToken))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} {path}");
        }
    }

    private static object TestBody(DateTimeOffset modifiedAt) => new
    {
        StudiedOn = Day,
        SourceId = Publisher,
        IsRepeat = false,
        Note = Marker,
        Results = new[] { new { TopicId = Paragraph, Total = 20, Correct = 10, Wrong = 8, Blank = 2 } },
        ClientModifiedAt = modifiedAt,
    };

    private static object ExamBody(DateTimeOffset modifiedAt) => new
    {
        Kind = "Tyt",
        TakenOn = Day,
        SourceId = Publisher,
        Title = Marker,
        Note = Marker,
        Sections = new[] { new { SectionCode = "TYT-TUR", Correct = 30, Wrong = 8 } },
        ClientModifiedAt = modifiedAt,
    };

    private Task<HttpResponseMessage> Delete(AuthApi.Tokens tokens, string resource) =>
        _api.Send(HttpMethod.Delete, $"/v1/{resource}?modifiedAt={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"))}", tokens.AccessToken);

    private async Task<string> SyncText(AuthApi.Tokens tokens) =>
        await (await _api.Send(HttpMethod.Get, "/v1/sync?since=0", tokens.AccessToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement.Clone();
    }
}
