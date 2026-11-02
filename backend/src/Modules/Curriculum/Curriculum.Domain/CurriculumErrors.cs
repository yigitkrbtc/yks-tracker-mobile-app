using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Curriculum.Domain;

/// <summary>Stable error codes; clients switch on these, so never rename one.</summary>
public static class CurriculumErrors
{
    public static readonly Error NotSeeded = Error.NotFound("curriculum.not_seeded", "Müfredat henüz yüklenmedi.");

    public static readonly Error DraftNotFound = Error.NotFound("curriculum.draft_not_found", "Taslak bulunamadı.");

    public static readonly Error VersionNotFound = Error.NotFound("curriculum.version_not_found", "Sürüm bulunamadı.");

    public static readonly Error DraftChanged = Error.Conflict(
        "curriculum.draft_changed", "Taslak sen düzenlerken değişti; yeniden yükleyip tekrar dene.");

    public static readonly Error DraftStale = Error.Conflict(
        "curriculum.draft_stale", "Taslak eski bir sürüme göre hazırlandı; güncel sürüme taşıyıp tekrar dene.");

    public static readonly Error SeedChanged = Error.Conflict(
        "curriculum.seed_changed", "Seed bu taslaktan sonra değişti; içe aktarma taslağı yeniden oluşturulacak.");

    public static readonly Error SummaryRequired = Error.Validation("curriculum.summary_required", "Yayın için kısa bir özet yaz.");

    public static Error InvalidDraft(string message) => Error.Validation("curriculum.invalid_draft", message);
}
