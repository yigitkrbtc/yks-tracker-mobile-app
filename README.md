# Nirengi

YKS'ye hazırlanan öğrenci için konu testi ve TYT/AYT deneme takibi, ÖSYM konu ağırlıklarıyla açıklanabilir analiz. "Nirengi" çalışma adıdır.

| Parça | Teknoloji | Klasör |
| --- | --- | --- |
| API + arka plan işleri | .NET 10, ASP.NET Core, EF Core, PostgreSQL 17 | `backend/` |
| Mobil | Flutter, Riverpod, go_router | `mobile/` |
| Admin | React, TypeScript, Vite | `admin/` |

## Hızlı başlangıç

```bash
docker compose up -d            # API http://localhost:8080 , Mailpit http://localhost:8025
curl localhost:8080/health/ready
```

Geliştirme kuralları ve komutlar: [CLAUDE.md](CLAUDE.md). Mimari kararlar: [docs/adr](docs/adr/README.md).
