using Nirengi.Modules.Tracking.Domain.Profiles;
using Shouldly;

namespace Nirengi.Tracking.UnitTests;

public sealed class StudyProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-06-30T20:59:00Z", 2026)]
    [InlineData("2026-06-30T21:00:00Z", 2027)] // 1 July 00:00 in Türkiye
    [InlineData("2026-10-28T09:00:00Z", 2027)]
    [InlineData("2027-01-15T09:00:00Z", 2027)]
    public void Next_exam_year_flips_after_june_in_turkish_time(string utc, int expected) =>
        ExamCalendar.NextExamYear(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);

    [Fact]
    public void Update_replaces_every_field_and_keeps_creation_time()
    {
        var profile = StudyProfile.Create(Guid.NewGuid(), Grade.Eleventh, ScoreType.SAY, ScoreType.EA, null, 2028, 200, 4, Now);

        profile.Update(Grade.Twelfth, ScoreType.DIL, null, YdtLanguage.English, 2027, null, null, Now.AddDays(1));

        profile.Grade.ShouldBe(Grade.Twelfth);
        profile.PrimaryScoreType.ShouldBe(ScoreType.DIL);
        profile.SecondaryScoreType.ShouldBeNull();
        profile.YdtLanguage.ShouldBe(YdtLanguage.English);
        profile.StudiesLanguage.ShouldBeTrue();
        profile.WeeklyQuestionGoal.ShouldBeNull();
        profile.CreatedAt.ShouldBe(Now);
        profile.UpdatedAt.ShouldBe(Now.AddDays(1));
    }

    [Fact]
    public void Same_score_type_twice_is_a_programming_error() =>
        Should.Throw<ArgumentException>(() => StudyProfile.Create(Guid.NewGuid(), Grade.Eleventh, ScoreType.EA, ScoreType.EA, null, 2027, null, null, Now));

    [Fact]
    public void Language_without_dil_is_a_programming_error() =>
        Should.Throw<ArgumentException>(() => StudyProfile.Create(Guid.NewGuid(), Grade.Eleventh, ScoreType.EA, null, YdtLanguage.French, 2027, null, null, Now));
}
