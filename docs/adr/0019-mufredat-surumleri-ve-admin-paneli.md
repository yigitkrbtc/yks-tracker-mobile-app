# ADR-0019: Puan türüne göre q, müfredat sürümleri ve admin paneli

- Durum: Kabul edildi
- Tarih: 2026-11-02
- ADR-0013'ün "müfredat yalnız seed'den" kısmının yerine geçer; seed hâlâ ilk kaynak ve yapı/ağırlık kaynağıdır.

## Bağlam

S7 içerik ekibine müfredatı koddan bağımsız yönetme imkânı verir. Üç sorun vardı: (1) AYT tarih/coğrafya gibi iki teste giren konuların ağırlığı tek sayıydı, EA ve SÖZ öğrencisi için aynı q kullanılıyordu; (2) müfredat değişikliği yalnız seed + deploy ile oluyordu, geçmişi ve geri dönüşü yoktu; (3) admin paneli boştu. Yayın hazırlığı işleri (denetim kaydı, KVKK yeniden onay, barındırma, SMTP, Play iç test) ürünün yönü netleşince ayrı sprinte bırakıldı.

## Karar

**q test bazında (K1, K2)**
- `konu_agirliklari.csv` ve `curriculum.topic_weights` satırı (konu, sınav yılı, test). Çok testli derslerde her test ayrı normalize edilir (TAR ≈ 10 + 11, COĞ ≈ 6 + 11).
- Öğrencinin q_i'si profilindeki puan türlerinin testleri üzerinden toplam (`CatalogWeightedTopic.ExpectedFor`). Motor değişmedi; Analytics ve Recommendation q'yu motora vermeden önce hesaplar. Öneri kural seti `R2026.2`.
- Öğrenci `TopicDto.tier` = testlerdeki en yüksek kademe (eski alan), yanında `weights[]`. Mobil `Topic.forTests` ile aynı toplamı yapar.

**Sürümler (K3, K4, K5)**
- Canlı tablolar (`topics`, `sources`, `exam_calendars`) okumada değişmeden kalır; her yayın `curriculum_versions` satırı (sıra no, özet, yayınlayan, işlemler, tam snapshot) ekler. `releases.version_number` canlı sürümü, `content_hash` = SHA256(seed hash + snapshot) ETag'i ve modüller arası katalog sürümünü verir; analiz damgası bununla yenilenir.
- Değişiklik bir **taslaktır**: sıralı, polimorfik işlem listesi (`topic.add|update|retire|reactivate|merge|split`, `source.add|update|retire|reactivate`, `calendar.set`). Sınav yılı başına bir elle taslak + bir seed içe aktarma taslağı. Kaydetmek revizyonu artırır (çakışmada 409), yeni konulara kalıcı kodu (`<ders>.NN`, hiç kullanılmamış en küçük sonraki) atar ve taslağı güncel sürüme taşır. Her okumada kuru çalıştırma sonucu döner.
- Yayın tek transaction'da, advisory kilitle: taslak saf `DraftApplier` ile canlıya uygulanır (seed ayrıştırıcısının kuralları: kod biçimi ve tekrar kullanılmaz, sınıf 9–12, alt konu 1–30, bir ders aktif konusuz kalamaz), canlı tablolar ve sürüm yazılır. Konu ve kaynak silinmez, emekliye ayrılır.
- Geri dönüş = eski snapshot ile canlı arasındaki fark taslağı (`ContentDiff`); geçmiş yeniden yazılmaz.
- Seed: boş veritabanı doğrudan sürüm 1. Sonra seed değişince fark bir seed içe aktarma taslağı olur; `Curriculum:AutoPublishSeed` (geliştirme ve testte açık, üretimde kapalı) açıksa açılışta yayınlanır. Sınav yapısı, puan türleri, deneme bölümleri ve ağırlıklar yalnız seed'den gelir; panelde salt okunur. Panelde eklenen konunun ağırlığı seed onu öğrenene kadar yoktur (q = 0).
- Sınav takvimi seed'den DB'ye taşındı (`exam_calendars`, `calendar.set`); `/v1/curriculum/exam-calendar` ETag'i içerik hash'i.
- `/v1/admin/curriculum/export` canlı müfredatı `data/seed` dosyaları (konu, kaynak, takvim) olarak ZIP verir; kalıcı hale getirmek için repoya işlenir.

**Soy bağı (K6)**
- `topic_lineage` (eski → yeni, `merged` / `split`). Birleştirmede eski konunun test satırları ve deneme işaretleri analizde hedef konuya sayılır (Analytics Application, zincirler izlenir, motor girdisinden önce). Bölmede kayıt taşınmaz; eski konu emekli olarak geçmişini korur.

**Kural setleri (K7)**
- Parametreler kodda kalır; değişiklik golden dataset + sürüm artışıyla. `IRuleSetDescriptor` (BuildingBlocks) her modülün setini tanımlar, `/v1/admin/rule-sets` salt okunur listeler.

**Panel (K8, K9, K10)**
- `admin/`: Refine 5 + Ant Design 5; tipler `openapi-typescript` ile sözleşmeden (`npm run gen:api`), istemci `openapi-fetch`. Ekranlar: giriş + TOTP (ilk girişte kurulum, kurtarma kodları bir kez), müfredat (ders filtresi, test başına beklenen soru, konu/kaynak/takvim düzenleme, birleştir/böl), taslak paneli (sıra, kaldır, kuru çalıştırma, yayın özeti), sürüm geçmişi ve geri dönüş, kural setleri, seed indir.
- API imajı paneli `/admin` altında aynı kökenden sunar (Dockerfile Node aşaması → `wwwroot/admin`), CORS yok. Erişim jetonu bellekte, yenileme jetonu `sessionStorage`'da (sekme kapanınca biter). 401'de tek seferlik yenileme.
- Yetki: `admin` rolü (Identity `AdminEmails` + zorunlu TOTP, ADR-0012), kullanıcı başına dakikada 120 istek. Panel öğrenci verisi göstermez.

**Güvenlik başlıkları**
- Her yanıt: `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, COOP/CORP same-origin, `Permissions-Policy`; API'de `default-src 'none'` CSP; geliştirme dışı HSTS.
- `/admin`: `script-src 'self'`, `style-src 'self' 'nonce-…'` (index.html'e istek başına nonce yazılır, Ant Design CSS-in-JS bunu kullanır), `style-src-attr 'unsafe-inline'` (React `style=` öznitelikleri), `frame-ancestors 'none'`, `connect-src 'self'`.
- Data Protection anahtarları zaten Identity DB'sinde (`PersistKeysToDbContext`); ek iş gerekmedi.

## Plandan sapmalar

- Analytics `RuleSet` sürümü artmadı: motor ve kurallar değişmedi; birleştirme eşlemesi motor girdisinde. Sonuçlar yine de yenilenir çünkü katalog sürümü (içerik hash'i) analiz damgasında.
- Ayrı "doğrula" ucu yok; her taslak okuması kuru çalıştırma sonucunu taşır. Revizyon gövdede (ETag/If-Match yerine).
- Taslaklar (yıl, tür) başına tek; çok kişili eşzamanlı düzenleme revizyon çakışmasıyla korunur.
- Panelde sistem/durum ekranı yok (yayın hazırlığı sprintine).
- Puan türü q'sunda deneme maruziyeti öğrencinin toplam q'sunu kullanır, bölüm (test) başına q'yu değil (motor değişmesin diye); etkisi yalnız iki testli derslerin deneme payı.
- SAY öğrencisinde yalnız AYT-SB2'ye giren dersler q = 0 olduğu için genel bakışta o test hiç görünmez (önce "pasif" görünüyordu); mobil zaten yalnız aktif testleri gösterir.
- `rc-util` kaydırma ölçümü ve modal kaydırma kilidi nonce'suz `<style>` eklemeye çalışır; CSP bunları engeller (konsolda uyarı, modal açıkken arka sayfa kayabilir). Güvenlik için `unsafe-inline` açılmadı.

## Sonuçlar

- Üretimde seed değişikliği admin onayı ister; deploy tek başına müfredatı değiştirmez. Seed ile canlı ayrışırsa panel uyarır; kalıcılık için dışa aktarılan dosyalar repoya işlenir.
- Konu kodları kalıcı ve tekrar kullanılmaz; UUID v5 kimliği değişmediği için öğrenci kayıtları ve senkron etkilenmez.
- Yeni işlem türü eklemek: domain kaydı + `DraftApplier` + `ContentDiff` + panel `describe()`; sözleşme yeniden üretilir.
