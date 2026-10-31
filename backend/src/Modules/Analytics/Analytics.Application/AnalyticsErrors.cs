using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Analytics.Application;

/// <summary>Stable error codes; clients switch on these, so never rename one.</summary>
public static class AnalyticsErrors
{
    public static readonly Error TopicNotFound = Error.NotFound("analytics.topic_not_found", "Konu müfredatta yok.");
    public static readonly Error CurriculumNotLoaded = new("analytics.curriculum_not_loaded", "Müfredat henüz yüklenmedi.");
}
