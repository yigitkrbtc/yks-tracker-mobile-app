using System.Globalization;

namespace Nirengi.Modules.Recommendation.Domain.Plans;

/// <summary>
/// Turkish item texts: evidence → reason → expected benefit → confidence. Task-focused and self-referenced:
/// no score, no rank, no comparison with others (requirements: "ters tepen geri bildirim").
/// </summary>
internal static class PlanTexts
{
    private const string StudyFirst = "Konuyu henüz çalışmadım diye işaretledin; önce konuyu çalış, sonra testi çöz.";

    public static PlanItem Tag(PendingTag tag) => new(
        PlanProgress.TagKey(tag.MockExamId, tag.SectionCode),
        PlanItemKind.TagMockExam,
        null,
        null,
        tag.SectionCode,
        tag.MockExamId,
        null,
        "Denemeyi etiketle",
        $"{Day(tag.TakenOn)} denemesinin {tag.SectionName} bölümünde {tag.WrongAndBlank} yanlış/boş henüz bir konuya bağlı değil. "
        + "Etiketlersen konu kartların deneme sinyalini de kullanır.");

    public static PlanItem Focus(TopicInfo topic, TopicState state, int target, DeclaredStatus declared)
    {
        var evidence = state.MasteryLow is { } low && state.MasteryHigh is { } high
            ? $"{state.Questions} soruluk kayda göre ustalığın {Pct(low)}–{Pct(high)} arasında."
            : $"Bu konuda {state.Questions} soru kaydın var.";
        var benefit = Next(state.Confidence) is { } next
            ? $"{target} soru daha çözersen değerlendirme \"{next}\" güvenine çıkar."
            : $"{target} soru daha, ustalığı hedef seviyeye yaklaştırır.";
        return Topic(
            "focus", PlanItemKind.Focus, topic, target, $"{topic.Name}: {target} soru çöz",
            declared,
            $"{evidence} Sınavda {Questions(topic.ExpectedQuestions)} çıkıyor. {benefit} Güven: {Name(state.Confidence)}.");
    }

    public static PlanItem Decline(TopicInfo topic, TopicState state, int target, DeclaredStatus declared) => Topic(
        "decline", PlanItemKind.Decline, topic, target, $"{topic.Name}: düşüşü yakala",
        declared,
        $"Son haftalardaki doğruluğun öncekinden düşük (düşüş olasılığı {Pct(1 - (state.ProbabilityUp ?? 0.5))}). "
        + $"Sınavda {Questions(topic.ExpectedQuestions)} çıkıyor. {target} soruluk bir test nedenini gösterir. Güven: {Name(state.Confidence)}.");

    public static PlanItem Collect(TopicInfo topic, int questions, int target, DeclaredStatus declared) => Topic(
        "collect", PlanItemKind.CollectData, topic, target, $"{topic.Name}: ilk ölçüm",
        declared,
        $"Sınavda {Questions(topic.ExpectedQuestions)} çıkıyor ama bu konuda {questions} soru kaydın var; değerlendirme için az. "
        + $"{target} soruluk bir test ilk ölçümü verir.");

    public static PlanItem Review(TopicInfo topic, TopicState state, int days, int target) => Topic(
        "review", PlanItemKind.Review, topic, target, $"{topic.Name}: tekrar",
        DeclaredStatus.Unknown,
        $"Ustalığın {Pct(state.Mastery!.Value)} ama {days} gündür bu konuda kayıt yok. "
        + $"{target} soruluk bir tekrar testi unutmayı erken yakalar. Güven: {Name(state.Confidence)}.");

    public static PlanItem Gap(SectionGap gap) => new(
        $"gap:{gap.Code}",
        PlanItemKind.PracticeGap,
        null,
        null,
        gap.Code,
        null,
        null,
        $"{gap.Name}: deneme pratiği",
        $"Bu bölümde konu testlerindeki doğruluğun denemelerdekinden belirgin biçimde yüksek (fark {Pct(gap.Gap)}). "
        + "Konu testlerini daha zor bir kaynaktan çöz ya da bir deneme daha gir.");

    public static PlanItem First(TopicInfo topic, int target, DeclaredStatus declared) => Topic(
        "first", PlanItemKind.FirstRecord, topic, target, $"{topic.Name}: ilk testini gir",
        declared,
        $"Sınavda {Questions(topic.ExpectedQuestions)} çıkan ağır bir konu. {target} soruluk ilk testin analizi başlatır.");

    // The host runs with invariant globalization, so Turkish formats are spelled out here.
    private static readonly string[] Months =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    private static string Day(DateOnly day) => string.Create(CultureInfo.InvariantCulture, $"{day.Day} {Months[day.Month - 1]}");

    private static PlanItem Topic(string prefix, PlanItemKind kind, TopicInfo topic, int target, string title, DeclaredStatus declared, string text) => new(
        $"{prefix}:{topic.TopicId:N}",
        kind,
        topic.TopicId,
        topic.SubjectCode,
        null,
        null,
        target,
        title,
        declared == DeclaredStatus.NotStarted ? $"{StudyFirst} {text}" : text);

    private static string Pct(double value) => string.Create(CultureInfo.InvariantCulture, $"%{Math.Round(value * 100, MidpointRounding.AwayFromZero):0}");

    private static string Questions(double q) =>
        q < 0.5 ? "1 sorudan az" : $"yaklaşık {Math.Round(q, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',')} soru";

    private static string? Next(TopicConfidence level) => level switch
    {
        TopicConfidence.None or TopicConfidence.VeryLow => Name(TopicConfidence.Preliminary),
        TopicConfidence.Preliminary => Name(TopicConfidence.Reasonable),
        TopicConfidence.Reasonable => Name(TopicConfidence.Reliable),
        _ => null,
    };

    private static string Name(TopicConfidence level) => level switch
    {
        TopicConfidence.VeryLow => "çok az veri",
        TopicConfidence.Preliminary => "ön bulgu",
        TopicConfidence.Reasonable => "makul",
        TopicConfidence.Reliable => "güvenilir",
        _ => "veri yok",
    };
}
