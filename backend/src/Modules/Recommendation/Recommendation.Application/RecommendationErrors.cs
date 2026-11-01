using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Recommendation.Application;

/// <summary>Stable error codes; clients switch on these, so never rename one.</summary>
public static class RecommendationErrors
{
    public static readonly Error CurriculumNotLoaded = new("recommendation.curriculum_not_loaded", "Müfredat henüz yüklenmedi.");
    public static readonly Error ItemNotFound = Error.NotFound("recommendation.item_not_found", "Bu öneri bu haftanın planında yok.");
}
