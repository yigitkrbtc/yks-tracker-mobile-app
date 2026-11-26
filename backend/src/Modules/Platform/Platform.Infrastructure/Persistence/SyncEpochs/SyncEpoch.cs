using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Platform.Contracts;

namespace Nirengi.Modules.Platform.Infrastructure.Persistence.SyncEpochs;

/// <summary>Read on every feed request (one-row primary-key table): a restore must show at once, so nothing is cached.</summary>
internal sealed class SyncEpoch(PlatformDbContext db) : ISyncEpoch
{
    public async Task<Guid> Current(CancellationToken cancellationToken) =>
        await db.Set<SyncEpochRow>().AsNoTracking().Select(e => e.Epoch).SingleAsync(cancellationToken);
}
