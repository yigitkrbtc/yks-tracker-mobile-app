# admin

Refine + Ant Design müfredat yönetim paneli (ADR-0019). Üretimde API imajı `/admin` altında sunar.

```bash
npm ci
npm run dev        # http://localhost:5173/admin/ — /v1 isteklerini NIRENGI_API'ye (varsayılan localhost:8080) iletir
npm run gen:api    # contracts/openapi/v1.json değişince tipleri yeniden üret
npm run lint && npm test && npm run build
```

Giriş için API'de `Identity:AdminEmails` listesindeki bir hesap ve TOTP gerekir (`docs/yerel-calistirma.md`).
