# ADR-0006: AI port ve adaptör

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

AI önerisi v1.1'de; sağlayıcı ve bütçe henüz seçilmedi.

## Karar

- Recommendation modülü `IAiClient` portunu tanımlar; sağlayıcı adaptörü Infrastructure'dadır.
- Çıktı yapılandırılmış JSON, şema doğrulamasından geçer; geçmezse kural tabanlı metne düşülür.
- MVP'de yalnız kural tabanlı öneri vardır.

## Sonuçlar

Sağlayıcı değişimi tek adaptör değişikliğidir.
