# ADR-0004: Veri öğrenciye aittir

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Kurum/koç erişimi v2'de gelecek; bireysel kullanıcı kurumlar arasında taşınabilmeli.

## Karar

- Kullanıcı verisi tutan modüllerde (Tracking, Analytics, Recommendation) depo imzası sahibi zorunlu kılar: `Get(userId, id)`, `GetChanges(userId, …)`, `DeleteForUser(userId)`. Handler başkasının satırını yükleyip sonradan sahiplik karşılaştırmaz; başkasının id'si "yok" okunur. Bir id'nin başkasına ait olduğunu yalnız yazma çakışmasında 404 vermek için `IsOwnedByAnotherUser(userId, id)` söyler (satırı yüklemez).
- EF global query filter **kullanılmıyor** (önceki sürümde yazılan karar uygulanmamıştı): arka plan işleri (outbox, hesap silme) kullanıcı bağlamı olmadan çalışır ve filtreyi `IgnoreQueryFilters` ile aşmak zorunda kalırdı; `ExecuteDelete` ve ham SQL zaten filtreden geçmez. Açık imza, sahiplik şartını okunur ve test edilebilir kılar.
- Bu kural testle zorlanır: `ArchitectureTests/OwnershipTests` kullanıcı verisi modüllerinin depo arayüzlerinde `userId` almayan yöntem bırakmaz; `Host.IntegrationTests/DataIsolationTests` kullanıcı B'nin jetonuyla A'nın kayıtlarına her uçtan erişimi dener (404, veri değişmez).
- Kurum ve koç erişimi v2'de `access_grants` (öğrenci izni, kapsam, süre) ile eklenir; tenant başına veritabanı yok.

## Sonuçlar

Hesap silmede `UserDeleted` olayı ile her modül kendi verisini kalıcı siler.
