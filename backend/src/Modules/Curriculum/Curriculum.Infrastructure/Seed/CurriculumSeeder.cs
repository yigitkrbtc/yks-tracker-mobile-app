using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain.Versions;
using Nirengi.Modules.Curriculum.Infrastructure.Persistence;

namespace Nirengi.Modules.Curriculum.Infrastructure.Seed;

/// <summary>
/// Brings data/seed into the database at startup (ADR-0013, ADR-0019). A no-op when the seed hash is already
/// loaded. Otherwise the difference from the live curriculum becomes a "seed import" draft: published at once on an
/// empty database or with <see cref="CurriculumOptions.AutoPublishSeed"/>, else left for an admin to review.
/// </summary>
public sealed partial class CurriculumSeeder
{
    private readonly CurriculumDbContext db;
    private readonly CurriculumPublisher publisher;
    private readonly IClock clock;
    private readonly CurriculumOptions options;
    private readonly ILogger<CurriculumSeeder> logger;

    public CurriculumSeeder(
        CurriculumDbContext db,
        CurriculumPublisher publisher,
        IClock clock,
        IOptions<CurriculumOptions> options,
        ILogger<CurriculumSeeder> logger)
    {
        this.db = db;
        this.publisher = publisher;
        this.clock = clock;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <returns>True when anything was written.</returns>
    public Task<bool> Seed(CancellationToken cancellationToken) => Apply(CurriculumSeed.LoadEmbedded(), cancellationToken);

    internal async Task<bool> Apply(CurriculumSeed seed, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Several API instances may start together; only one imports, the rest then see the hash and skip.
        await publisher.Lock(cancellationToken);

        var release = await db.Releases.SingleOrDefaultAsync(r => r.ExamYear == seed.ExamYear, cancellationToken);
        if (release is not null)
        {
            await publisher.BackfillFirstVersion(release, cancellationToken);
            if (release.SeedHash == seed.Hash)
            {
                // A manual publish since the seed draft was made leaves it stale; rebuild it against the live state.
                var stale = await db.Drafts.AnyAsync(
                    d => d.ExamYear == seed.ExamYear && d.Kind == DraftKind.SeedImport && d.BasedOnNumber != release.VersionNumber, cancellationToken);
                if (stale)
                {
                    await publisher.RebuildSeedDraft(seed, release, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return stale;
            }
        }

        if (release is null || options.AutoPublishSeed)
        {
            db.Drafts.RemoveRange(await db.Drafts.Where(d => d.ExamYear == seed.ExamYear && d.Kind == DraftKind.SeedImport).ToListAsync(cancellationToken));
            var operations = ContentDiff.Between(await publisher.LoadContent(cancellationToken), seed.Content);
            var now = clock.UtcNow;
            var draft = CurriculumDraft.Create(
                Guid.CreateVersion7(now),
                seed.ExamYear,
                DraftKind.SeedImport,
                release?.VersionNumber ?? 0,
                $"Seed içe aktarma {seed.Hash[..8]}",
                CurriculumJson.Serialize(operations),
                seed.Hash,
                null,
                now);
            var published = await publisher.PublishLocked(draft, null, $"Seed içe aktarma ({operations.Count} değişiklik)", seed, cancellationToken);
            if (published.IsFailure)
            {
                // A seed that cannot be applied must stop the deploy, as a broken seed file always did.
                throw new InvalidOperationException($"Seed import failed: {published.Error.Message}");
            }

            LogSeeded(logger, seed.ExamYear, published.Value.Number, operations.Count, seed.Hash);
        }
        else
        {
            await publisher.ApplySeedStructure(seed, release, cancellationToken);
            var changes = await publisher.RebuildSeedDraft(seed, release, cancellationToken);
            LogDraft(logger, seed.ExamYear, changes, seed.Hash);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Curriculum {ExamYear} seeded as version {Version}: {Changes} changes, hash {Hash}")]
    private static partial void LogSeeded(ILogger logger, int examYear, int version, int changes, string hash);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Curriculum {ExamYear}: seed {Hash} differs ({Changes} changes); an admin must publish the seed import draft")]
    private static partial void LogDraft(ILogger logger, int examYear, int changes, string hash);
}
