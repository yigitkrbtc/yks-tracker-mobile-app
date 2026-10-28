using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Domain;
using Nirengi.Modules.Tracking.Domain.Profiles;

namespace Nirengi.Modules.Tracking.Application.Features.Profile;

/// <summary>Creates or replaces the caller's study profile (idempotent PUT, ADR-0005).</summary>
public sealed record SaveStudyProfileCommand(
    Guid UserId,
    Grade Grade,
    ScoreType PrimaryScoreType,
    ScoreType? SecondaryScoreType,
    YdtLanguage? YdtLanguage,
    int TargetExamYear,
    int? WeeklyQuestionGoal) : ICommand<StudyProfileResponse>;

internal sealed class SaveStudyProfileValidator : AbstractValidator<SaveStudyProfileCommand>
{
    public SaveStudyProfileValidator(IClock clock)
    {
        RuleFor(c => c.Grade).IsInEnum();
        RuleFor(c => c.PrimaryScoreType).IsInEnum();
        RuleFor(c => c.SecondaryScoreType)
            .IsInEnum()
            .NotEqual(c => c.PrimaryScoreType).WithMessage("İkinci puan türü birincisinden farklı olmalı.");

        When(c => c.PrimaryScoreType == ScoreType.DIL || c.SecondaryScoreType == ScoreType.DIL, () =>
            RuleFor(c => c.YdtLanguage).NotNull().WithMessage("Dil puan türü için YDT dilini seç.").IsInEnum());
        When(c => c.PrimaryScoreType != ScoreType.DIL && c.SecondaryScoreType != ScoreType.DIL, () =>
            RuleFor(c => c.YdtLanguage).Null().WithMessage("YDT dili yalnızca dil puan türünde seçilir."));

        RuleFor(c => c.TargetExamYear).Must((_, year) =>
            {
                var next = ExamCalendar.NextExamYear(clock.UtcNow);
                return year >= next && year <= next + ExamCalendar.MaxYearsAhead;
            })
            .WithMessage("Hedef sınav yılı önümüzdeki dört YKS'den biri olmalı.");

        RuleFor(c => c.WeeklyQuestionGoal).InclusiveBetween(1, StudyProfile.MaxWeeklyQuestionGoal);
    }
}

internal sealed class SaveStudyProfileHandler(
    IStudyProfileRepository profiles,
    ITrackingUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<SaveStudyProfileCommand, StudyProfileResponse>
{
    public async Task<Result<StudyProfileResponse>> Handle(SaveStudyProfileCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var profile = await profiles.Get(command.UserId, cancellationToken);
        if (profile is null)
        {
            profile = StudyProfile.Create(
                command.UserId,
                command.Grade,
                command.PrimaryScoreType,
                command.SecondaryScoreType,
                command.YdtLanguage,
                command.TargetExamYear,
                command.WeeklyQuestionGoal,
                now);
            profiles.Add(profile);
        }
        else
        {
            profile.Update(
                command.Grade,
                command.PrimaryScoreType,
                command.SecondaryScoreType,
                command.YdtLanguage,
                command.TargetExamYear,
                command.WeeklyQuestionGoal,
                now);
        }

        // Two devices finishing onboarding at once: the loser retries and then updates the winner's row.
        return await unitOfWork.TrySaveChanges(cancellationToken)
            ? StudyProfileResponse.From(profile)
            : TrackingErrors.ProfileConflict;
    }
}
