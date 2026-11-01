# Mimari karar kayıtları

| No | Karar | Özet | Durum |
| --- | --- | --- | --- |
| 0001 | [Modüler monolit ve Clean Architecture](0001-moduler-monolit-clean-architecture.md) | Tek Host süreci yedi modül; modül = Domain/Application/Infrastructure/Api/Contracts, erişim yalnız Contracts, şema başına tek sahip. | Kabul |
| 0002 | [Modül başına transactional outbox](0002-modul-basina-outbox.md) | Olay aynı `SaveChanges` ile modül şemasındaki `outbox_messages`'a yazılır; en az bir kez teslim, handler idempotent. | Kabul |
| 0003 | [Analytics Engine saf kütüphane](0003-analytics-engine-saf-kutuphane.md) | Motor I/O ve framework bağımlılığı taşımaz, saati parametre alır; golden dataset ile doğrulanır. | Kabul |
| 0004 | [Veri öğrenciye aittir](0004-veri-ogrenciye-ait.md) | Tüm veri `user_id` global filtresiyle; kurum/koç erişimi v2'de `access_grants` ile. | Kabul |
| 0005 | [Offline-first ve idempotent yazma](0005-offline-first-idempotent-yazma.md) | Mobil önce Drift + outbox; istemci UUID v7, `PUT /v1/<kaynak>/{id}`, yumuşak silme, `GET /v1/sync?since=`. | Kabul |
| 0006 | [AI port ve adaptör](0006-ai-port-adaptor.md) | Recommendation `IAiClient` portu; şema doğrulamalı JSON, geçmezse kural tabanlı metin; MVP yalnız kural tabanlı. | Kabul |
| 0007 | [MediatR yerine ince dispatcher](0007-kendi-dispatcher.md) | `IDispatcher.Send/Query` tipli sarmalayıcıyla; önce FluentValidation, hata `Result` ile, başka pipeline yok. | Kabul |
| 0008 | [OpenAPI sözleşmesi repoda, CI'da kontrol](0008-openapi-sozlesmesi.md) | `contracts/openapi/v1.json` her build'de koddan üretilir; değişirse CI kırılır. | Kabul |
| 0009 | [Konteyner, bulut bağımsız dağıtım](0009-konteyner-bulut-bagimsiz.md) | API + worker tek Docker imajı, yerelde compose; PaaS'a özgü servis yok, yapılandırma ortam değişkeniyle. | Kabul |
| 0010 | [Deneme kaydı ayrı aggregate](0010-deneme-kaydi.md) | `MockExam` konu testinden ayrı; ders başına D/Y, net sunucuda; etiketleme isteğe bağlı, puan tahmini yok. | Kabul |
| 0011 | [Mobil tasarım sistemi shadcn_ui üzerine](0011-mobil-tasarim-sistemi.md) | Token/tema/`N*` bileşenleri `core/design`'da; özellikler shadcn'i doğrudan import etmez (mimari test). | Kabul |
| 0012 | [Kimlik doğrulama: JWT + dönen refresh token](0012-kimlik-dogrulama.md) | Kendi `User` aggregate'i; 15 dk JWT, dönen opak refresh (30 gün aile, tekrar kullanımda iptal), admin TOTP, hesap varlığı sızdırılmaz. | Kabul |
| 0013 | [Müfredat seed'den yüklenir; çalışma profili Tracking'de](0013-mufredat-ve-calisma-profili.md) | `topic_code` + UUID v5 kimlik, `data/seed` açılışta hash'li upsert, ağırlık sınav yılına göre; profil `tracking.study_profiles`. | Kabul |
| 0014 | [Konu testi kaydı ve offline senkron](0014-konu-testi-ve-senkron.md) | `TopicTest` 1–20 konu satırı; `clientModifiedAt` ile LWW (sunucu saatine kırpılır), tombstone, kullanıcı başına `change_seq` akışı. | Kabul |
| 0015 | [Deneme kaydı, bölüm şablonu ve yanlış etiketleme](0015-deneme-kaydi-ve-etiketleme.md) | Curriculum `mock_sections` şablonu; çözülen bölümler + konu işaretleri, bölüm başına `tagging_status`; senkron ADR-0014 ile aynı. | Kabul |
| 0016 | [Analiz motoru, okuma modeli ve tazeleme](0016-analiz-motoru-ve-okuma-modeli.md) | C# saf motor (`RuleSet.V1`, scipy golden dataset); snapshot tabloları, damga + gün + kural sürümüyle olayda ve okumada tazeleme; ETag'li panel, hesap yalnız sunucuda. | Kabul |

Yeni karar: sıradaki numarayla aynı şablonda (Bağlam, Karar, Sonuçlar) dosya ekle ve bu tabloya tek satırlık özetiyle ekle. Değişen karar silinmez; yeni ADR eskisini "Yerine geçti" olarak işaretler.
