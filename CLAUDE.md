# Nirengi (çalışma adı) — kod kuralları

YKS (TYT/AYT) konu testi ve deneme takibi + açıklanabilir analiz. Gereksinim ve metrik spesifikasyonu (M0–M11) proje dokümanında; mimari kararlar `docs/adr/`.

## Yapı

| Klasör | İçerik |
| --- | --- |
| `backend/` | .NET 10 modüler monolit (`Nirengi.slnx`) |
| `mobile/` | Flutter uygulaması |
| `admin/` | React + TypeScript admin paneli |
| `contracts/openapi/v1.json` | Koddan üretilen API sözleşmesi (commit'lenir) |
| `data/seed/` | ÖSYM 2019–2026 konu taksonomisi ve ağırlıkları |
| `docs/adr/` | Mimari karar kayıtları |

## Backend kuralları

- Modül projeleri: `Domain` (Analytics'te `Engine`), `Application`, `Infrastructure`, `Api`, `Contracts`. Bağımlılık içe akar; `ArchitectureTests` kırılırsa kural değil kod düzeltilir.
- Modüller arası erişim yalnız `<Modül>.Contracts` (sorgu arayüzü, integration event DTO'su). Başka modülün DbContext'i, tablosu veya iç tipi kullanılmaz.
- Her modül kendi şemasına yazar (`<Modül>DbContext.SchemaName`). Kolon adları snake_case (otomatik).
- Komut/sorgu: `ICommand<T>`/`IQuery<T>` + `internal sealed` handler, `IDispatcher` ile çağrılır. Doğrulama FluentValidation validator'ında. Hata `Result`/`Error` ile döner; iş akışı için exception atılmaz.
- Modüller arası yan etki: handler `IEventOutbox.Enqueue(event)` çağırır, olay aynı `SaveChanges` ile yazılır. Event handler'lar idempotent olmalı.
- Endpoint'ler modülün `MapEndpoints(v1)` metodunda, `/v1` altında. Hata yanıtı `ToProblem()` ile ProblemDetails.
- Saat için `IClock`; `DateTime.Now` kullanılmaz. Analytics Engine saati parametre olarak alır, I/O yapmaz.
- Uyarılar hata sayılır (`TreatWarningsAsErrors`). Paket sürümleri yalnız `Directory.Packages.props`'ta.

## Sık komutlar

```bash
cd backend
dotnet build Nirengi.slnx                     # contracts/openapi/v1.json da yeniden üretilir
dotnet test --project tests/ArchitectureTests
dotnet test --project tests/BuildingBlocks.UnitTests
dotnet test --project tests/Host.IntegrationTests   # Docker gerekir
dotnet ef migrations add <Ad> -p src/Modules/<M>/<M>.Infrastructure -s src/Host -c <M>DbContext -o Persistence/Migrations

docker compose up -d                          # kökten: PostgreSQL 17, Mailpit, API (http://localhost:8080)

cd mobile && flutter analyze && flutter test
cd admin && npm run lint && npm run build
```

## Mobil kuralları

- `lib/features/<özellik>/{domain, application, data, presentation}`; bağımlılık içe akar, özellikler birbirinin `data`/`presentation` katmanını import etmez.
- Durum yönetimi Riverpod, yönlendirme go_router. Yazmalar önce yerel Drift + outbox'a (Sprint 3).
- UI yalnız `lib/core/design/design.dart` (token + `N*` bileşenleri) üzerinden; `shadcn_ui` doğrudan yalnız `core/design` ve `app` içinde (ADR-0011, mimari test).
- Yapılandırma `--dart-define` ile (`AppConfig`).

## Süreç

- Her iş ayrı dal + PR; `main` her zaman yeşil. PR'da testler ve mimari testler zorunlu.
- Analiz motoru değişikliği golden dataset testinden geçmeden birleşmez.
- Ürün adı "Nirengi" geçici; `Directory.Build.props` (`Product`), `AppConfig.appName`, admin başlığı tek değişim noktaları.
- Puan/sıralama tahmini ürün kapsamı dışı.
