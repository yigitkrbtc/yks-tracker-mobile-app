using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Tracking.Domain.TopicTests;
using Nirengi.Modules.Tracking.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Idempotent mock exam writes with topic marks, and their place in the shared change feed (S4, ADR-0015).</summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class MockExamApiTests(NirengiApiFactory factory)
{
    private static readonly Guid History = TopicIdentity.For("TYT.TAR.01");
    private static readonly Guid Biology = TopicIdentity.For("TYT.BIY.01");
    private static readonly Guid Publisher = SourceIdentity.For("OSYM");
    private readonly AuthApi _api = new(factory);

    private static string Today => TopicTestRules.TodayInTurkey(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Same_put_twice_stores_one_exam_with_template_counts()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();
        var body = Tyt(DateTimeOffset.UtcNow.AddMinutes(-1));

        var first = await AuthApi.Read<ExamItem>(await Put(tokens, id, body));
        var second = await AuthApi.Read<ExamItem>(await Put(tokens, id, body));

        second.ShouldBeEquivalentTo(first);
        first.Kind.ShouldBe("Tyt");
        var turkish = first.Sections.Single(s => s.SectionCode == "TYT-TUR");
        (turkish.QuestionCount, turkish.Blank, turkish.Net).ShouldBe((40, 2, 28m));
        first.Sections.Single(s => s.SectionCode == "TYT-SOS.TAR").TaggingStatus.ShouldBe("Full");
        first.Marks.ShouldHaveSingleItem().TopicId.ShouldBe(History);
        (await Sync(tokens, 0)).MockExams.ShouldHaveSingleItem().Id.ShouldBe(id);
    }

    [Fact]
    public async Task Tagging_later_is_an_edit_and_an_older_edit_gets_the_newer_state_back()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await Put(tokens, id, Tyt(now.AddMinutes(-3)) with { Marks = [] });

        var tagged = await AuthApi.Read<ExamItem>(await Put(tokens, id, Tyt(now.AddMinutes(-1))));
        var late = await AuthApi.Read<ExamItem>(await Put(tokens, id, Tyt(now.AddMinutes(-2)) with { Marks = [] }));

        tagged.Marks.Count.ShouldBe(1);
        late.Marks.Count.ShouldBe(1);
        late.ChangeSeq.ShouldBe(tagged.ChangeSeq);
    }

    [Fact]
    public async Task Another_users_exam_id_is_not_found()
    {
        var owner = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var other = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();
        await Put(owner, id, Tyt(DateTimeOffset.UtcNow));

        await AuthApi.ShouldBeProblem(await Put(other, id, Tyt(DateTimeOffset.UtcNow)), HttpStatusCode.NotFound, "tracking.mock_exam_not_found");
        await AuthApi.ShouldBeProblem(await Delete(other, id, DateTimeOffset.UtcNow), HttpStatusCode.NotFound, "tracking.mock_exam_not_found");
        (await Sync(other, 0)).MockExams.ShouldBeEmpty();
    }

    public static TheoryData<string, string> InvalidCases => new()
    {
        { "unknown-section", "tracking.unknown_mock_section" },
        { "wrong-kind", "tracking.mock_sections_kind" },
        { "own-count", "tracking.mock_section_question_count" },
        { "too-many-answers", "tracking.mock_section_counts" },
        { "overlap", "tracking.mock_sections_overlap" },
        { "topic-of-other-subject", "tracking.topic_not_in_section" },
        { "unknown-topic", "tracking.unknown_topic" },
        { "unknown-source", "tracking.unknown_source" },
        { "marks-exceed", "validation.failed" },
        { "no-sections", "validation.failed" },
        { "future", "validation.failed" },
        { "no-source", "validation.failed" },
    };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task Invalid_exam_is_rejected(string invalid, string code)
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var valid = Tyt(DateTimeOffset.UtcNow);
        var body = invalid switch
        {
            "unknown-section" => valid with { Sections = [new("TYT-XXX", null, 1, 1)], Marks = [] },
            "wrong-kind" => valid with { Kind = "Ayt" },
            "own-count" => valid with { Sections = [new("TYT-TUR", 30, 20, 5)], Marks = [] },
            "too-many-answers" => valid with { Sections = [new("TYT-SOS.TAR", null, 4, 2)], Marks = [] },
            "overlap" => valid with { Sections = [new("TYT-SOS", null, 10, 2), new("TYT-SOS.TAR", null, 3, 1)], Marks = [] },
            "topic-of-other-subject" => valid with { Marks = [new("TYT-SOS.TAR", Biology, 1, 0)] },
            "unknown-topic" => valid with { Marks = [new("TYT-SOS.TAR", Guid.NewGuid(), 1, 0)] },
            "unknown-source" => valid with { SourceId = Guid.NewGuid() },
            "marks-exceed" => valid with { Marks = [new("TYT-SOS.TAR", History, 2, 0)] },
            "no-sections" => valid with { Sections = [], Marks = [] },
            "future" => valid with { TakenOn = TopicTestRules.TodayInTurkey(DateTimeOffset.UtcNow).AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
            _ => valid with { SourceId = null },
        };

        await AuthApi.ShouldBeProblem(await Put(tokens, Guid.CreateVersion7(), body), HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Branch_exam_keeps_its_own_question_count()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var body = Tyt(DateTimeOffset.UtcNow) with { Kind = "Branch", Sections = [new("AYT-FEN.FIZ", 20, 12, 4)], Marks = [] };

        var exam = await AuthApi.Read<ExamItem>(await Put(tokens, Guid.CreateVersion7(), body));

        exam.Sections.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            s => s.QuestionCount.ShouldBe(20),
            s => s.Blank.ShouldBe(4),
            s => s.Net.ShouldBe(11m));
    }

    [Fact]
    public async Task Delete_is_idempotent_syncs_as_a_tombstone_and_publishes_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();
        var created = await AuthApi.Read<ExamItem>(await Put(tokens, id, Tyt(DateTimeOffset.UtcNow.AddMinutes(-1))));

        (await Delete(tokens, id, DateTimeOffset.UtcNow.AddMinutes(-2))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Delete(tokens, id, DateTimeOffset.UtcNow)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Delete(tokens, id, DateTimeOffset.UtcNow)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Delete(tokens, Guid.CreateVersion7(), DateTimeOffset.UtcNow)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tombstone = (await Sync(tokens, created.ChangeSeq)).MockExams.ShouldHaveSingleItem();
        tombstone.Deleted.ShouldBeTrue();
        tombstone.Sections.ShouldBeEmpty();
        tombstone.Marks.ShouldBeEmpty();

        await using var scope = factory.Services.CreateAsyncScope();
        var payloads = await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().OutboxMessages.AsNoTracking()
            .Where(m => m.Type.Contains("MockExamChanged"))
            .Select(m => m.Payload)
            .ToListAsync(ct);
        var mine = payloads.Where(p => p.Contains(id.ToString(), StringComparison.Ordinal)).ToList();
        mine.Count.ShouldBe(2); // recorded + deleted
        mine.ShouldAllBe(p => p.Contains("TYT-SOS.TAR", StringComparison.Ordinal) && p.Contains(History.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Change_feed_pages_across_topic_tests_and_mock_exams_with_one_cursor()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var order = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var exam = Guid.CreateVersion7();
            await Put(tokens, exam, Tyt(DateTimeOffset.UtcNow));
            order.Add(exam);

            var test = Guid.CreateVersion7();
            var body = new { StudiedOn = Today, SourceId = Publisher, IsRepeat = false, Results = new[] { new { TopicId = Biology, Total = 5, Correct = 5, Wrong = 0, Blank = 0 } }, ClientModifiedAt = DateTimeOffset.UtcNow };
            (await _api.Send(HttpMethod.Put, $"/v1/topic-tests/{test}", tokens.AccessToken, body)).StatusCode.ShouldBe(HttpStatusCode.OK);
            order.Add(test);
        }

        var page1 = await Sync(tokens, 0, limit: 3);
        var page2 = await Sync(tokens, page1.Cursor, limit: 3);

        (page1.HasMore, page2.HasMore).ShouldBe((true, false));
        (page1.MockExams.Count, page1.TopicTests.Count).ShouldBe((2, 1));
        (page2.MockExams.Count, page2.TopicTests.Count).ShouldBe((0, 1));
        page1.MockExams.Select(e => e.Id).Concat(page1.TopicTests.Select(t => t.Id)).Concat(page2.TopicTests.Select(t => t.Id))
            .Order().ShouldBe(order.Order());
        page1.Cursor.ShouldBe(page1.MockExams.Max(e => e.ChangeSeq));
    }

    [Fact]
    public async Task Mock_exams_require_sign_in()
    {
        var response = await _api.Client.PutAsync($"/v1/mock-exams/{Guid.CreateVersion7()}", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Turkish, history and geography; history's 1 wrong + 1 blank fully marked.</summary>
    private static ExamBody Tyt(DateTimeOffset modifiedAt) => new(
        "Tyt",
        Today,
        Publisher,
        null,
        "Türkiye geneli 1",
        165,
        null,
        [new("TYT-TUR", null, 30, 8), new("TYT-SOS.TAR", null, 3, 1), new("TYT-SOS.COG", null, 2, 2)],
        [new("TYT-SOS.TAR", History, 1, 1)],
        modifiedAt);

    private Task<HttpResponseMessage> Put(AuthApi.Tokens tokens, Guid id, ExamBody body) =>
        _api.Send(HttpMethod.Put, $"/v1/mock-exams/{id}", tokens.AccessToken, body);

    private Task<HttpResponseMessage> Delete(AuthApi.Tokens tokens, Guid id, DateTimeOffset modifiedAt) =>
        _api.Send(HttpMethod.Delete, $"/v1/mock-exams/{id}?modifiedAt={Uri.EscapeDataString(modifiedAt.ToString("O"))}", tokens.AccessToken);

    private async Task<SyncItem> Sync(AuthApi.Tokens tokens, long since, int limit = 500) =>
        await AuthApi.Read<SyncItem>(await _api.Send(HttpMethod.Get, $"/v1/sync?since={since}&limit={limit}", tokens.AccessToken));

    private sealed record SectionInput(string SectionCode, int? QuestionCount, int Correct, int Wrong);

    private sealed record MarkInput(string SectionCode, Guid TopicId, int Wrong, int Blank);

    private sealed record ExamBody(
        string Kind,
        string TakenOn,
        Guid? SourceId,
        string? SourceName,
        string? Title,
        int? DurationMinutes,
        string? Note,
        List<SectionInput> Sections,
        List<MarkInput> Marks,
        DateTimeOffset ClientModifiedAt);

    private sealed record SectionItem(string SectionCode, int QuestionCount, int Correct, int Wrong, int Blank, decimal Net, string TaggingStatus);

    private sealed record MarkItem(string SectionCode, Guid TopicId, int Wrong, int Blank);

    private sealed record ExamItem(Guid Id, string Kind, string TakenOn, List<SectionItem> Sections, List<MarkItem> Marks, bool Deleted, long ChangeSeq);

    private sealed record IdItem(Guid Id);

    private sealed record SyncItem(List<IdItem> TopicTests, List<ExamItem> MockExams, long Cursor, bool HasMore);
}
