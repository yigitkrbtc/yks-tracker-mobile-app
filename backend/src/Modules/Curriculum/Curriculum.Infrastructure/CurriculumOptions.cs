namespace Nirengi.Modules.Curriculum.Infrastructure;

public sealed class CurriculumOptions
{
    public const string Section = "Curriculum";

    /// <summary>
    /// True: a changed data/seed is published at startup (development, tests). False: it becomes a "seed import" draft
    /// that an admin reviews and publishes (ADR-0019). An empty database is always seeded directly.
    /// </summary>
    public bool AutoPublishSeed { get; set; }
}
