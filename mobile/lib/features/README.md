# features/

Her özellik kendi klasöründe, aynı katmanlarla (bkz. kökteki `CLAUDE.md`):

```
features/<özellik>/
  domain/        saf Dart: entity, value object, repository arayüzü
  application/   Riverpod notifier/provider'lar, use case
  data/          Drift tabloları, OpenAPI istemcisi, repository uygulaması, offline outbox
  presentation/  widget ve sayfalar
```

Bağımlılık yalnız içe akar: presentation → application → domain; data, domain'deki arayüzleri uygular. Bir özellik başka bir özelliğin `data` veya `presentation` klasörünü import etmez.
