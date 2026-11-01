# ADR-0016: Analiz motoru, okuma modeli ve tazeleme

- Durum: Kabul edildi
- Tarih: 2026-10-31

## Bağlam

S5'te konu testleri ve denemelerden açıklanabilir analiz üretilir: M0–M3 (konu ustalığı, güven, bant, trend), M6 (öncelik), M7 lite (test/ders toplamı), M11a–c (deneme formu, pratik açığı, deneme sinyali). ADR-0003 motorun saf olmasını şart koşar. Hesabın nerede ve ne zaman yapılacağı, saklanma biçimi ve mobil gösterimi bu ADR ile sabitlenir.

## Karar

**Motor (`Analytics.Engine`)**
- Hesap C# saf kütüphanede; SQL prosedürü yok. Beta fonksiyonları (log-gamma, düzenli tamamlanmamış beta, quantile, P(X>Y)) kendi yazımı, dış paket yok.
- Girdi `AnalysisInput(Today, Catalog, ActiveTests, TopicRecords, MockExams)`, çıktı `AnalysisResult`; sıra bağımsız ve deterministik. Parametreler `RuleSet.V1` (`2026.1`) kodda; tablo/yönetim S7.
- M1 iki Beta-Binomial (doğruluk, işaretleme), yarı ömür 30 gün, tekrar ×0,5, ders önseli (birini dışarıda bırakarak, güç 4, ≥20 işaretli soru yoksa Beta(1,1), ortalama [0,125; 0,875]). M2 güven φ (Pearson, [1,3]) ile, eşikler 0,35/0,60/0,80; bant ≥20 soru ve ≥2 kayıtla. M3 son 21 gün / önceki 60 gün. M6 `q·max(0, 0,8−m)·öğrenilebilirlik + μ`, ilk 5, ders başına ≤2; veri az ağır konular ayrı liste (2). M11a bölüm başına 80 % aralık, ≥3 deneme.
- Golden dataset: `backend/tests/Analytics.GoldenTests` — 38 senaryo JSON; beklenen değerler bağımsız Python/scipy referansından (`reference/`), 1e-7 bağıl tolerans. CI'da koşar; motor değişikliği buradan geçmeden birleşmez.

**Okuma modeli (`Analytics.Application/Infrastructure`)**
- `analytics.user_analyses` (özet jsonb, `source_stamp`, `computed_on`, `rule_set_version`), `topic_snapshots` (konu kartı jsonb + liste kolonları), `section_snapshots`. Yazım tek transaction'da sil + ekle, kullanıcı başına advisory lock.
- Veri `Tracking.Contracts.ITrackingAnalyticsReader` (geçmiş + damga) ve `Curriculum.Contracts.ICurriculumCatalog.GetAnalysisCatalog/GetVersion` üzerinden; başka modülün tablosu okunmaz.
- Tazelik: `source_stamp = change_seq | profil güncelleme | müfredat sürümü`, İstanbul günü ve kural sürümü. Üçü aynıysa saklı sonuç döner, değilse yeniden hesaplanır. Tetik: `TopicTestChanged`/`MockExamChanged` olayları ve her okuma; gece işi yok (gün değişince ilk okuma hesaplar). `UserDeleted` satırları siler.
- Aktif testler profilin puan türlerinden; profil yoksa hepsi.

**API**
- `GET /v1/analytics/overview`: özet + konu durumları; `ETag` (içerik SHA-256), `If-None-Match` → 304, `Cache-Control: private, no-cache`.
- `GET /v1/analytics/topics/{topicId}`: konu kartı; kaydı olmayan konu gri (`ConfidenceLevel=None`), bilinmeyen konu 404 `analytics.topic_not_found`.

**Mobil**
- Hesap yalnız sunucuda; istemci yeniden hesaplamaz. `features/analytics`: HTTP deposu (ETag), panel `cache_entries`'te kullanıcı anahtarıyla (Drift şema değişikliği yok), çevrimdışı son panel gösterilir.
- Panel tam senkron sonrası (`SyncService.completed`) ve profil değişince yeniden doğrulanır; konu kartı panelin ETag'i değişince yeniden alınır. Gönderilmemiş kayıt varsa panel bunu söyler.
- Analiz sekmesi: sıradaki odak, veri az konular, test/ders ustalığı aralıkla, deneme formu. Konu kartı `/analysis/topics/:id`. Konu listesindeki durum noktası ve "Analizi gör" `app/`'ten geri çağırmayla (özellikler arası presentation importu yok).

## Plandan sapmalar

- Referans betik `tools/golden` yerine `backend/tests/Analytics.GoldenTests/reference/` (test projesinin yanında).
- M11a deneme trendine uyum kuralı eklendi: son 3 denemenin en az 2'si önceki ortalamadan aynı yönde ≥0,05 ayrılmalı (tek kötü deneme "düşüyor" demesin).
- "Sonraki güven seviyesine kalan soru" Application'da hesaplanır (gösterim yardımı, metrik değil).

## Sonuçlar

- Okuma anında tazeleme günde en fazla bir yeniden hesap + her kayıt değişikliğinde bir hesap demektir; ağır geçmişte motor ~20 ms (test sınırı 500 ms).
- Bilinen sınır: AYT tarih/coğrafya iki testte geçtiği için `q` iki testin toplamıdır; EA öğrencisinde bu konuların önceliği biraz yüksek çıkar. Puan türüne göre `q` S7'de.
- Popülasyon önseli (S7+) SQL `GROUP BY` ile toplanıp motora parametre olarak verilir; motor yine saf kalır.
- S6 öneri motoru `analytics` snapshot'larını Contracts sorgusuyla okuyacak.
