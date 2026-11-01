using Nirengi.Modules.Tracking.Domain.Progress;
using Shouldly;

namespace Nirengi.Tracking.UnitTests;

public sealed class TopicProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Newer_edit_wins_and_older_or_equal_changes_nothing()
    {
        var mark = TopicProgress.Create(Guid.NewGuid(), Guid.NewGuid(), TopicProgressStatus.InProgress, Now.AddMinutes(-10), Now);

        mark.Apply(TopicProgressStatus.Done, Now.AddMinutes(-20), Now).ShouldBeFalse();
        mark.Apply(TopicProgressStatus.Done, Now.AddMinutes(-10), Now).ShouldBeFalse();
        mark.Status.ShouldBe(TopicProgressStatus.InProgress);

        mark.Apply(TopicProgressStatus.NotStarted, Now.AddMinutes(-5), Now).ShouldBeTrue();
        mark.Status.ShouldBe(TopicProgressStatus.NotStarted);
        mark.ClientModifiedAt.ShouldBe(Now.AddMinutes(-5));
    }

    [Fact]
    public void Client_time_in_the_future_is_capped_at_server_time()
    {
        var mark = TopicProgress.Create(Guid.NewGuid(), Guid.NewGuid(), TopicProgressStatus.Done, Now.AddDays(3), Now);

        mark.ClientModifiedAt.ShouldBe(Now);
        mark.Apply(TopicProgressStatus.InProgress, Now.AddMinutes(1), Now.AddMinutes(1)).ShouldBeTrue();
    }
}
