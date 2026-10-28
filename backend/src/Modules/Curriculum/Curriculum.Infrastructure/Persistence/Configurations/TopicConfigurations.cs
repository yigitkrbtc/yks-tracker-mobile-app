using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence.Configurations;

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topics");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Code).HasMaxLength(Topic.CodeMaxLength);
        builder.HasIndex(t => t.Code).IsUnique();
        builder.Property(t => t.SubjectCode).HasMaxLength(Subject.CodeMaxLength);
        builder.HasIndex(t => t.SubjectCode);
        builder.Property(t => t.Name).HasMaxLength(Topic.NameMaxLength);
        builder.Property(t => t.Description).HasMaxLength(Topic.DescriptionMaxLength);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
    }
}

internal sealed class TopicWeightConfiguration : IEntityTypeConfiguration<TopicWeight>
{
    public void Configure(EntityTypeBuilder<TopicWeight> builder)
    {
        builder.ToTable("topic_weights");
        builder.HasKey(w => new { w.TopicId, w.ExamYear });
        builder.Property(w => w.ExpectedQuestions).HasPrecision(5, 2);
        builder.Property(w => w.Tier).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Topic>().WithMany().HasForeignKey(w => w.TopicId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CurriculumReleaseConfiguration : IEntityTypeConfiguration<CurriculumRelease>
{
    public void Configure(EntityTypeBuilder<CurriculumRelease> builder)
    {
        builder.ToTable("releases");
        builder.HasKey(r => r.ExamYear);
        builder.Property(r => r.ExamYear).ValueGeneratedNever();
        builder.Property(r => r.SeedHash).HasMaxLength(CurriculumRelease.HashLength).IsFixedLength();
    }
}
