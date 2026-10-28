namespace Nirengi.Modules.Curriculum.Domain;

/// <summary>Which seed content is loaded for an exam year; the hash doubles as the API's ETag.</summary>
public sealed class CurriculumRelease
{
    public const int HashLength = 64;

    private CurriculumRelease()
    {
    }

    public int ExamYear { get; private set; }

    public string SeedHash { get; private set; } = null!;

    public DateTimeOffset SeededAt { get; private set; }

    public static CurriculumRelease Create(int examYear, string seedHash, DateTimeOffset now) =>
        new() { ExamYear = examYear, SeedHash = seedHash, SeededAt = now };

    public void Reseed(string seedHash, DateTimeOffset now)
    {
        SeedHash = seedHash;
        SeededAt = now;
    }
}
