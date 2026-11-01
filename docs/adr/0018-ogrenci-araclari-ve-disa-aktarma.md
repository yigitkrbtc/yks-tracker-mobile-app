# ADR-0018: Öğrenci araçları (takvim, konu işareti, hedef/seri, hatırlatma) ve kişisel veri dışa aktarma

- Durum: Kabul edildi
- Tarih: 2026-11-01

## Bağlam

S6 öneri motorunun yanında öğrencinin her gün açacağı araçları getirir: sınava kalan gün, konu bazında "bitirdim" işareti ve müfredat yüzdesi, hedef + hafta serisi, yerel hatırlatma. KVKK veri taşınabilirliği için öğrenci girdiği her şeyi indirebilmelidir. Hepsi mevcut kalıplara (ADR-0005/0014 senkron, modül sınırları) uymalı, analizi etkilememelidir.

## Karar

**Sınav takvimi (Curriculum)**
- `data/seed/exam_calendar.json`: yıl, kaynak (ÖSYM), not, olaylar (`tyt`, `ayt`, `ydt`, başvuru, sonuç), her olay `announced` / `estimated`. Açılışta doğrulanır (bilinen kod, yıl eşleşmesi, başvuru < TYT ≤ AYT/YDT < sonuç). ÖSYM 2027'yi açıklamadığı için tarihler tahmini; açıklanınca tek seed değişikliği.
- `GET /v1/curriculum/exam-calendar` anonim, ETag = içerik hash'i, `public, max-age=86400`. Geri sayım istemcide, İstanbul takvim günüyle, profilin hedef yılına göre.

**Konu işareti (Tracking)**
- `tracking.topic_progress` (kullanıcı + konu doğal anahtar): `NotStarted` / `InProgress` / `Done`. `PUT /v1/topic-progress/{topicId}`, `clientModifiedAt` ile LWW (ADR-0014), aynı `change_seq` akışı, `/v1/sync` yanıtında `topicProgress`. Silme yok (üç durum yeter). Bilinmeyen konu 400.
- Metriklere girmez; yalnız öneri metnini değiştirir. Mobilde Drift `topic_progress_marks` (şema v3) + outbox + `TopicProgressSyncHandler`. Müfredat %: aktif testlerde bitti / konu sayısı, altında ÖSYM ağırlıklı pay.

**Hedef ve seri**
- Hedef çalışma profilinde: mevcut `weekly_question_goal` + yeni `weekly_active_days_goal` (1–7, boşsa 5). Günlük hedef = haftalık / aktif gün, yukarı yuvarlı; profil hiç hedef girmediyse 200 / 5 = günde 40.
- İlerleme ve seri istemcide, yerel kayıtlardan (çevrimdışı anında): soru = konu testi satırları + çözülen deneme bölümleri, silinenler hariç; aktif gün = o gün ≥ 1 soru. Hafta tutar = aktif gün ≥ hedef ve soru ≥ haftalık hedef. Seri = hedefi tutan ardışık hafta; süren hafta tutunca sayılır, bitmeden seriyi bozmaz. Günlük seri ve dondurma hakkı yok.

**Hatırlatma**
- Yalnız yerel bildirim (push yok). `core/notifications/LocalReminderScheduler` portu, adaptörü `flutter_local_notifications` + `timezone`; tam zamanlı alarm izni istenmez. Varsayılan kapalı; açılınca bildirim izni istenir, reddedilirse kapalı kalır.
- Ayarlar cihazda (`cache_entries`), oturum kapanınca silinir ve zamanlanmışlar iptal edilir. Günlük hatırlatma önümüzdeki 7 gün için tek seferlik kurulur, günün hedefi tutunca o günkü düşer; Pazartesi 09:00 plan hatırlatması sonraki iki Pazartesi. Her kayıt ve açılışta yeniden kurulur.

**Kişisel veri dışa aktarma**
- `BuildingBlocks.Application.Export.IPersonalDataExporter`: veri tutan her modül kendi şemasından CSV üretir (Identity: profil, izinler; Tracking: çalışma profili, konu testleri + satırları, denemeler + bölümler + işaretler, konu işaretleri; Recommendation: öneri geri bildirimi). Platform modülleri bilmez, DI'dan toplar.
- `GET /v1/me/export`: ZIP, `BENIOKU.txt` + CSV'ler; UTF-8 BOM, `;` ayırıcı, ondalık virgül, ISO tarih (UTC), konu kodu + adı birlikte, CSV formül enjeksiyonuna karşı önek. Silinmiş kayıtlar, analiz/öneri gibi türetilmiş sonuçlar, şifre/TOTP/token yok. Kullanıcı başına saatte 3 (`RateLimiting:Export`).
- Mobil: hesap menüsünde "Verilerimi indir"; gönderilmemiş kayıt varsa önce senkron denenir, kalan varsa uyarılır; ZIP geçici klasöre iner, paylaş menüsüyle açılır.

**Mobil ana ekran**
- Beş sekme: Bugün, Konular, Testler, Denemeler, Analiz. Bugün `app/today_view.dart`'ta: geri sayım, hedef halkası + hafta noktaları + seri, planın ilk 3 öğesi, müfredat %, "Analizi gör". Hedef ayarı `/goals`, takvim `/exam-calendar`, plan `/plan`.

## Plandan sapmalar

- Engagement modülünde ayrı `study_goals` tablosu ve senkronu yerine hedef çalışma profilinde (mevcut haftalık soru hedefi yeniden kullanıldı, aynı varlık iki yerde tutulmasın). Bu yüzden `hedefler.csv` yok, hedef `calisma_profili.csv`'de; plandaki "günde 40 soru" varsayılanı haftalık hedeften türetilir.
- Hatırlatma ayarları sunucuya gitmez (yalnız bu cihaz).
- Router çalışma profilinin her kaydında değil, yalnız onboarding kararını etkileyen değişimde (yükleniyor / hata / var-yok) yenilenir; aksi halde kaydedip kapanan sayfa geri açılıyordu.
- Android: `flutter_local_notifications` için core library desugaring ve zamanlanmış bildirim alıcıları eklendi.

## Sonuçlar

- Hedef/seri sunucuda hesaplanmadığı için cihazlar arası farklı görünebilir (bir cihazda henüz inmemiş kayıt). Senkron sonrası eşitlenir.
- Takvim tahmini olduğu sürece uygulama "Tahmini" etiketi gösterir; ÖSYM açıklayınca seed `announced` yapılır.
- Yeni bir modül kişisel veri tutarsa `IPersonalDataExporter` uygulayıp kaydetmek zorunda; aksi halde dışa aktarma eksik kalır (kod incelemesinde kontrol).
