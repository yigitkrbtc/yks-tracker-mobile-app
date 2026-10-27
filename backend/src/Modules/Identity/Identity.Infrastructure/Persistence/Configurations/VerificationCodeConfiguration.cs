using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Identity.Domain.Codes;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        builder.ToTable("verification_codes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Purpose).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.CodeHash).HasMaxLength(100);
        builder.HasIndex(c => new { c.UserId, c.Purpose }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property<uint>("Version").IsRowVersion();
    }
}
