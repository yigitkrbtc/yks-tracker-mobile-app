# ADR-0015: Deneme kaydı, bölüm şablonu ve yanlış etiketleme

- Durum: Kabul edildi
- Tarih: 2026-10-31

## Bağlam

S4'te öğrenci TYT, AYT ve branş denemesini ders bazında D/Y ile girer ve isterse yanlış/boşlarını konulara dağıtır. ADR-0010 deneme aggregate'ini ve analiz kurallarını belirledi; ADR-0014 offline senkron yolunu. Bu ADR ayrıntıları ve gereksinim dokümanından sapmaları sabitler.

## Karar

**Bölüm şablonu (Curriculum)**
- `data/seed/exam_structure.json` → `mockSections`, `curriculum.mock_sections` tablosu; seed ile yüklenir, `GET /v1/curriculum` içinde `mockSections`, `ICurriculumCatalog.GetMockSections()`.
- Her testin tek kök bölümü vardır (soru sayısı testinkine eşit). Blok testlerde (TYT Sosyal/Fen, AYT TDE-SB1/SB2/Fen) ders başına alt bölüm, toplamları testi tutar; matematik geometri dahil tek bölüm.
- Öğrenci bir bloğu ya toplam olarak ya dersleriyle girer, ikisi birden olmaz. Din yerine felsefe çözen için alternatif alt bölüm (`TYT-SOS.FEL2` 5, `AYT-SB2.FEL2` 6); aslıyla birlikte olmaz.
- Seed doğrulaması: kök bölüm tekliği ve soru sayısı, alt bölüm toplamı, ders–test ilişkisi, alternatifin aynı büyüklükte kardeş olması.
- Gereksinim dokümanındaki `exam_templates` / `exam_template_sections` yerine bu tek tablo kullanılır (`exam_year` sürümü S7'de müfredat sürümleriyle gelir).

**Kayıt (Tracking)**
- `MockExam` aggregate: tür (`Tyt`, `Ayt`, `Branch`; YDT yok, dil testi ve konusu seed'de yok), tarih (bugün…−365 gün, İstanbul), yayınevi (liste ya da serbest metin, tam biri), ad, süre, not.
- `mock_exam_sections`: yalnız çözülen bölümler (çözülmeyen satır yoktur; ortalamaya girmez). Soru sayısı kayda kopyalanır; TYT/AYT'de şablonla aynı olmalı, branşta 1–80 serbest. Boş ve net sunucuda hesaplanır. Branş denemesi tek bölümdür.
- `mock_exam_topic_marks` (PK sınav + bölüm + konu): yanlış, boş, isteğe bağlı neden sayıları (bilgi/dikkat/zaman ≤ yanlış). Konu bölümün dersine ait olmalı (dersi olmayan bölümde testin herhangi bir dersi). Bölüm başına etiketlenen yanlış ≤ Y, boş ≤ B.
- `tagging_status` bölüm başına (`None`/`Partial`/`Full`); `Full` = etiketli Y+B bölümün Y+B'sine eşit (hiç kaybı olmayan bölüm de `Full`). M11c yalnız `Full` bölümleri kullanır.
- `ösym_çıkmış` türü ve `net` kolonu yok (kaynak türü çıkmış soruyu belirtir; soru bazlı giriş v1.1; net okunurken D − Y/4).

**Senkron**
- ADR-0014 ile birebir: istemci UUID v7, `PUT/DELETE /v1/mock-exams/{id}`, LWW `clientModifiedAt` (sunucu saatine kırpılır, `EditTime.Of` ortak), yumuşak silme, eski yazma/silme saklı kaydı 200 ile döner.
- Etiketleme aynı aggregate'in parçasıdır, aynı `PUT` ile gider; bütün deneme tek birim olarak son yazan kazanır.
- `tracking.change_seq` iki kayıt türü için ortaktır (`ISyncRecord`); `GET /v1/sync` iki listeyi (`topicTests`, `mockExams`) tek imleçle döner: her tablodan `limit+1` okunur, sıralanıp ilk `limit` konum alınır.
- Her değişiklik `MockExamChanged` (önce/sonra bölüm kodları ve etiketli konular) yayınlar; S5 Analytics tüketir. Hesap silmede denemeler de silinir.

**Mobil**
- `features/mock_exams/{domain, application, data, presentation}`; Drift şema v2 (`mock_exams`, `mock_exam_sections`, `mock_exam_topic_marks`), v1'den yükseltme tabloları ekler, mevcut veriyi korur.
- `MockExamSyncHandler` (`mock_exam`, feed `mockExams`) `offlineRecordOverrides`'ta.
- Form: tür → yayınevi (son kullanılan seçili), ad, tarih → bölümler; AYT'de profildeki puan türlerinin testleri gösterilir, diğerleri "Diğer testleri göster" ile açılır. Boş/net anlık. Yeni kayıttan sonra "Konulara ayır" sorulur; düzenlemede "Kaydet ve yanlışları konulara ayır".
- Etiketleme ekranı: bölüm başına konu ekle, yanlış/boş sayacı (kalan kadar artar), durum rozeti. Neden sayıları API'de var, ekranda henüz yok (konu testinde olduğu gibi sonra eklenebilir).
- `SyncStatus` konu testi domain'inden yeniden kullanılır (domain → domain importu serbest); `formatDay`/`formatNet` `core/design/format.dart`'a taşındı.

## Sonuçlar

- Deneme konu ustalığını (M1) değiştirmez; M11a bölüm satırlarından, M11c `Full` bölümlerin etiketlerinden okur.
- Bilinen sınır: iki cihazda aynı denemenin eşzamanlı düzenlenmesi + etiketlenmesi son yazanın tüm denemeyi almasıyla sonuçlanır (ADR-0014 ile aynı LWW).
- Şablondan kaldırılan bir bölüm kodu, onu içeren eski denemenin yeniden kaydını reddettirir; kayıtlar soru sayısını kopyaladığı için okunmaya devam eder.
