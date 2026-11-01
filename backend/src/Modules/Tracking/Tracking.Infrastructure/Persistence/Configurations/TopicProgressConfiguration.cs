using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Tracking.Domain.Progress;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence.Configurations;

internal sealed class TopicProgressConfiguration : IEntityTypeConfiguration<TopicProgress>
{
    public void Configure(EntityTypeBuilder<TopicProgress> builder)
    {
        builder.ToTable("topic_progress");
        builder.HasKey(p => new { p.UserId, p.TopicId });
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasIndex(p => new { p.UserId, p.ChangeSeq });
    }
}
