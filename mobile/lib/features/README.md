# features/

Her özellik kendi klasöründe, aynı katmanlarla (bkz. kökteki `CLAUDE.md`):

```
features/<özellik>/
  domain/        saf Dart: entity, value object, repository arayüzü
  application/   Riverpod notifier/provider'lar, use case
  data/          Drift sorguları (tablolar core/database'te), HTTP istemcisi, repository uygulaması, SyncHandler
  presentation/  widget ve sayfalar
```

Bağımlılık yalnız içe akar: presentation → application → domain; data, domain'deki arayüzleri uygular. Bir özellik başka bir özelliğin `data` veya `presentation` klasörünü import etmez.

Başka özelliğin ekranına geçiş, `app/` katmanından verilen geri çağırma ile yapılır (ör. konu kartındaki "Bu konuda test ekle" → `TopicListView(onRecordTest:)`, `home_shell.dart`'ta bağlanır). Başka özelliğin parçasını göstermek de böyle: konu listesindeki analiz durum noktası `TopicListView(statusBuilder:)` ile verilir. Senkronlanan kayıtların repository ve `SyncHandler` bağlaması `app/composition.dart`'taki `offlineRecordOverrides` listesindedir (ADR-0014).
