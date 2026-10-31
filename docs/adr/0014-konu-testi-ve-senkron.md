# ADR-0014: Konu testi kaydı ve offline senkron

- Durum: Kabul edildi
- Tarih: 2026-10-31

## Bağlam

S3'te öğrenci çözdüğü konu testini internet olmadan da kaydeder; kayıt bağlantı gelince sunucuya gider, diğer cihazlara iner. ADR-0005 yönü belirliyor (Drift + outbox, istemci UUID v7, idempotent `PUT`, `GET /v1/sync?since=`, son yazan kazanır); bu ADR ayrıntıları sabitler.

## Karar

**Alan (Tracking)**
- `TopicTest` aggregate: tarih (gün, Europe/Istanbul; bugün ile son 365 gün arası), kaynak, tekrar, zorluk, süre, not ve 1–20 konu satırı. Satır: soru (1–200), doğru, yanlış, boş (D+Y+B = soru), isteğe bağlı yanlış nedenleri (bilgi/dikkat/zaman, toplamı ≤ yanlış). Net = D − Y/4.
- Çok konulu fasikül tek testtir, konu başına bir satır. Konu satırları `topic_test_results`'ta (PK `topic_test_id, topic_id`); analiz bu tablodan okur.
- Kaynak: seed'li liste (`data/seed/kaynaklar.csv` → `curriculum.sources`, UUID v5(`0b7e5d2c-4a91-4f3e-9d68-1c2b3a4f5e6d`, `source_code`), `GET /v1/curriculum` içinde) ya da serbest metin; ikisinden tam biri (DB check). Konu ve kaynak varlığı `Curriculum.Contracts.ICurriculumCatalog` ile denetlenir; yalnız yeni eklenen konu/kaynak aktif olmalı, eski kayıt emekli konuyla düzenlenebilir.
- Sayılar `integer` kolon; `smallint` EF/Npgsql eşlemesinde tip uyuşmazlığı çıkardığı için kullanılmadı.

**Sürüm ve çakışma**
- Son yazan kazanır, istemcinin `clientModifiedAt`'ine göre. Sunucu bu zamanı kendi saatine kırpar (ileri tarihli cihaz saati sonsuza kadar kazanamaz) ve mikro saniyeye keser (PostgreSQL hassasiyeti). Eski bir yazma hiçbir şeyi değiştirmez ve saklı durumu döner; istemci onu uygular.
- Silme yumuşaktır (`deleted_at`, sonuçlar silinir, tombstone kalır); daha yeni bir `PUT` testi geri getirir. `DELETE` bilinmeyen kimlik için de 204 döner (idempotent); başka kullanıcının kimliği 404. Saklı düzenlemeden eski bir `DELETE` kaybeder ve eski `PUT` gibi saklı testi 200 ile döndürür; istemci kopyasını atmak yerine onu geri yazar.

**Değişim akışı**
- Her kayıt değişikliği `tracking.change_seq` dizisinden `change_seq` alır. `TrackingUnitOfWork` kayıt öncesi kullanıcı başına `pg_advisory_xact_lock` alır, böylece aynı öğrencinin iki transaction'ı sırayı ters commit edip imleci atlatamaz.
- `GET /v1/sync?since=&limit=` (≤ 500) `{ topicTests, cursor, hasMore }` döner; tombstone'lar dahil. Yeni kayıt türleri aynı yanıta kendi listesiyle eklenir.
- Her değişiklik aynı transaction'da `TopicTestChanged` (önce/sonra konu kimliklerinin birleşimi) yayınlar; S5 Analytics tüketir.

**Mobil**
- Şema `core/database` (`AppDatabase`, Drift); özellikler yalnız `data` katmanından erişir. Üretilen `.g.dart` commit'lenir, CI güncelliğini denetler.
- `core/sync`: `OutboxStore` (kayıt başına tek bekleyen yazma; yeni yazma eskisini `revision+1` ile değiştirir, havadaki push onu silemez), `SyncService` (önce push sonra pull, tek uçuş, 2 sn'den 5 dk'ya üstel bekleme), `SyncScheduler` (yazmadan 500 ms sonra, öne gelince, 5 dk'da bir, beklemesi dolunca). Core hiçbir özelliği bilmez; özellik `SyncHandler` uygular, `app/composition.dart` `offlineRecordOverrides` ile bağlar.
- 400/404/422 kalıcı ret sayılır: yazma park edilir, kayıt "Sunucu kabul etmedi" rozeti ve sunucunun mesajıyla görünür; öğrenci düzeltince yeniden kuyruğa girer. Reddedilen silme her iki kopyayla birlikte atılır (gösterilecek kayıt yok). Yanıt yok, 401/403/408/429/502/503/504 çalışmayı durdurur ve geri çekilir. Sunucunun tek bir yazmada verdiği diğer hatalar (500, 409…) o yazmayı atlar; kalanlar ve pull devam eder, aynı yazma 5 denemede park edilir. Beklenmeyen istemci hatası (okunamayan akış kaydı) da geri çekilmeyle sonuçlanır. Senkron sürerken gelen istek bir çalışma daha sıraya koyar. Çıkışta süren çalışma terk edilir; veritabanına ve sunucuya bir şey yazmaz.
- Pull, kuyrukta yazması olan kayıtları ezmez; yerel yazma sunucu cevap verene kadar kazanır.
- Hiç senkronlanmamış testin silinmesi de `DELETE` gönderir (plan metninde iki kuyruğu birlikte atmak vardı). Sebep: havadaki bir `PUT` sunucuya ulaşmış olabilir; kayıt başına tek kuyruk girdisi kuralı bozulmaz, sunucu bilinmeyen kimliğe 204 der.
- Müfredat (kaynak listesi dahil) ve çalışma profili `cache_entries`'te saklanır; uygulama çevrimdışı açılır, müfredat sürümle yeniden doğrulanır.
- Çıkışta eşitlenmemiş kayıt varsa uyarı gösterilir; çıkış ve başka hesapla giriş yerel veritabanını siler (cihazda tek öğrencinin verisi).

## Sonuçlar

- Kayıt ekranı ağı beklemez; senkron durumu listede rozetle görünür, aşağı çekmek hemen senkronlar.
- Deneme (S4) ve sonraki kayıt türleri aynı yolu izler: Tracking aggregate + `change_seq`, `PUT/DELETE` ucu, sync yanıtında yeni liste, mobilde tablo + `SyncHandler`.
- Bilinen sınır: cihaz saati geride olan bir cihazın silmesi, başka cihazdaki daha yeni düzenlemeye yenilir (LWW, ADR-0005); sunucu saklı testi döndürdüğü için silen cihaz testi geri görür.
- Schema sürümü 1; yayından sonra Drift şema değişiklikleri migration ister.
