# ADR-0003: Analytics Engine saf kütüphane

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Metrik spesifikasyonu (M0–M11) deterministik olmalı ve golden dataset ile doğrulanmalı.

## Karar

- `Analytics.Engine` hiçbir Nirengi, Microsoft, I/O veya ağ bağımlılığı taşımaz. Girdi: kayıt listesi + kural seti + referans tarih. Çıktı: metrikler.
- Saat parametre olarak verilir; rastgelelik yok.
- `Analytics.Application` veriyi toplar, motoru çağırır, snapshot yazar.

## Sonuçlar

Motor değişikliği golden dataset testinden geçmeden birleşmez. Mimari test motorun saflığını doğrular.
