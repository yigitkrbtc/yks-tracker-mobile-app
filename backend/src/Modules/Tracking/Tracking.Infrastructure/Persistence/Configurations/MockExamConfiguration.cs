using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Tracking.Domain.MockExams;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence.Configurations;

internal sealed class MockExamConfiguration : IEntityTypeConfiguration<MockExam>
{
    public void Configure(EntityTypeBuilder<MockExam> builder)
    {
        builder.ToTable("mock_exams", t => t.HasCheckConstraint(
            "ck_mock_exams_source",
            "deleted_at IS NOT NULL OR (source_id IS NULL) <> (source_name IS NULL)"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.SourceName).HasMaxLength(MockExamRules.SourceNameMaxLength);
        builder.Property(e => e.Title).HasMaxLength(MockExamRules.TitleMaxLength);
        builder.Property(e => e.Note).HasMaxLength(MockExamRules.NoteMaxLength);
        builder.Property<uint>("Version").IsRowVersion();

        // Change feed per user (GET /v1/sync) and the live history list.
        builder.HasIndex(e => new { e.UserId, e.ChangeSeq });
        builder.HasIndex(e => new { e.UserId, e.TakenOn }).HasFilter("deleted_at IS NULL");

        builder.HasMany(e => e.Sections).WithOne().HasForeignKey(s => s.MockExamId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Sections).HasField("_sections").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(e => e.Marks).WithOne().HasForeignKey(m => m.MockExamId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Marks).HasField("_marks").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.IsDeleted);
        builder.Ignore(e => e.DomainEvents);
    }
}

internal sealed class MockExamSectionConfiguration : IEntityTypeConfiguration<MockExamSection>
{
    public void Configure(EntityTypeBuilder<MockExamSection> builder)
    {
        builder.ToTable("mock_exam_sections", t => t.HasCheckConstraint(
            "ck_mock_exam_sections_counts",
            $"correct >= 0 AND wrong >= 0 AND blank >= 0 AND correct + wrong + blank = question_count AND question_count BETWEEN 1 AND {MockExamRules.MaxBranchQuestions}"));
        builder.HasKey(s => new { s.MockExamId, s.SectionCode });
        builder.Property(s => s.SectionCode).HasMaxLength(20);
        builder.Property(s => s.Net).HasPrecision(6, 2);
        builder.Property(s => s.TaggingStatus).HasConversion<string>().HasMaxLength(10);

        // Subject form (M11a) reads a student's sections over time.
        builder.HasIndex(s => new { s.UserId, s.SectionCode, s.TakenOn });
    }
}

internal sealed class MockExamTopicMarkConfiguration : IEntityTypeConfiguration<MockExamTopicMark>
{
    public void Configure(EntityTypeBuilder<MockExamTopicMark> builder)
    {
        builder.ToTable("mock_exam_topic_marks", t => t.HasCheckConstraint(
            "ck_mock_exam_topic_marks_counts",
            "wrong >= 0 AND blank >= 0 AND wrong + blank >= 1"));
        builder.HasKey(m => new { m.MockExamId, m.SectionCode, m.TopicId });
        builder.Property(m => m.SectionCode).HasMaxLength(20);

        // Exam miss signal (M11c) reads a student's marks per topic over time.
        builder.HasIndex(m => new { m.UserId, m.TopicId, m.TakenOn });
    }
}
