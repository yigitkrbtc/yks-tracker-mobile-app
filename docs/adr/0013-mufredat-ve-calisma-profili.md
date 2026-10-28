# ADR-0013: Müfredat seed'den yüklenir; çalışma profili Tracking'de

- Durum: Kabul edildi
- Tarih: 2026-10-28

## Bağlam

S2'de öğrenci onboarding'de sınıf, puan türü ve hedef yılı seçer, kendi alanının konu listesini görür. Konu taksonomisi ve ağırlıkları `data/seed/` CSV'lerinde (ÖSYM 2019–2026 etiketlemesi). Gereksinim dokümanının veri modeli taslağı profili Identity'ye (`user_profiles`) koyuyordu.

## Karar

- **Konu kimliği:** kalıcı anahtar `topic_code` (ör. `AYT.AMAT.26`); `topics.id` = UUID v5(`6f1c2a4e-9b7d-4e3a-8c51-2d0f7a9e4b13`, `topic_code`). Yeniden seed kimliği değiştirmez, mobil aynı kimliği offline hesaplar (S3). Namespace değişmez.
- **Sınav yapısı** `data/seed/exam_structure.json`'da: testler, dersler (ders → test, AYT Tarih ve Coğrafya hem TDE-SB1 hem SB2), puan türü → test. Puan türü → test eşlemesi API'den gelir, filtreyi istemci yapar; kural kodda değil veride.
- **Ağırlık** `topic_weights(topic_id, exam_year)` ile sınav yılına göre sürümlü. Taslak/yayın akışlı `curriculum_versions` ve `topic_lineage` admin sprintine (S7) bırakıldı; tek yayınlı taksonomi var.
- **Seed yükleme:** CSV/JSON dosyaları Curriculum.Infrastructure'a embedded resource olarak derlenir (tek kaynak `data/seed`, kopya yok). `DatabaseMigrator` migration'lardan hemen sonra `CurriculumSeeder`'ı çağırır: SHA-256 aynıysa atlar, değilse advisory lock altında tek transaction'da `topic_code` ile upsert eder. Seed'den düşen konu silinmez, `Retired` olur (kayıtlar ona bağlı); geri gelirse yeniden aktifleşir. Ayrı bir `SeedOnStartup` ayarı yok: migrate edilmiş veritabanı her zaman kullanılabilir veritabanıdır.
- **`GET /v1/curriculum`** tüm ağacı döner; `ETag` = seed hash, `If-None-Match` → 304.
- **Çalışma profili Tracking'de** (`tracking.study_profiles`, PK `user_id`). Identity yalnız kimlik ve rıza tutar; profil öğrenme verisidir ve S3 kayıtlarıyla aynı sahiplik ve silme yolunu kullanır. `PUT /v1/profile` idempotent upsert, `GET` profil yoksa 404 (`tracking.profile_not_found`) ile onboarding'i işaret eder.
- Tracking `UserDeleted`'ın ilk tüketicisidir: profili `ExecuteDelete` ile siler (idempotent).
- Puan türü kodları ASCII: `SAY, EA, SOZ, DIL`. Tracking'in `ScoreType` enum'u ile Curriculum'un kodlarının eşitliğini entegrasyon testi denetler; çalışma anında modüller arası çağrı yok.
- API imajının build context'i repo köküdür (`data/seed` derlemeye girer); `.dockerignore` yalnız `backend/` ve `data/seed/`'i bırakır.

## Sonuçlar

Taksonomi veya ağırlık değişikliği yalnız `data/seed` PR'ıdır; deploy sonrası ilk açılışta yüklenir ve istemciler yeni ETag'i görür. Etiketlenmemiş konular (q_i = 0) listede "son yıllarda çıkmadı" olarak görünür. YDT taksonomisi yok; DİL öğrencisi şimdilik yalnız TYT görür.
