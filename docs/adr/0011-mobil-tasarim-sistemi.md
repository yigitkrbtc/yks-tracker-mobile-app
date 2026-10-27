# ADR-0011: Mobil tasarım sistemi shadcn_ui üzerine, kendi katmanımızla

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Mobil arayüz için tutarlı bir bileşen seti ve tema gerekiyor. Tamagui değerlendirildi; React/React Native kütüphanesi olduğu için Flutter uygulamasında kullanılamaz. Flutter seçenekleri: `shadcn_ui`, `forui`, yalnız Material 3 + kendi token'larımız.

## Karar

- Temel bileşen kütüphanesi `shadcn_ui` (pub.dev).
- Tasarım sistemi `lib/core/design/` altında:
  - `tokens/`: renk, boşluk, radius, tipografi. Tek kaynak.
  - `theme/`: token'lardan hem `ShadThemeData` hem Material `ThemeData` üretilir (açık/koyu).
  - `components/`: uygulamanın kendi widget'ları (`NButton`, `NCard`, `NBadge`, ...). İçeride shadcn kullanılabilir veya sıfırdan yazılabilir.
- `features/**` `package:shadcn_ui` import etmez; yalnız `core/design/design.dart` üzerinden kullanır. Kural `test/architecture/design_imports_test.dart` ile zorlanır.

## Sonuçlar

Kütüphane değişse veya bir bileşen kendimiz yazılsa değişiklik `core/design` içinde kalır. Yeni ihtiyaçta önce mevcut `N*` bileşenine varyant eklenir, sonra yeni bileşen yazılır. Material widget'ları (`Scaffold`, `NavigationBar`) kullanılmaya devam eder; renkleri aynı token'lardan gelir.
