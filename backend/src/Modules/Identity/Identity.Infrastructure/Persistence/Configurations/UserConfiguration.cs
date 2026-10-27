using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.FirstName).HasMaxLength(User.NameMaxLength);
        builder.Property(u => u.LastName).HasMaxLength(User.NameMaxLength);
        builder.Property(u => u.Email).HasMaxLength(Email.MaxLength);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(Email.MaxLength);
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
        builder.Property(u => u.PasswordHash).HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(u => u.TotpSecret).HasMaxLength(500);
        builder.Property(u => u.PendingTotpSecret).HasMaxLength(500);
        builder.HasIndex(u => u.DeletionRequestedAt).HasFilter("deletion_requested_at IS NOT NULL");
        builder.Property<uint>("Version").IsRowVersion();
        builder.Ignore(u => u.IsAdmin);
        builder.Ignore(u => u.TotpEnabled);
        builder.Ignore(u => u.DomainEvents);
    }
}
