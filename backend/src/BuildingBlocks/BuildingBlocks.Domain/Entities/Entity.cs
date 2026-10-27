namespace Nirengi.BuildingBlocks.Domain.Entities;

public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    // Parameterless constructor for the ORM only.
    protected Entity()
    {
        Id = default!;
    }

    public TId Id { get; protected set; }
}
