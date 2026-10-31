using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Curriculum.Domain.Sources;

public enum SourceKind
{
    Publisher = 0,

    /// <summary>ÖSYM past papers, MEB material.</summary>
    Official = 1,

    /// <summary>School or course handouts.</summary>
    Institution = 2,
}

public enum SourceStatus
{
    Active = 0,

    /// <summary>Dropped from the seed; kept because study records may still point at it.</summary>
    Retired = 1,
}

/// <summary>Where a student's questions come from (a publisher, past papers). Feeds the source difficulty model (M4).</summary>
public sealed class Source : Entity<Guid>
{
    public const int CodeMaxLength = 30;

    public const int NameMaxLength = 100;

    private Source()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public SourceKind Kind { get; private set; }

    public SourceStatus Status { get; private set; }

    public int SortOrder { get; private set; }

    public static Source Create(string code, string name, SourceKind kind, int sortOrder)
    {
        var source = new Source { Id = SourceIdentity.For(code), Code = code };
        source.Update(name, kind, sortOrder);
        return source;
    }

    public void Update(string name, SourceKind kind, int sortOrder)
    {
        Name = name;
        Kind = kind;
        SortOrder = sortOrder;
        Status = SourceStatus.Active;
    }

    public void Retire() => Status = SourceStatus.Retired;
}

/// <summary>Source ids are UUID v5 of the code (own namespace). Never change the namespace.</summary>
public static class SourceIdentity
{
    public static readonly Guid Namespace = new("0b7e5d2c-4a91-4f3e-9d68-1c2b3a4f5e6d");

    public static Guid For(string sourceCode) => Uuid5.Create(Namespace, sourceCode);
}
