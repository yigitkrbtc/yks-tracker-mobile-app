using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Analytics.Application.Snapshots;

namespace Nirengi.Modules.Analytics.Infrastructure.Persistence.Configurations;

internal sealed class UserAnalysisConfiguration : IEntityTypeConfiguration<UserAnalysis>
{
    public void Configure(EntityTypeBuilder<UserAnalysis> builder)
    {
        builder.ToTable("user_analyses");
        builder.HasKey(a => a.UserId);
        builder.Property(a => a.RuleSetVersion).HasMaxLength(20);
        builder.Property(a => a.SourceStamp).HasMaxLength(200);
        builder.Property(a => a.Summary).HasColumnType("jsonb");
    }
}

internal sealed class TopicSnapshotConfiguration : IEntityTypeConfiguration<TopicSnapshot>
{
    public void Configure(EntityTypeBuilder<TopicSnapshot> builder)
    {
        builder.ToTable("topic_snapshots");
        builder.HasKey(t => new { t.UserId, t.TopicId });
        builder.Property(t => t.ConfidenceLevel).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Band).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Trend).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Details).HasColumnType("jsonb");
    }
}

internal sealed class SectionSnapshotConfiguration : IEntityTypeConfiguration<SectionSnapshot>
{
    public void Configure(EntityTypeBuilder<SectionSnapshot> builder)
    {
        builder.ToTable("section_snapshots");
        builder.HasKey(s => new { s.UserId, s.SectionCode });
        builder.Property(s => s.SectionCode).HasMaxLength(20);
        builder.Property(s => s.Trend).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Details).HasColumnType("jsonb");
    }
}
