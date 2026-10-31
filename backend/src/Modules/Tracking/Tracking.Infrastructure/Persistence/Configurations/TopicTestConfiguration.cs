using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence.Configurations;

internal sealed class TopicTestConfiguration : IEntityTypeConfiguration<TopicTest>
{
    public void Configure(EntityTypeBuilder<TopicTest> builder)
    {
        builder.ToTable("topic_tests", t => t.HasCheckConstraint(
            "ck_topic_tests_source",
            "deleted_at IS NOT NULL OR (source_id IS NULL) <> (source_name IS NULL)"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.SourceName).HasMaxLength(TopicTestRules.SourceNameMaxLength);
        builder.Property(t => t.Note).HasMaxLength(TopicTestRules.NoteMaxLength);
        builder.Property(t => t.Difficulty).HasConversion<string>().HasMaxLength(10);
        builder.Property<uint>("Version").IsRowVersion();

        // Change feed per user (GET /v1/sync) and the live history list.
        builder.HasIndex(t => new { t.UserId, t.ChangeSeq });
        builder.HasIndex(t => new { t.UserId, t.StudiedOn }).HasFilter("deleted_at IS NULL");

        builder.HasMany(t => t.Results).WithOne().HasForeignKey(r => r.TopicTestId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Results).HasField("_results").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(t => t.IsDeleted);
        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class TopicResultConfiguration : IEntityTypeConfiguration<TopicResult>
{
    public void Configure(EntityTypeBuilder<TopicResult> builder)
    {
        builder.ToTable("topic_test_results", t => t.HasCheckConstraint(
            "ck_topic_test_results_counts",
            $"correct >= 0 AND wrong >= 0 AND blank >= 0 AND correct + wrong + blank = total AND total BETWEEN 1 AND {TopicTestRules.MaxQuestionsPerTopic}"));
        builder.HasKey(r => new { r.TopicTestId, r.TopicId });
        builder.Property(r => r.Net).HasPrecision(6, 2);

        // Analytics (S5) reads a student's results per topic over time.
        builder.HasIndex(r => new { r.UserId, r.TopicId, r.StudiedOn });
    }
}
