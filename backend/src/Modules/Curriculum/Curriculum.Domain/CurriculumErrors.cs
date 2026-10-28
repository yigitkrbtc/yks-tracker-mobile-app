using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Curriculum.Domain;

/// <summary>Stable error codes; clients switch on these, so never rename one.</summary>
public static class CurriculumErrors
{
    public static readonly Error NotSeeded = Error.NotFound("curriculum.not_seeded", "Müfredat henüz yüklenmedi.");
}
