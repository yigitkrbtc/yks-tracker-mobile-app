using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Modules.Recommendation.Infrastructure.Persistence.Configurations;

internal sealed class StoredPlanConfiguration : IEntityTypeConfiguration<StoredPlan>
{
    public void Configure(EntityTypeBuilder<StoredPlan> builder)
    {
        builder.ToTable("weekly_plans");
        builder.HasKey(p => new { p.UserId, p.WeekStart });
        builder.Property(p => p.RuleSetVersion).HasMaxLength(20);
        builder.Property(p => p.AnalysisStamp).HasMaxLength(100);
        builder.Property(p => p.Items).HasColumnType("jsonb");
    }
}

internal sealed class ItemFeedbackConfiguration : IEntityTypeConfiguration<ItemFeedback>
{
    public void Configure(EntityTypeBuilder<ItemFeedback> builder)
    {
        builder.ToTable("item_feedback");
        builder.HasKey(f => new { f.UserId, f.WeekStart, f.ItemKey });
        builder.Property(f => f.ItemKey).HasMaxLength(100);
        builder.Property(f => f.Reason).HasConversion<string>().HasMaxLength(20);
    }
}
