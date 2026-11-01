using Nirengi.Modules.Recommendation.Domain.Plans;
using Shouldly;

namespace Nirengi.Recommendation.UnitTests;

public sealed class PlanProgressTests
{
    [Theory]
    [InlineData("2026-11-02", "2026-11-02")] // Monday
    [InlineData("2026-11-08", "2026-11-02")] // Sunday
    [InlineData("2026-11-09", "2026-11-09")]
    public void Week_starts_on_monday(string day, string monday) =>
        Week.StartOf(DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(DateOnly.Parse(monday, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Topic_item_completes_at_its_target()
    {
        var s = new Scenario().Topic("A.1", "A", 3).State("A.1").Priority("A.1");
        var item = s.Build().Items.Single();
        var none = new HashSet<string>();

        PlanProgress.Of(item, new Dictionary<Guid, int> { [Scenario.Id("A.1")] = 29 }, none).ShouldBe(new ItemProgress(29, false));
        PlanProgress.Of(item, new Dictionary<Guid, int> { [Scenario.Id("A.1")] = 30 }, none).ShouldBe(new ItemProgress(30, true));
    }

    [Fact]
    public void Tagging_item_completes_when_the_section_is_tagged()
    {
        var item = new Scenario().Pending("E1", 1, "TYT-TUR").Build().Items.Single();
        var empty = new Dictionary<Guid, int>();

        PlanProgress.Of(item, empty, new HashSet<string> { item.Key }).Completed.ShouldBeFalse();
        PlanProgress.Of(item, empty, new HashSet<string>()).Completed.ShouldBeTrue();
    }
}
