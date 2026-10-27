# ADR-0005: Offline-first ve idempotent yazma

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Öğrenciler kütüphane ve sınıf gibi zayıf bağlantılı yerlerde kayıt girer.

## Karar

- Mobil kayıtlar önce Drift (SQLite) ve yerel outbox'a yazılır, senkron servisi gönderir.
- Kimlik istemcide UUID v7 olarak üretilir; yazma uçları `PUT /v1/<kaynak>/{id}` ile idempotenttir.
- Yumuşak silme (`deleted_at`) ve `GET /v1/sync?since=` ile delta çekimi.

## Sonuçlar

Sunucu aynı kimliği ikinci kez aldığında aynı sonucu döner; çakışmada son yazan kazanır (MVP).
