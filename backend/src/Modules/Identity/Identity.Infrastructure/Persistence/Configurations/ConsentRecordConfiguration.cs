using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Identity.Domain.Consents;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("consent_records");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.Version).HasMaxLength(20);
        builder.HasIndex(c => c.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
