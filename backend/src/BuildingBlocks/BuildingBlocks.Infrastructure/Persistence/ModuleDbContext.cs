using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Application.Events;

namespace Nirengi.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base for every module's DbContext: one PostgreSQL schema per module and an outbox table
/// in that same schema, so events commit atomically with the data that caused them.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options), IEventOutbox
{
    private readonly List<IIntegrationEvent> _pending = [];

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected abstract string Schema { get; }

    public void Enqueue(IIntegrationEvent integrationEvent) => _pending.Add(integrationEvent);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var integrationEvent in _pending)
        {
            var type = integrationEvent.GetType();
            OutboxMessages.Add(new OutboxMessage
            {
                Id = integrationEvent.Id,
                Type = type.AssemblyQualifiedName ?? type.FullName!,
                Payload = JsonSerializer.Serialize(integrationEvent, type),
                OccurredAt = integrationEvent.OccurredAt,
            });
        }

        _pending.Clear();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Type).HasMaxLength(500);
            b.Property(m => m.Payload).HasColumnType("jsonb");
            b.HasIndex(m => m.OccurredAt).HasFilter("processed_at IS NULL").HasDatabaseName("ix_outbox_messages_unprocessed");
        });

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
