# ADR-0002: Modül başına transactional outbox

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Kayıt → analiz → öneri zinciri asenkron ilerler. Olay kaybı analizi sessizce eskitir.

## Karar

- Integration event'ler yayınlayan modülün `Contracts` projesinde tanımlanır.
- Handler `IEventOutbox.Enqueue` çağırır; olay aynı `SaveChanges` içinde modülün kendi şemasındaki `outbox_messages` tablosuna yazılır.
- Her modül için bir `OutboxProcessor` arka plan servisi tabloyu boşaltır ve süreçteki tüm `IIntegrationEventHandler<T>`'lara dağıtır.
- Teslimat en az bir kez: handler'lar idempotent yazılır. 10 başarısız denemeden sonra mesaj bekletilir (`attempts`, `last_error`).

## Sonuçlar

Gereksinim dokümanındaki tek `platform.outbox_messages` tablosu yerine modül başına tablo seçildi: olay ile veri aynı transaction'da kalsın diye. Ayrı kuyruk altyapısı MVP'de yok.
