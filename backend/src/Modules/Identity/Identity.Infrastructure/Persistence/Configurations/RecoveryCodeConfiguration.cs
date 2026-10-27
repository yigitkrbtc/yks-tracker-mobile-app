using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Identity.Domain.Tokens;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.ToTable("recovery_codes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CodeHash).HasMaxLength(100);
        builder.HasIndex(c => c.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property<uint>("Version").IsRowVersion();
    }
}
