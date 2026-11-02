using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Curriculum.Domain.Versions;

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence.Configurations;

internal sealed class CurriculumVersionConfiguration : IEntityTypeConfiguration<CurriculumVersion>
{
    public void Configure(EntityTypeBuilder<CurriculumVersion> builder)
    {
        builder.ToTable("curriculum_versions");
        builder.HasKey(v => new { v.ExamYear, v.Number });
        builder.Property(v => v.Summary).HasMaxLength(CurriculumVersion.SummaryMaxLength);
        builder.Property(v => v.ContentHash).HasMaxLength(CurriculumRelease.HashLength).IsFixedLength();
        builder.Property(v => v.SeedHash).HasMaxLength(CurriculumRelease.HashLength).IsFixedLength();
        builder.Property(v => v.Snapshot).HasColumnType("jsonb");
        builder.Property(v => v.Operations).HasColumnType("jsonb");
    }
}

internal sealed class CurriculumDraftConfiguration : IEntityTypeConfiguration<CurriculumDraft>
{
    public void Configure(EntityTypeBuilder<CurriculumDraft> builder)
    {
        builder.ToTable("curriculum_drafts");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(d => new { d.ExamYear, d.Kind }).IsUnique();
        builder.Property(d => d.Title).HasMaxLength(CurriculumDraft.TitleMaxLength);
        builder.Property(d => d.Operations).HasColumnType("jsonb");
        builder.Property(d => d.SeedHash).HasMaxLength(CurriculumRelease.HashLength).IsFixedLength();
        builder.Property(d => d.Revision).IsConcurrencyToken();
    }
}

internal sealed class TopicLineageConfiguration : IEntityTypeConfiguration<TopicLineage>
{
    public void Configure(EntityTypeBuilder<TopicLineage> builder)
    {
        builder.ToTable("topic_lineage");
        builder.HasKey(l => new { l.FromTopicId, l.ToTopicId });
        builder.Property(l => l.Relation).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Topic>().WithMany().HasForeignKey(l => l.FromTopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Topic>().WithMany().HasForeignKey(l => l.ToTopicId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StoredExamCalendarConfiguration : IEntityTypeConfiguration<StoredExamCalendar>
{
    public void Configure(EntityTypeBuilder<StoredExamCalendar> builder)
    {
        builder.ToTable("exam_calendars");
        builder.HasKey(c => c.ExamYear);
        builder.Property(c => c.ExamYear).ValueGeneratedNever();
        builder.Property(c => c.Data).HasColumnType("jsonb");
    }
}
