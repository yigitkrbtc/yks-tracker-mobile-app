# ADR-0008: OpenAPI sözleşmesi repoda, CI'da kontrol

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Mobil ve admin istemcileri API sözleşmesinden üretilecek; üç kod tabanı senkron kalmalı.

## Karar

- Sözleşme `contracts/openapi/v1.json` dosyasıdır ve her build'de koddan üretilir.
- CI, build sonrası dosya değişmişse kırılır: endpoint değişikliği PR'da sözleşme farkıyla birlikte görünür ve incelenir.
- Yeni endpoint için önce PR açıklamasında istek/yanıt şekli yazılır, sonra kodlanır.

## Sonuçlar

Elle yazılan YAML yerine koddan üretim seçildi: sözleşme ile uygulama ayrışamaz. Flutter istemcisi bu dosyadan üretilir (Sprint 1).
