namespace Nirengi.Modules.Curriculum.Domain.Versions;

/// <summary>A published state of the curriculum (ADR-0019). Never changed after publishing; going back publishes a new one.</summary>
public sealed class CurriculumVersion
{
    public const int SummaryMaxLength = 300;

    private CurriculumVersion()
    {
    }

    public int ExamYear { get; private set; }

    public int Number { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>The admin who published; null for the system (seed import at startup).</summary>
    public Guid? PublishedBy { get; private set; }

    public string Summary { get; private set; } = null!;

    public string ContentHash { get; private set; } = null!;

    /// <summary>The seed import this version brought in, if any.</summary>
    public string? SeedHash { get; private set; }

    /// <summary>JSON of <see cref="CurriculumContent"/> right after publishing.</summary>
    public string Snapshot { get; private set; } = null!;

    /// <summary>JSON of the operations that produced it.</summary>
    public string Operations { get; private set; } = null!;

    public static CurriculumVersion Create(
        int examYear,
        int number,
        DateTimeOffset publishedAt,
        Guid? publishedBy,
        string summary,
        string contentHash,
        string? seedHash,
        string snapshot,
        string operations) => new()
        {
            ExamYear = examYear,
            Number = number,
            PublishedAt = publishedAt,
            PublishedBy = publishedBy,
            Summary = summary,
            ContentHash = contentHash,
            SeedHash = seedHash,
            Snapshot = snapshot,
            Operations = operations,
        };
}
