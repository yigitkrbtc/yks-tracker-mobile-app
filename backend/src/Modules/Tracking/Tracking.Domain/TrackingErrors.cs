using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Tracking.Domain;

/// <summary>Stable error codes; clients switch on these, so never rename one.</summary>
public static class TrackingErrors
{
    public static readonly Error ProfileNotFound = Error.NotFound("tracking.profile_not_found", "Çalışma profili henüz oluşturulmadı.");
    public static readonly Error TopicTestNotFound = Error.NotFound("tracking.topic_test_not_found", "Test kaydı bulunamadı.");
    public static readonly Error TopicTestConflict = Error.Conflict("tracking.topic_test_conflict", "Test kaydı aynı anda başka bir cihazdan güncellendi; tekrar dene.");
    public static readonly Error UnknownTopic = Error.Validation("tracking.unknown_topic", "Konulardan biri müfredatta yok.");
    public static readonly Error UnknownSource = Error.Validation("tracking.unknown_source", "Seçilen kaynak listede yok.");
    public static readonly Error ProfileConflict = Error.Conflict("tracking.profile_conflict", "Profil aynı anda başka bir cihazdan güncellendi; tekrar dene.");
}
