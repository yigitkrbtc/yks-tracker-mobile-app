using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Domain.TopicTests;
using Nirengi.Modules.Tracking.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Topic tests round-trip through PostgreSQL and get ordered change-feed positions (S3, ADR-0005).</summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class TopicTestPersistenceTests(NirengiApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid TopicA = TopicIdentity.For("TYT.BIY.01");
    private static readonly Guid TopicB = TopicIdentity.For("TYT.BIY.02");

    private static TopicTestData Data(params TopicResultData[] results) =>
        new(new DateOnly(2026, 10, 30), null, "Kendi notlarım", false, Difficulty.Medium, 25, null, results);

    [Fact]
    public async Task Edits_update_rows_in_place_and_each_save_moves_the_change_feed()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        var user = Guid.CreateVersion7();

        long firstSeq;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var test = TopicTest.Create(id, user, Data(new TopicResultData(TopicA, 10, 6, 2, 2), new TopicResultData(TopicB, 10, 5, 5, 0)), Now, Now);
            scope.ServiceProvider.GetRequiredService<ITopicTestRepository>().Add(test);
            (await scope.ServiceProvider.GetRequiredService<ITrackingUnitOfWork>().TrySaveChanges(ct)).ShouldBeTrue();
            firstSeq = test.ChangeSeq;
            firstSeq.ShouldBeGreaterThan(0);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var test = (await scope.ServiceProvider.GetRequiredService<ITopicTestRepository>().Get(id, ct)).ShouldNotBeNull();
            test.Apply(Data(new TopicResultData(TopicA, 12, 10, 2, 0)), Now.AddMinutes(1), Now.AddMinutes(1)).ShouldBeTrue();
            (await scope.ServiceProvider.GetRequiredService<ITrackingUnitOfWork>().TrySaveChanges(ct)).ShouldBeTrue();
            test.ChangeSeq.ShouldBeGreaterThan(firstSeq);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().TopicTests
                .AsNoTracking().Include(t => t.Results).SingleAsync(t => t.Id == id, ct);
            var result = stored.Results.ShouldHaveSingleItem();
            (result.TopicId, result.Total, result.Correct, result.Net).ShouldBe((TopicA, 12, 10, 9.5m));
            stored.DurationMinutes.ShouldBe(25);
            stored.Difficulty.ShouldBe(Difficulty.Medium);
        }
    }

    [Fact]
    public async Task Concurrent_writes_of_one_user_commit_in_change_feed_order()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = Guid.CreateVersion7();

        var seqs = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var test = TopicTest.Create(Guid.CreateVersion7(), user, Data(new TopicResultData(TopicA, 5, 5, 0, 0)), Now, Now);
            scope.ServiceProvider.GetRequiredService<ITopicTestRepository>().Add(test);
            (await scope.ServiceProvider.GetRequiredService<ITrackingUnitOfWork>().TrySaveChanges(ct)).ShouldBeTrue();
            return test.ChangeSeq;
        }));

        seqs.Distinct().Count().ShouldBe(10);
        await using var check = factory.Services.CreateAsyncScope();
        var stored = await check.ServiceProvider.GetRequiredService<TrackingDbContext>().TopicTests
            .AsNoTracking().Where(t => t.UserId == user).OrderBy(t => t.ChangeSeq).Select(t => t.ChangeSeq).ToListAsync(ct);
        stored.ShouldBe(seqs.Order());
    }

    [Fact]
    public async Task Deleting_a_user_removes_tests_and_results()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = Guid.CreateVersion7();
        await using var scope = factory.Services.CreateAsyncScope();
        var tests = scope.ServiceProvider.GetRequiredService<ITopicTestRepository>();
        tests.Add(TopicTest.Create(Guid.CreateVersion7(), user, Data(new TopicResultData(TopicA, 5, 5, 0, 0)), Now, Now));
        (await scope.ServiceProvider.GetRequiredService<ITrackingUnitOfWork>().TrySaveChanges(ct)).ShouldBeTrue();

        await tests.DeleteForUser(user, ct);

        var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
        (await db.TopicTests.AsNoTracking().AnyAsync(t => t.UserId == user, ct)).ShouldBeFalse();
        (await db.Set<TopicResult>().AsNoTracking().AnyAsync(r => r.UserId == user, ct)).ShouldBeFalse();
    }
}
