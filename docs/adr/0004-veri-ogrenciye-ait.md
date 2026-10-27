# ADR-0004: Veri öğrenciye aittir

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Kurum/koç erişimi v2'de gelecek; bireysel kullanıcı kurumlar arasında taşınabilmeli.

## Karar

- Tüm kullanıcı verisi `user_id` ile filtrelenir (EF global query filter).
- Kurum ve koç erişimi v2'de `access_grants` (öğrenci izni, kapsam, süre) ile eklenir; tenant başına veritabanı yok.

## Sonuçlar

Hesap silmede `UserDeleted` olayı ile her modül kendi verisini kalıcı siler.
