using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Marks an <c>IdentityEmailRequested</c> event as mailed, so a redelivered outbox message does not mail again.</summary>
internal sealed class SentEmail
{
    public Guid EventId { get; init; }

    public DateTimeOffset SentAt { get; init; }
}

internal sealed class SentEmailConfiguration : IEntityTypeConfiguration<SentEmail>
{
    public void Configure(EntityTypeBuilder<SentEmail> builder)
    {
        builder.ToTable("sent_emails");
        builder.HasKey(e => e.EventId);
        builder.HasIndex(e => e.SentAt);
    }
}
