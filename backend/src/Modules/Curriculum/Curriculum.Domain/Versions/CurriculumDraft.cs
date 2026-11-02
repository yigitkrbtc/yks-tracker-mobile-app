namespace Nirengi.Modules.Curriculum.Domain.Versions;

public enum DraftKind
{
    /// <summary>Edited by an admin; one per exam year.</summary>
    Manual = 0,

    /// <summary>Built from a changed data/seed at startup; replaced by the next seed change.</summary>
    SeedImport = 1,
}

/// <summary>Pending changes to the curriculum (ADR-0019): an ordered operation list, applied when published.</summary>
public sealed class CurriculumDraft
{
    public const int TitleMaxLength = 120;

    private CurriculumDraft()
    {
    }

    public Guid Id { get; private set; }

    public int ExamYear { get; private set; }

    public DraftKind Kind { get; private set; }

    /// <summary>The version the operations were written against; publishing a stale draft is refused.</summary>
    public int BasedOnNumber { get; private set; }

    public string Title { get; private set; } = null!;

    /// <summary>JSON array of <see cref="CurriculumOperation"/>.</summary>
    public string Operations { get; private set; } = null!;

    public string? SeedHash { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Bumped on every save; the If-Match token.</summary>
    public int Revision { get; private set; }

    public static CurriculumDraft Create(
        Guid id,
        int examYear,
        DraftKind kind,
        int basedOnNumber,
        string title,
        string operations,
        string? seedHash,
        Guid? createdBy,
        DateTimeOffset now) => new()
        {
            Id = id,
            ExamYear = examYear,
            Kind = kind,
            BasedOnNumber = basedOnNumber,
            Title = title,
            Operations = operations,
            SeedHash = seedHash,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
            Revision = 1,
        };

    public void Save(string title, string operations, int basedOnNumber, DateTimeOffset now)
    {
        Title = title;
        Operations = operations;
        BasedOnNumber = basedOnNumber;
        UpdatedAt = now;
        Revision++;
    }
}
