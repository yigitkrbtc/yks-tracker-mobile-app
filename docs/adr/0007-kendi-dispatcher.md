# ADR-0007: MediatR yerine ince dispatcher

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

MediatR ticari lisansa geçti; ihtiyaç yalnız komut/sorgu yönlendirme ve doğrulama.

## Karar

- `IDispatcher.Send/Query` handler'ı DI'dan tipli sarmalayıcıyla çözer (handler'lar `internal` olabilir).
- Handler'dan önce FluentValidation çalışır; hata `Result` olarak döner, exception ile akış kontrolü yok.
- Başka pipeline davranışı yok; loglama ve transaction handler veya middleware sorumluluğunda.

## Sonuçlar

Bağımlılık az, davranış açık. Ek davranış gerekirse dispatcher'a bilinçli olarak eklenir.
