using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nirengi.Modules.Platform.Infrastructure.Persistence.SyncEpochs;

/// <summary>The single row of <c>platform.sync_epoch</c>; only a restore changes it (<c>deploy/bin/restore.sh</c>).</summary>
internal sealed class SyncEpochRow
{
    public Guid Epoch { get; private set; }
}

internal sealed class SyncEpochConfiguration : IEntityTypeConfiguration<SyncEpochRow>
{
    public void Configure(EntityTypeBuilder<SyncEpochRow> builder)
    {
        builder.ToTable("sync_epoch");
        builder.HasKey(e => e.Epoch);
        builder.Property(e => e.Epoch).ValueGeneratedNever();
    }
}
