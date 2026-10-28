using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Tracking.Domain.Profiles;

/// <summary>
/// What the student is preparing for, set during onboarding. Drives which topics they see; changing the score
/// type later never touches their records, only the view and weights.
/// </summary>
public sealed class StudyProfile : AggregateRoot<Guid>
{
    public const int MaxWeeklyQuestionGoal = 5000;

    private StudyProfile()
    {
    }

    public Guid UserId => Id;

    public Grade Grade { get; private set; }

    public ScoreType PrimaryScoreType { get; private set; }

    /// <summary>Optional second type for students undecided between two (typically SAY and EA).</summary>
    public ScoreType? SecondaryScoreType { get; private set; }

    public YdtLanguage? YdtLanguage { get; private set; }

    public int TargetExamYear { get; private set; }

    public int? WeeklyQuestionGoal { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool StudiesLanguage => PrimaryScoreType == ScoreType.DIL || SecondaryScoreType == ScoreType.DIL;

    public static StudyProfile Create(
        Guid userId,
        Grade grade,
        ScoreType primary,
        ScoreType? secondary,
        YdtLanguage? ydtLanguage,
        int targetExamYear,
        int? weeklyQuestionGoal,
        DateTimeOffset now)
    {
        var profile = new StudyProfile { Id = userId, CreatedAt = now };
        profile.Update(grade, primary, secondary, ydtLanguage, targetExamYear, weeklyQuestionGoal, now);
        return profile;
    }

    /// <summary>Inputs are validated by the command validator; the guards here only catch programming errors.</summary>
    public void Update(
        Grade grade,
        ScoreType primary,
        ScoreType? secondary,
        YdtLanguage? ydtLanguage,
        int targetExamYear,
        int? weeklyQuestionGoal,
        DateTimeOffset now)
    {
        if (secondary == primary)
        {
            throw new ArgumentException("The second score type must differ from the first.", nameof(secondary));
        }

        if ((primary == ScoreType.DIL || secondary == ScoreType.DIL) != ydtLanguage.HasValue)
        {
            throw new ArgumentException("A YDT language is required exactly when DIL is chosen.", nameof(ydtLanguage));
        }

        Grade = grade;
        PrimaryScoreType = primary;
        SecondaryScoreType = secondary;
        YdtLanguage = ydtLanguage;
        TargetExamYear = targetExamYear;
        WeeklyQuestionGoal = weeklyQuestionGoal;
        UpdatedAt = now;
    }
}
