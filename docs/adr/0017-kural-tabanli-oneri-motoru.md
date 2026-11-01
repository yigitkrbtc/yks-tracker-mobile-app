# ADR-0017: Kural tabanlı haftalık öneri motoru

- Durum: Kabul edildi
- Tarih: 2026-11-01

## Bağlam

S6'da öğrenci her hafta 3–5 maddelik, gerekçeli bir çalışma planı görür. ADR-0006 MVP'de yalnız kural tabanlı metni öngörür (AI v1.1). Plan analiz sonuçlarına dayanır (ADR-0016), ama analiz sonucu gün içinde değişir; plan da her okumada değişirse öğrenci "neye çalışacaktım" diye kaybolur. Puan/sıralama tahmini kapsam dışıdır.

## Karar

**Yer ve bağımlılık**
- Hesap `Recommendation` modülünde. `Recommendation.Domain/Plans/PlanBuilder` saf ve deterministik: aynı girdi + saklı öğeler → aynı plan. I/O ve saat yok, bugün parametre.
- Girdi `Analytics.Contracts.IAnalysisDigestReader` (öncelikler M6, veri az konular, konu durumları, bölüm açıkları; okuma anında tazelenir), `Tracking.Contracts.ITrackingActivityReader` (gün × konu soru, etiketlenmemiş denemeler, konu işaretleri) ve `Curriculum.Contracts.ICurriculumCatalog` (konu kodu/adı, bölüm adı) üzerinden. Başka modülün tablosu okunmaz; yeni entegrasyon olayı yok.

**Kural seti `R2026.1` (`RecommendationRuleSet.V1`)**
- Aday sırası (tür önceliği): 1) son 14 günde etiketlenmemiş deneme bölümü, 2) Odak (M6 öncelik sırası), 3) Düşüş (trend düşüyor ve güven ≥ makul), 4) Veri topla (veri az ağır konular), 5) Tekrar (ustalık ≥ 0,80 ve son kayıt ≥ 21 gün önce), 6) Pratik–deneme açığı (M11b uyarısı), 7) Hiç veri yoksa ilk kayıt (en ağır 3 konu).
- Sınırlar: en çok 5 öğe, ders başına ≤ 2, etiketleme ve açık türünden en çok 1, ilk kayıttan en çok 3; bir konu planda bir kez. Eşitlikte konu kodu.
- Soru hedefi: sonraki güven seviyesine kalan soru, 10'a yukarı yuvarlanır, [20; 60]; veri topla/ilk kayıt 20, düşüş 20, tekrar 15.
- Yalnız aktif testlerin dersleri (profilin puan türleri; profil yoksa hepsi).
- Metin sunucu şablonu: kanıt → gerekçe → fayda → güven. Puan, sıralama, başkasıyla kıyas yok (test edilir). "Henüz çalışmadım" işaretli konuda metin önce konuyu çalışmayı söyler; işaret metriklere girmez.

**Kararlılık ve geri bildirim**
- Plan hafta başı (Pazartesi, İstanbul) ilk okumada kurulur, `recommendation.weekly_plans`'ta (kullanıcı + hafta, öğeler jsonb) donar. Yeniden kurulur: kural sürümü değişince, öğe reddedilince, ya da plan ≤ 2 öğeliyken analiz damgası değişince. Saklı öğeler önce kalır, boş yerler adaylarla dolar.
- İlerleme saklanmaz, her okumada haftanın kayıtlarından hesaplanır; hedefe ulaşan veya etiketlenen öğe "tamam".
- Red yalnız çevrimiçi, iki neden (`NotNow`, `AlreadyKnow`), `recommendation.item_feedback`'e yazılır (idempotent); yerine sıradaki aday gelir. Beta itiraz oranı buradan okunur.

**API**
- `GET /v1/recommendations/week`: plan + canlı ilerleme, ETag (içerik SHA-256), `If-None-Match` → 304.
- `POST /v1/recommendations/items/{itemKey}/dismiss` `{reason}`: güncel plan; bilinmeyen öğe 404 `recommendation.item_not_found`.
- `UserDeleted` iki tabloyu da siler.

**Mobil**
- `features/recommendations`: HTTP (ETag) + `cache_entries` (kullanıcı anahtarlı), çevrimdışı son plan. Tam senkron sonrası ve profil değişince yeniden doğrulanır.
- Bugün sekmesinde ilk 3 öğe, `/plan` sayfasında tümü + reddet menüsü. Öğeye dokunma konu testi formunu veya deneme etiketleme sayfasını `app/`'ten verilen geri çağırmayla açar.

## Plandan sapmalar

- Senaryolar JSON dosyası yerine okunur C# kurucusuyla (`tests/Recommendation.UnitTests/Scenario.cs`); 25 senaryo + belirlilik ve metin testleri. Beklenen planlar elle yazılır; motor saf olduğundan golden referansı gerekmez.
- Yeni öğrencide analiz zaten "veri az" iki ağır konuyu verdiği için plan önce bu ikisini "ilk ölçüm" diye, sonra ilk kayıt adaylarını gösterir (S25).

## Sonuçlar

- Kural değişikliği `RecommendationRuleSet` sürümünü artırır; o hafta planları bir sonraki okumada yeniden kurulur.
- AI (v1.1) yalnız metni değiştirecek: seçim bu motorda kalır, AI çıktısı şema doğrulamasından geçmezse şablon metin kullanılır (ADR-0006).
- Bilinen sınır: AYT tarih/coğrafya `q` değeri iki testin toplamı (ADR-0016); ders başına ≤ 2 sınırı etkisini sınırlar. FSRS tabanlı tekrar v1.1.
