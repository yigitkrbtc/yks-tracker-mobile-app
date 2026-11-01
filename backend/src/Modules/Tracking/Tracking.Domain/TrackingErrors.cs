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
    public static readonly Error MockExamNotFound = Error.NotFound("tracking.mock_exam_not_found", "Deneme kaydı bulunamadı.");
    public static readonly Error MockExamConflict = Error.Conflict("tracking.mock_exam_conflict", "Deneme kaydı aynı anda başka bir cihazdan güncellendi; tekrar dene.");
    public static readonly Error UnknownMockSection = Error.Validation("tracking.unknown_mock_section", "Deneme bölümlerinden biri sınav yapısında yok.");
    public static readonly Error MockSectionsDoNotFitKind = Error.Validation("tracking.mock_sections_kind", "Bölümler deneme türüne uymuyor (branş denemesi tek bölümdür).");
    public static readonly Error MockSectionQuestionCount = Error.Validation("tracking.mock_section_question_count", "TYT ve AYT bölümlerinin soru sayısı ÖSYM yapısıyla aynı olmalı.");
    public static readonly Error MockSectionsOverlap = Error.Validation("tracking.mock_sections_overlap", "Bir blok ya toplam olarak ya da dersleriyle girilir; Din ve yerine çözülen felsefe birlikte olmaz.");
    public static readonly Error MockSectionCounts = Error.Validation("tracking.mock_section_counts", "Doğru + yanlış, bölümün soru sayısını aşamaz.");
    public static readonly Error MarksExceedSection = Error.Validation("tracking.marks_exceed_section", "Etiketlenen yanlış ve boşlar bölümün yanlış ve boş sayısını aşamaz.");
    public static readonly Error TopicNotInSection = Error.Validation("tracking.topic_not_in_section", "Etiketlenen konu bu bölümün derslerinden değil.");
    public static readonly Error TopicProgressConflict = Error.Conflict("tracking.topic_progress_conflict", "Konu işareti aynı anda başka bir cihazdan güncellendi; tekrar dene.");
    public static readonly Error ProfileConflict = Error.Conflict("tracking.profile_conflict", "Profil aynı anda başka bir cihazdan güncellendi; tekrar dene.");
}
