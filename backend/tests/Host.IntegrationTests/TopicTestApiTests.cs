using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Tracking.Domain.TopicTests;
using Nirengi.Modules.Tracking.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Idempotent topic test writes and the change feed the mobile app syncs from (S3, ADR-0005).</summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class TopicTestApiTests(NirengiApiFactory factory)
{
    private static readonly Guid TopicA = TopicIdentity.For("TYT.BIY.01");
    private static readonly Guid TopicB = TopicIdentity.For("TYT.BIY.02");
    private static readonly Guid PastPapers = SourceIdentity.For("OSYM");
    private readonly AuthApi _api = new(factory);

    private static string Today => TopicTestRules.TodayInTurkey(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Same_put_twice_stores_one_test_and_answers_the_same()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();
        var body = Body(DateTimeOffset.UtcNow.AddMinutes(-1), (TopicA, 20, 12, 5, 3), (TopicB, 10, 4, 4, 2));

        var first = await AuthApi.Read<TestItem>(await Put(tokens, id, body));
        var second = await AuthApi.Read<TestItem>(await Put(tokens, id, body));

        second.ShouldBeEquivalentTo(first);
        first.Results.Single(r => r.TopicId == TopicA).Net.ShouldBe(10.75m);
        first.SourceId.ShouldBe(PastPapers);
        var sync = await Sync(tokens, 0);
        sync.TopicTests.ShouldHaveSingleItem().Id.ShouldBe(id);
        sync.Cursor.ShouldBe(first.ChangeSeq);
    }

    [Fact]
    public async Task Older_edit_arriving_late_gets_the_newer_state_back()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await Put(tokens, id, Body(now.AddMinutes(-1), (TopicA, 10, 9, 1, 0)));

        var late = await AuthApi.Read<TestItem>(await Put(tokens, id, Body(now.AddMinutes(-5), (TopicA, 10, 1, 9, 0))));

        late.Results.Single().Correct.ShouldBe(9);
    }

    [Fact]
    public async Task Another_users_test_id_is_not_found()
    {
        var owner = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var other = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();
        await Put(owner, id, Body(DateTimeOffset.UtcNow, (TopicA, 5, 5, 0, 0)));

        await AuthApi.ShouldBeProblem(await Put(other, id, Body(DateTimeOffset.UtcNow, (TopicA, 5, 0, 5, 0))), HttpStatusCode.NotFound, "tracking.topic_test_not_found");
        await AuthApi.ShouldBeProblem(await Delete(other, id, DateTimeOffset.UtcNow), HttpStatusCode.NotFound, "tracking.topic_test_not_found");
        (await Sync(other, 0)).TopicTests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unknown_topic_or_source_is_rejected()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        await AuthApi.ShouldBeProblem(
            await Put(tokens, Guid.CreateVersion7(), Body(DateTimeOffset.UtcNow, (Guid.NewGuid(), 5, 5, 0, 0))),
            HttpStatusCode.BadRequest,
            "tracking.unknown_topic");
        await AuthApi.ShouldBeProblem(
            await Put(tokens, Guid.CreateVersion7(), Body(DateTimeOffset.UtcNow, (TopicA, 5, 5, 0, 0)) with { SourceId = Guid.NewGuid() }),
            HttpStatusCode.BadRequest,
            "tracking.unknown_source");
    }

    public static TheoryData<string> InvalidCases => ["counts", "reasons", "future", "both-sources", "no-source", "duplicate-topic", "no-results"];

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task Invalid_test_is_rejected(string invalid)
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var valid = Body(DateTimeOffset.UtcNow, (TopicA, 10, 6, 2, 2));
        var body = invalid switch
        {
            "counts" => Body(DateTimeOffset.UtcNow, (TopicA, 10, 6, 2, 1)),
            "reasons" => valid with { Results = [new ResultInput(TopicA, 10, 6, 2, 2, 2, 1, null)] },
            "future" => valid with { StudiedOn = TopicTestRules.TodayInTurkey(DateTimeOffset.UtcNow).AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
            "both-sources" => valid with { SourceName = "Kitap" },
            "no-source" => valid with { SourceId = null },
            "duplicate-topic" => Body(DateTimeOffset.UtcNow, (TopicA, 5, 5, 0, 0), (TopicA, 5, 5, 0, 0)),
            _ => valid with { Results = [] },
        };

        await AuthApi.ShouldBeProblem(await Put(tokens, Guid.CreateVersion7(), body), HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Delete_is_idempotent_and_syncs_as_a_tombstone()
    {
        var ct = TestContext.Current.CancellationToken;
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var id = Guid.CreateVersion7();
        var created = await AuthApi.Read<TestItem>(await Put(tokens, id, Body(DateTimeOffset.UtcNow.AddMinutes(-1), (TopicA, 5, 5, 0, 0))));

        (await Delete(tokens, id, DateTimeOffset.UtcNow)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Delete(tokens, id, DateTimeOffset.UtcNow)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Delete(tokens, Guid.CreateVersion7(), DateTimeOffset.UtcNow)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tombstone = (await Sync(tokens, created.ChangeSeq)).TopicTests.ShouldHaveSingleItem();
        tombstone.Deleted.ShouldBeTrue();
        tombstone.Results.ShouldBeEmpty();

        await using var scope = factory.Services.CreateAsyncScope();
        var payloads = await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().OutboxMessages.AsNoTracking()
            .Where(m => m.Type.Contains("TopicTestChanged"))
            .Select(m => m.Payload)
            .ToListAsync(ct);
        payloads.Count(p => p.Contains(id.ToString(), StringComparison.Ordinal)).ShouldBe(2); // recorded + deleted; the repeated delete changed nothing
    }

    [Fact]
    public async Task Change_feed_pages_in_order_until_caught_up()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var id = Guid.CreateVersion7();
            ids.Add(id);
            await Put(tokens, id, Body(DateTimeOffset.UtcNow, (TopicA, 5, i, 5 - i, 0)));
        }

        var page1 = await Sync(tokens, 0, limit: 2);
        var page2 = await Sync(tokens, page1.Cursor, limit: 2);
        var page3 = await Sync(tokens, page2.Cursor, limit: 2);

        (page1.HasMore, page2.HasMore, page3.HasMore).ShouldBe((true, false, false));
        page1.TopicTests.Concat(page2.TopicTests).Select(t => t.Id).ShouldBe(ids);
        page3.TopicTests.ShouldBeEmpty();
        page3.Cursor.ShouldBe(page2.Cursor);
        await AuthApi.ShouldBeProblem(await _api.Send(HttpMethod.Get, "/v1/sync?since=0&limit=501", tokens.AccessToken), HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Topic_tests_require_sign_in()
    {
        var response = await _api.Client.GetAsync("/v1/sync", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static TestBody Body(DateTimeOffset modifiedAt, params (Guid Topic, int Total, int Correct, int Wrong, int Blank)[] results) =>
        new(Today, PastPapers, null, false, "Medium", 30, "deneme notu", [.. results.Select(r => new ResultInput(r.Topic, r.Total, r.Correct, r.Wrong, r.Blank, null, null, null))], modifiedAt);

    private Task<HttpResponseMessage> Put(AuthApi.Tokens tokens, Guid id, TestBody body) =>
        _api.Send(HttpMethod.Put, $"/v1/topic-tests/{id}", tokens.AccessToken, body);

    private Task<HttpResponseMessage> Delete(AuthApi.Tokens tokens, Guid id, DateTimeOffset modifiedAt) =>
        _api.Send(HttpMethod.Delete, $"/v1/topic-tests/{id}?modifiedAt={Uri.EscapeDataString(modifiedAt.ToString("O"))}", tokens.AccessToken);

    private async Task<SyncItem> Sync(AuthApi.Tokens tokens, long since, int limit = 500) =>
        await AuthApi.Read<SyncItem>(await _api.Send(HttpMethod.Get, $"/v1/sync?since={since}&limit={limit}", tokens.AccessToken));

    private sealed record ResultInput(Guid TopicId, int Total, int Correct, int Wrong, int Blank, int? MistakeKnowledge, int? MistakeAttention, int? MistakeTime);

    private sealed record TestBody(string StudiedOn, Guid? SourceId, string? SourceName, bool IsRepeat, string? Difficulty, int? DurationMinutes, string? Note, List<ResultInput> Results, DateTimeOffset ClientModifiedAt);

    private sealed record ResultItem(Guid TopicId, int Total, int Correct, int Wrong, int Blank, decimal Net);

    private sealed record TestItem(Guid Id, string StudiedOn, Guid? SourceId, string? SourceName, string? Difficulty, List<ResultItem> Results, bool Deleted, long ChangeSeq);

    private sealed record SyncItem(List<TestItem> TopicTests, long Cursor, bool HasMore);
}
