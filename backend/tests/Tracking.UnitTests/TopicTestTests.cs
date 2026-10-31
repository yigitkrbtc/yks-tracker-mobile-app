using Nirengi.Modules.Tracking.Domain.TopicTests;
using Shouldly;

namespace Nirengi.Tracking.UnitTests;

public sealed class TopicTestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly Guid TopicA = Guid.NewGuid();
    private static readonly Guid TopicB = Guid.NewGuid();

    private static TopicTestData Data(params TopicResultData[] results) =>
        new(new DateOnly(2026, 10, 31), null, "Deneme kitabı", false, null, null, null, results.Length == 0 ? [new(TopicA, 10, 6, 2, 2)] : results);

    [Fact]
    public void Net_is_correct_minus_a_quarter_of_wrong_and_can_be_negative()
    {
        var test = TopicTest.Create(Guid.CreateVersion7(), User, Data(new TopicResultData(TopicA, 20, 12, 5, 3), new TopicResultData(TopicB, 10, 1, 8, 1)), Now, Now);

        test.Results.Single(r => r.TopicId == TopicA).Net.ShouldBe(10.75m);
        test.Results.Single(r => r.TopicId == TopicB).Net.ShouldBe(-1m);
        test.Results.ShouldAllBe(r => r.UserId == User && r.StudiedOn == new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void Newer_edit_replaces_content_and_updates_rows_in_place()
    {
        var test = TopicTest.Create(Guid.CreateVersion7(), User, Data(new TopicResultData(TopicA, 10, 5, 5, 0), new TopicResultData(TopicB, 10, 5, 5, 0)), Now.AddMinutes(-10), Now);
        var rowA = test.Results.Single(r => r.TopicId == TopicA);

        test.Apply(Data(new TopicResultData(TopicA, 10, 9, 1, 0)) with { Note = "  tekrar  " }, Now.AddMinutes(-5), Now).ShouldBeTrue();

        test.Results.ShouldHaveSingleItem().ShouldBeSameAs(rowA);
        rowA.Correct.ShouldBe(9);
        test.Note.ShouldBe("tekrar");
        test.ClientModifiedAt.ShouldBe(Now.AddMinutes(-5));
    }

    [Fact]
    public void Older_or_replayed_edit_changes_nothing()
    {
        var test = TopicTest.Create(Guid.CreateVersion7(), User, Data(), Now.AddMinutes(-5), Now);

        test.Apply(Data(new TopicResultData(TopicA, 10, 0, 10, 0)), Now.AddMinutes(-6), Now).ShouldBeFalse();
        test.Apply(Data(new TopicResultData(TopicA, 10, 0, 10, 0)), Now.AddMinutes(-5), Now).ShouldBeFalse();

        test.Results.Single().Correct.ShouldBe(6);
    }

    [Fact]
    public void Future_client_time_is_capped_at_server_time()
    {
        var test = TopicTest.Create(Guid.CreateVersion7(), User, Data(), Now.AddDays(3), Now);

        test.ClientModifiedAt.ShouldBe(Now);
        test.Apply(Data(new TopicResultData(TopicA, 10, 10, 0, 0)), Now.AddSeconds(1), Now.AddSeconds(1)).ShouldBeTrue();
    }

    [Fact]
    public void Edit_time_is_cut_to_microseconds_so_a_stored_replay_still_matches()
    {
        var clientTime = Now.AddMinutes(-1).AddTicks(7);
        var test = TopicTest.Create(Guid.CreateVersion7(), User, Data(), clientTime, Now);

        test.ClientModifiedAt.UtcTicks.ShouldBe(Now.AddMinutes(-1).UtcTicks);
        test.Apply(Data(), clientTime, Now).ShouldBeFalse();
    }

    [Fact]
    public void Delete_leaves_a_tombstone_and_ignores_stale_or_repeated_deletes()
    {
        var test = TopicTest.Create(Guid.CreateVersion7(), User, Data(), Now.AddMinutes(-5), Now.AddMinutes(-5));

        test.Delete(Now.AddMinutes(-6), Now).ShouldBeFalse();
        test.Delete(Now.AddMinutes(-1), Now).ShouldBeTrue();
        test.Delete(Now, Now).ShouldBeFalse();

        test.IsDeleted.ShouldBeTrue();
        test.DeletedAt.ShouldBe(Now);
        test.Results.ShouldBeEmpty();
    }

    [Fact]
    public void Edit_made_after_a_delete_restores_the_test()
    {
        var test = TopicTest.Create(Guid.CreateVersion7(), User, Data(), Now.AddMinutes(-5), Now);
        test.Delete(Now.AddMinutes(-4), Now);

        test.Apply(Data(), Now.AddMinutes(-3), Now).ShouldBeTrue();

        test.IsDeleted.ShouldBeFalse();
        test.Results.ShouldHaveSingleItem();
    }

    [Fact]
    public void Listed_source_drops_free_text_name()
    {
        var source = Guid.NewGuid();
        var test = TopicTest.Create(Guid.CreateVersion7(), User, Data() with { SourceId = source, SourceName = null }, Now, Now);

        test.SourceId.ShouldBe(source);
        test.SourceName.ShouldBeNull();
    }

    public static TheoryData<TopicTestData> Invalid => new()
    {
        Data() with { Results = [] },
        Data() with { Results = [.. Enumerable.Range(0, TopicTestRules.MaxTopics + 1).Select(_ => new TopicResultData(Guid.NewGuid(), 1, 1, 0, 0))] },
        Data(new TopicResultData(TopicA, 5, 5, 0, 0), new TopicResultData(TopicA, 5, 5, 0, 0)),
        Data(new TopicResultData(TopicA, 10, 6, 2, 1)),
        Data(new TopicResultData(TopicA, 0, 0, 0, 0)),
        Data(new TopicResultData(TopicA, 201, 201, 0, 0)),
        Data(new TopicResultData(TopicA, 10, 11, -1, 0)),
        Data(new TopicResultData(TopicA, 10, 6, 2, 2, MistakeKnowledge: 2, MistakeTime: 1)),
        Data() with { SourceName = null },
        Data() with { SourceId = Guid.NewGuid() },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Invalid_content_is_a_programming_error(TopicTestData data) =>
        Should.Throw<ArgumentException>(() => TopicTest.Create(Guid.CreateVersion7(), User, data, Now, Now));

    [Theory]
    [InlineData("2026-10-31T20:59:00Z", "2026-10-31", true)]
    [InlineData("2026-10-31T20:59:00Z", "2026-11-01", false)] // still 31 Oct in Türkiye
    [InlineData("2026-10-31T21:00:00Z", "2026-11-01", true)] // midnight in Türkiye
    [InlineData("2026-10-31T09:00:00Z", "2025-10-31", true)]
    [InlineData("2026-10-31T09:00:00Z", "2025-10-30", false)]
    public void Study_date_is_today_or_up_to_a_year_back_in_turkish_time(string utcNow, string date, bool allowed) =>
        TopicTestRules.IsAllowedStudyDate(
            DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(utcNow, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(allowed);
}
