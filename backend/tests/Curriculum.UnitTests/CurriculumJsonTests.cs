using Nirengi.Modules.Curriculum.Domain.Versions;
using Shouldly;

namespace Nirengi.Curriculum.UnitTests;

public sealed class CurriculumJsonTests
{
    [Fact]
    public void Operations_round_trip_with_their_type()
    {
        CurriculumOperation[] ops =
        [
            new AddTopic(null, "TYT.MAT", "Ad", ["Alt"], 9, 10, null),
            new MergeTopics(["TYT.MAT.01"], "TYT.MAT.02"),
            new SetCalendar(DraftApplierTests.Calendar(2027, null)),
        ];

        var json = CurriculumJson.Serialize(ops);
        json.ShouldContain("\"type\":\"topic.add\"");

        var back = CurriculumJson.Operations(json);
        back.Select(o => o.GetType()).ShouldBe(ops.Select(o => o.GetType()));
    }

    [Fact]
    public void Type_may_come_after_other_keys_as_jsonb_returns_it()
    {
        var back = CurriculumJson.Operations("""[{"code":"TYT.MAT.01","type":"topic.retire"}]""");

        back.ShouldHaveSingleItem().ShouldBe(new RetireTopic("TYT.MAT.01"));
    }
}
