# ADR-0009: Konteyner, bulut bağımsız dağıtım

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Barındırma yeri (Türkiye içi olup olmayacağı) KVKK değerlendirmesine bağlı ve yayın öncesi seçilecek.

## Karar

- API + worker tek Docker imajı (`backend/src/Host/Dockerfile`), yerelde `docker compose` (PostgreSQL 17, Mailpit).
- PaaS'a özgü servis kullanılmaz; yapılandırma ortam değişkeniyle.

## Sonuçlar

Barındırma kararı imajı değiştirmez.
