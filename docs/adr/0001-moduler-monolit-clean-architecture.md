# ADR-0001: Modüler monolit ve Clean Architecture

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Tek geliştirici + Claude, tek dağıtım. Mikroservis operasyon yükü taşınamaz ama modüller ileride ayrılabilmeli.

## Karar

- Tek ASP.NET Core süreci (Host) yedi modülü çalıştırır: Identity, Curriculum, Tracking, Analytics, Recommendation, Engagement, Platform.
- Her modül `Domain` (Analytics'te `Engine`), `Application`, `Infrastructure`, `Api`, `Contracts` projelerinden oluşur. Bağımlılık içe akar.
- Modüller birbirine yalnız `Contracts` üzerinden erişir; başka modülün tablosuna JOIN yok.
- Her modül kendi PostgreSQL şemasının tek sahibidir; migration geçmişi de o şemadadır.

## Sonuçlar

Kurallar `backend/tests/ArchitectureTests` ile CI'da zorlanır. Bir modülü servise ayırmak, şemasını ve Contracts'ını taşımak demektir.
