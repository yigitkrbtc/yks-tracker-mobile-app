using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Tracking.Domain;

/// <summary>Stable error codes; clients switch on these, so never rename one.</summary>
public static class TrackingErrors
{
    public static readonly Error ProfileNotFound = Error.NotFound("tracking.profile_not_found", "Çalışma profili henüz oluşturulmadı.");
    public static readonly Error ProfileConflict = Error.Conflict("tracking.profile_conflict", "Profil aynı anda başka bir cihazdan güncellendi; tekrar dene.");
}
