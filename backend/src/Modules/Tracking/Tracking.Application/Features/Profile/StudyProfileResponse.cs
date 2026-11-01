using Nirengi.Modules.Tracking.Domain.Profiles;

namespace Nirengi.Modules.Tracking.Application.Features.Profile;

public sealed record StudyProfileResponse(
    Grade Grade,
    ScoreType PrimaryScoreType,
    ScoreType? SecondaryScoreType,
    YdtLanguage? YdtLanguage,
    int TargetExamYear,
    int? WeeklyQuestionGoal,
    int? WeeklyActiveDaysGoal,
    DateTimeOffset UpdatedAt)
{
    public static StudyProfileResponse From(StudyProfile p) =>
        new(p.Grade, p.PrimaryScoreType, p.SecondaryScoreType, p.YdtLanguage, p.TargetExamYear, p.WeeklyQuestionGoal, p.WeeklyActiveDaysGoal, p.UpdatedAt);
}
