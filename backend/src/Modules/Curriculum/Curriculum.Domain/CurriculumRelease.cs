namespace Nirengi.Modules.Curriculum.Domain;

/// <summary>
/// The current published curriculum of an exam year. <see cref="ContentHash"/> is the API's ETag and the version
/// other modules compare; it changes on every publish (ADR-0019). <see cref="SeedHash"/> is the last imported seed.
/// </summary>
public sealed class CurriculumRelease
{
    public const int HashLength = 64;

    private CurriculumRelease()
    {
    }

    public int ExamYear { get; private set; }

    public string SeedHash { get; private set; } = null!;

    public string ContentHash { get; private set; } = null!;

    /// <summary>Number of the latest published version (1 = the first seed import).</summary>
    public int VersionNumber { get; private set; }

    public DateTimeOffset SeededAt { get; private set; }

    public static CurriculumRelease Create(int examYear, string seedHash, DateTimeOffset now) =>
        new() { ExamYear = examYear, SeedHash = seedHash, ContentHash = seedHash, VersionNumber = 0, SeededAt = now };

    public void Reseed(string seedHash, DateTimeOffset now)
    {
        SeedHash = seedHash;
        SeededAt = now;
    }

    public void Publish(int versionNumber, string contentHash)
    {
        VersionNumber = versionNumber;
        ContentHash = contentHash;
    }
}
