using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Curriculum.Domain.Exams;

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence.Configurations;

internal sealed class ExamTestConfiguration : IEntityTypeConfiguration<ExamTest>
{
    public void Configure(EntityTypeBuilder<ExamTest> builder)
    {
        builder.ToTable("exam_tests");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("code").HasMaxLength(ExamTest.CodeMaxLength).ValueGeneratedNever();
        builder.Property(t => t.Session).HasConversion<string>().HasMaxLength(10);
        builder.Property(t => t.Name).HasMaxLength(ExamTest.NameMaxLength);
    }
}

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("code").HasMaxLength(Subject.CodeMaxLength).ValueGeneratedNever();
        builder.Property(s => s.Session).HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.Name).HasMaxLength(Subject.NameMaxLength);
    }
}

internal sealed class ScoreTypeConfiguration : IEntityTypeConfiguration<ScoreType>
{
    public void Configure(EntityTypeBuilder<ScoreType> builder)
    {
        builder.ToTable("score_types");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("code").HasMaxLength(ScoreType.CodeMaxLength).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(ScoreType.NameMaxLength);
    }
}

internal sealed class MockSectionConfiguration : IEntityTypeConfiguration<MockSection>
{
    public void Configure(EntityTypeBuilder<MockSection> builder)
    {
        builder.ToTable("mock_sections");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("code").HasMaxLength(MockSection.CodeMaxLength).ValueGeneratedNever();
        builder.Property(s => s.TestCode).HasMaxLength(ExamTest.CodeMaxLength);
        builder.Property(s => s.SubjectCode).HasMaxLength(Subject.CodeMaxLength);
        builder.Property(s => s.Name).HasMaxLength(MockSection.NameMaxLength);
        builder.Property(s => s.ParentCode).HasMaxLength(MockSection.CodeMaxLength);
        builder.Property(s => s.AlternativeOf).HasMaxLength(MockSection.CodeMaxLength);
    }
}
