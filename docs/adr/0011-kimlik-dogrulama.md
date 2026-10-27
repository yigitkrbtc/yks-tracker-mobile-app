# ADR-0011: Kimlik doğrulama: kendi Identity modülü, JWT + dönen refresh token

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Giriş yalnız e-posta + şifre; 6 haneli e-posta kodu, şifre sıfırlama, 30 gün geri alınabilir silme, admin için TOTP gerekiyor. Mobil istemci uzun oturum ister; harici kimlik sağlayıcısı (Google/Apple) v2'de gelecek.

## Karar

- ASP.NET Core Identity'nin tamamı yerine kendi `User` aggregate'imiz; şifre hash'i için yalnız `PasswordHasher<T>` (sürümlü PBKDF2) kullanılır.
- Erişim token'ı 15 dk HS256 JWT (`sub`, `role`, `jti`); anahtar `Jwt:SigningKey`. Doğrulama Host'ta, üretim Identity'de; tek süreç olduğu için simetrik anahtar yeterli.
- Refresh token 256 bit opak değer, yalnız HMAC hash'i saklanır. Her kullanımda döner; aile ömrü ilk girişten 30 gün. Dönmüş bir token tekrar gelirse aile iptal edilir (çalınma tespiti).
- E-posta kodu, refresh ve kurtarma kodu hash'leri `Identity:Security:HashKey` ile HMAC-SHA256; kod hash'i kullanıcı + amaç ile bağlanır.
- Admin şifreden sonra token yerine 5 dk geçerli MFA token alır. MFA token ayrı `aud` taşır; API onu erişim token'ı olarak kabul etmez. TOTP sırrı Data Protection ile şifrelenir (anahtar halkası `identity` şemasında).
- Hesap varlığı sızdırılmaz: kayıt, kod yenileme ve şifremi unuttum her durumda 202; giriş hatası tek tip. Durum hataları (doğrulanmamış, silinmek üzere) yalnız doğru şifreden sonra döner.
- Kalıcı silme saatlik işle yapılır; aynı transaction'da `UserDeleted` Identity outbox'ına yazılır.

## Sonuçlar

Bağımlılık az ve davranış testlerle sabit. Harici sağlayıcılar eklendiğinde aynı `SignInService` son adımı kullanılır. Ters vekil arkasında rate limit için `ForwardedHeaders` yapılandırılmalı; aksi halde tüm istemciler tek IP sayılır.
