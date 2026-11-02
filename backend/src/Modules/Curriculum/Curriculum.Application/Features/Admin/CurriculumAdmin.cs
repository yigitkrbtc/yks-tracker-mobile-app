using FluentValidation;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Versions;

namespace Nirengi.Modules.Curriculum.Application.Features.Admin;

// ---- Read models -------------------------------------------------------------------------------------------------

/// <summary>The live curriculum as the admin panel shows it: retired topics included, weights per test.</summary>
public sealed record AdminCurriculumResponse(
    int ExamYear,
    int VersionNumber,
    string ContentHash,
    IReadOnlyList<ExamTestDto> Tests,
    IReadOnlyList<AdminSubjectDto> Subjects,
    IReadOnlyList<AdminSourceDto> Sources,
    IReadOnlyList<ExamCalendar> Calendars,
    IReadOnlyList<DraftSummaryDto> Drafts);

public sealed record AdminSubjectDto(string Code, string Name, IReadOnlyList<string> TestCodes, IReadOnlyList<AdminTopicDto> Topics);

public sealed record AdminTopicDto(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<string> Subtopics,
    int GradeMin,
    int GradeMax,
    string? Description,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<TopicWeightDto> Weights);

public sealed record AdminSourceDto(string Code, string Name, SourceKind Kind, bool IsActive);

public sealed record DraftSummaryDto(Guid Id, DraftKind Kind, string Title, int BasedOnNumber, int OperationCount, DateTimeOffset UpdatedAt, int Revision);

/// <param name="Problem">Why the draft cannot be published as it stands; null when it applies cleanly.</param>
public sealed record DraftValidationDto(bool IsValid, string? Problem);

/// <param name="IsStale">The draft was written against an older version; saving it again moves it to the current one.</param>
public sealed record DraftResponse(
    Guid Id,
    int ExamYear,
    DraftKind Kind,
    string Title,
    int BasedOnNumber,
    int CurrentNumber,
    bool IsStale,
    int Revision,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CurriculumOperation> Operations,
    DraftValidationDto Validation);

public sealed record VersionSummaryDto(int Number, DateTimeOffset PublishedAt, Guid? PublishedBy, string Summary, bool IsSeedImport, int OperationCount);

public sealed record VersionDetailDto(VersionSummaryDto Version, IReadOnlyList<CurriculumOperation> Operations, CurriculumContent Snapshot);

// ---- Messages ----------------------------------------------------------------------------------------------------

public sealed record GetAdminCurriculumQuery : IQuery<AdminCurriculumResponse>;

/// <param name="FromVersion">Null: an empty draft (or the open one). A number: a draft that takes the curriculum back to that version.</param>
public sealed record CreateDraftCommand(Guid UserId, int? FromVersion) : ICommand<DraftResponse>;

public sealed record GetDraftQuery(Guid DraftId) : IQuery<DraftResponse>;

/// <summary>Replaces the draft's operation list. Saving also moves the draft onto the current version.</summary>
public sealed record SaveDraftCommand(Guid DraftId, int Revision, string Title, IReadOnlyList<CurriculumOperation> Operations) : ICommand<DraftResponse>;

public sealed record PublishDraftCommand(Guid UserId, Guid DraftId, int Revision, string Summary) : ICommand<VersionSummaryDto>;

public sealed record DeleteDraftCommand(Guid DraftId) : ICommand<Unit>;

public sealed record GetVersionsQuery : IQuery<IReadOnlyList<VersionSummaryDto>>;

public sealed record GetVersionQuery(int Number) : IQuery<VersionDetailDto>;

public sealed record ExportSeedQuery : IQuery<byte[]>;

internal sealed class SaveDraftValidator : AbstractValidator<SaveDraftCommand>
{
    public SaveDraftValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(CurriculumDraft.TitleMaxLength);
        RuleFor(c => c.Operations).NotNull();
        RuleFor(c => c.Operations.Count).LessThanOrEqualTo(DraftApplier.MaxOperations).When(c => c.Operations is not null);
        RuleForEach(c => c.Operations).NotNull();
    }
}

internal sealed class PublishDraftValidator : AbstractValidator<PublishDraftCommand>
{
    public PublishDraftValidator() =>
        RuleFor(c => c.Summary).NotEmpty().WithErrorCode(CurriculumErrors.SummaryRequired.Code).MaximumLength(CurriculumVersion.SummaryMaxLength);
}

// ---- Handlers ----------------------------------------------------------------------------------------------------

internal sealed class GetAdminCurriculumHandler(ICurriculumAdminStore store) : IQueryHandler<GetAdminCurriculumQuery, AdminCurriculumResponse>
{
    public async Task<Result<AdminCurriculumResponse>> Handle(GetAdminCurriculumQuery query, CancellationToken cancellationToken)
    {
        var release = await store.GetRelease(cancellationToken);
        if (release is null)
        {
            return CurriculumErrors.NotSeeded;
        }

        var content = await store.LoadContent(cancellationToken);
        var tests = await store.GetTests(cancellationToken);
        var testOrder = tests.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var weights = (await store.GetWeights(release.ExamYear, cancellationToken)).ToLookup(w => w.TopicId);
        var topics = content.Topics.ToLookup(t => t.SubjectCode);
        var drafts = await store.GetDrafts(cancellationToken);

        return new AdminCurriculumResponse(
            release.ExamYear,
            release.VersionNumber,
            release.ContentHash,
            [.. tests.Select(t => new ExamTestDto(t.Id, t.Session, t.Name, t.QuestionCount))],
            [.. (await store.GetSubjects(cancellationToken)).Select(s => new AdminSubjectDto(
                s.Id,
                s.Name,
                s.TestCodes,
                [.. topics[s.Id].OrderBy(t => t.SortOrder).ThenBy(t => t.Code, StringComparer.Ordinal).Select(t =>
                {
                    var id = Domain.Topics.TopicIdentity.For(t.Code);
                    return new AdminTopicDto(
                        id,
                        t.Code,
                        t.Name,
                        t.Subtopics,
                        t.GradeMin,
                        t.GradeMax,
                        t.Description,
                        t.SortOrder,
                        t.IsActive,
                        [.. weights[id].OrderBy(w => testOrder.GetValueOrDefault(w.TestCode)).Select(w => new TopicWeightDto(w.TestCode, w.ExpectedQuestions, w.Tier))]);
                })]))],
            [.. content.Sources.Select(s => new AdminSourceDto(s.Code, s.Name, s.Kind, s.IsActive))],
            content.Calendars,
            [.. drafts.Where(d => d.ExamYear == release.ExamYear).Select(Drafts.Summary)]);
    }
}

internal sealed class CreateDraftHandler(ICurriculumAdminStore store, IClock clock) : ICommandHandler<CreateDraftCommand, DraftResponse>
{
    public async Task<Result<DraftResponse>> Handle(CreateDraftCommand command, CancellationToken cancellationToken)
    {
        var release = await store.GetRelease(cancellationToken);
        if (release is null)
        {
            return CurriculumErrors.NotSeeded;
        }

        var open = await store.GetManualDraft(release.ExamYear, cancellationToken);
        if (open is not null && command.FromVersion is null)
        {
            return await Drafts.Response(store, open, release, cancellationToken);
        }

        IReadOnlyList<CurriculumOperation> operations = [];
        var title = "Yeni taslak";
        if (command.FromVersion is { } number)
        {
            if (open is not null)
            {
                return Error.Conflict("curriculum.draft_open", "Önce açık taslağı yayınla veya sil.");
            }

            var version = await store.GetVersion(release.ExamYear, number, cancellationToken);
            if (version is null)
            {
                return CurriculumErrors.VersionNotFound;
            }

            operations = ContentDiff.Between(await store.LoadContent(cancellationToken), CurriculumJson.Deserialize<CurriculumContent>(version.Snapshot));
            title = $"{number}. sürüme dönüş";
        }

        var now = clock.UtcNow;
        var draft = CurriculumDraft.Create(
            Guid.CreateVersion7(now), release.ExamYear, DraftKind.Manual, release.VersionNumber, title, CurriculumJson.Serialize(operations), null, command.UserId, now);
        store.AddDraft(draft);
        if (!await store.TrySaveChanges(cancellationToken))
        {
            return Error.Conflict("curriculum.draft_open", "Bu sınav yılı için zaten açık bir taslak var.");
        }

        return await Drafts.Response(store, draft, release, cancellationToken);
    }
}

internal sealed class GetDraftHandler(ICurriculumAdminStore store) : IQueryHandler<GetDraftQuery, DraftResponse>
{
    public async Task<Result<DraftResponse>> Handle(GetDraftQuery query, CancellationToken cancellationToken)
    {
        var draft = await store.GetDraft(query.DraftId, cancellationToken);
        var release = await store.GetRelease(cancellationToken);
        return draft is null || release is null ? CurriculumErrors.DraftNotFound : await Drafts.Response(store, draft, release, cancellationToken);
    }
}

internal sealed class SaveDraftHandler(ICurriculumAdminStore store, IClock clock) : ICommandHandler<SaveDraftCommand, DraftResponse>
{
    public async Task<Result<DraftResponse>> Handle(SaveDraftCommand command, CancellationToken cancellationToken)
    {
        var draft = await store.GetDraft(command.DraftId, cancellationToken);
        var release = await store.GetRelease(cancellationToken);
        if (draft is null || release is null)
        {
            return CurriculumErrors.DraftNotFound;
        }

        if (draft.Revision != command.Revision)
        {
            return CurriculumErrors.DraftChanged;
        }

        // New topics get their permanent code now, so later operations (merge, split) and the admin can refer to it.
        var content = await store.LoadContent(cancellationToken);
        var used = content.Topics.Select(t => t.Code).ToHashSet(StringComparer.Ordinal);
        var operations = new List<CurriculumOperation>(command.Operations.Count);
        foreach (var op in command.Operations)
        {
            if (op is AddTopic { Code: null or "" } add && add.SubjectCode is not null)
            {
                var code = DraftApplier.NextCode(add.SubjectCode, used);
                used.Add(code);
                operations.Add(add with { Code = code });
            }
            else
            {
                if (op is AddTopic { Code: { } given })
                {
                    used.Add(given);
                }

                operations.Add(op);
            }
        }

        draft.Save(command.Title.Trim(), CurriculumJson.Serialize<IReadOnlyList<CurriculumOperation>>(operations), release.VersionNumber, clock.UtcNow);
        if (!await store.TrySaveChanges(cancellationToken))
        {
            return CurriculumErrors.DraftChanged;
        }

        return await Drafts.Response(store, draft, release, cancellationToken);
    }
}

internal sealed class PublishDraftHandler(ICurriculumAdminStore store) : ICommandHandler<PublishDraftCommand, VersionSummaryDto>
{
    public async Task<Result<VersionSummaryDto>> Handle(PublishDraftCommand command, CancellationToken cancellationToken)
    {
        var published = await store.Publish(command.DraftId, command.Revision, command.UserId, command.Summary.Trim(), cancellationToken);
        return published.IsFailure ? published.Error : Drafts.Summary(published.Value);
    }
}

internal sealed class DeleteDraftHandler(ICurriculumAdminStore store) : ICommandHandler<DeleteDraftCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteDraftCommand command, CancellationToken cancellationToken)
    {
        var draft = await store.GetDraft(command.DraftId, cancellationToken);
        if (draft is null)
        {
            return Unit.Value; // already gone
        }

        store.RemoveDraft(draft);
        return await store.TrySaveChanges(cancellationToken) ? Unit.Value : CurriculumErrors.DraftChanged;
    }
}

internal sealed class GetVersionsHandler(ICurriculumAdminStore store) : IQueryHandler<GetVersionsQuery, IReadOnlyList<VersionSummaryDto>>
{
    public async Task<Result<IReadOnlyList<VersionSummaryDto>>> Handle(GetVersionsQuery query, CancellationToken cancellationToken)
    {
        var release = await store.GetRelease(cancellationToken);
        if (release is null)
        {
            return CurriculumErrors.NotSeeded;
        }

        return (await store.GetVersions(release.ExamYear, cancellationToken)).Select(Drafts.Summary).ToList();
    }
}

internal sealed class GetVersionHandler(ICurriculumAdminStore store) : IQueryHandler<GetVersionQuery, VersionDetailDto>
{
    public async Task<Result<VersionDetailDto>> Handle(GetVersionQuery query, CancellationToken cancellationToken)
    {
        var release = await store.GetRelease(cancellationToken);
        var version = release is null ? null : await store.GetVersion(release.ExamYear, query.Number, cancellationToken);
        return version is null
            ? CurriculumErrors.VersionNotFound
            : new VersionDetailDto(Drafts.Summary(version), CurriculumJson.Operations(version.Operations), CurriculumJson.Deserialize<CurriculumContent>(version.Snapshot));
    }
}

internal sealed class ExportSeedHandler(ICurriculumAdminStore store) : IQueryHandler<ExportSeedQuery, byte[]>
{
    public async Task<Result<byte[]>> Handle(ExportSeedQuery query, CancellationToken cancellationToken) =>
        await store.ExportSeed(cancellationToken);
}

internal static class Drafts
{
    public static DraftSummaryDto Summary(CurriculumDraft d) =>
        new(d.Id, d.Kind, d.Title, d.BasedOnNumber, CurriculumJson.Operations(d.Operations).Count, d.UpdatedAt, d.Revision);

    public static VersionSummaryDto Summary(CurriculumVersion v) =>
        new(v.Number, v.PublishedAt, v.PublishedBy, v.Summary, v.SeedHash is not null, CurriculumJson.Operations(v.Operations).Count);

    /// <summary>The draft with a dry run of publishing it against the live curriculum.</summary>
    public static async Task<DraftResponse> Response(ICurriculumAdminStore store, CurriculumDraft draft, CurriculumRelease release, CancellationToken cancellationToken)
    {
        var operations = CurriculumJson.Operations(draft.Operations);
        var content = await store.LoadContent(cancellationToken);
        var subjects = (await store.GetSubjects(cancellationToken)).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var applied = DraftApplier.Apply(content, subjects, content.Topics.Select(t => t.Code).ToHashSet(StringComparer.Ordinal), operations);
        return new DraftResponse(
            draft.Id,
            draft.ExamYear,
            draft.Kind,
            draft.Title,
            draft.BasedOnNumber,
            release.VersionNumber,
            draft.BasedOnNumber != release.VersionNumber,
            draft.Revision,
            draft.UpdatedAt,
            operations,
            new DraftValidationDto(applied.IsSuccess, applied.IsFailure ? applied.Error.Message : null));
    }
}
