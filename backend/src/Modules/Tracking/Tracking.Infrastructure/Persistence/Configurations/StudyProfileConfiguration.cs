using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Tracking.Domain.Profiles;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence.Configurations;

internal sealed class StudyProfileConfiguration : IEntityTypeConfiguration<StudyProfile>
{
    public void Configure(EntityTypeBuilder<StudyProfile> builder)
    {
        builder.ToTable("study_profiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(p => p.Grade).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.PrimaryScoreType).HasConversion<string>().HasMaxLength(10);
        builder.Property(p => p.SecondaryScoreType).HasConversion<string>().HasMaxLength(10);
        builder.Property(p => p.YdtLanguage).HasConversion<string>().HasMaxLength(20);
        builder.Property<uint>("Version").IsRowVersion();
        builder.Ignore(p => p.UserId);
        builder.Ignore(p => p.StudiesLanguage);
        builder.Ignore(p => p.DomainEvents);
    }
}
