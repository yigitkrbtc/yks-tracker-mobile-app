# ADR-0021: Güvenlik denetimi sertleştirmesi

- Durum: Kabul edildi (PR 1: kimlik)
- Tarih: 2026-11-12

## Bağlam

2026-11-12 mimari + güvenlik denetimi 19 güvenlik, 9 mantık bulgusu çıkardı ([rapor](https://claude.ai/artifact/WruuoUKGBMwuvfzATyfBPq)). Koddan çözülebilenler dört PR'da sırayla kapanır: kimlik, olay güvenilirliği, API sınırları, mobil + CI. Bu ADR her PR'da büyür; bulgu numaraları rapordaki gibidir.

## Karar

**PR 1: Kimlik**

- **G1 E-posta doğrulaması şifreye bağlı.** `POST /v1/auth/email/verify` gövdesi `password` ister; önce şifre (`CredentialChecker`, kilit sayacı dahil), sonra kod kontrol edilir. Doğrulanmamış kayıt tekrarı ADR-0012'deki gibi bilgileri değiştirir, ama kurbanın elindeki kod artık saldırganın şifresiyle hesabı etkinleştiremez: kurbanın şifresi tutmaz, `identity.invalid_credentials` döner, kurban yeniden kayıt olup hesabı geri alır. `registrationId` yerine şifre seçildi: yeni kolon ve istemci deposu gerekmez, uygulama yeniden başlasa da kullanıcı şifresini bilir. Mobil şifreyi rota `extra`'sıyla taşır (URL'de değil), yoksa doğrulama ekranı sorar.
- **G4 Deneme bütçesi.** Kod başına 5 deneme yeni kodlarla sınırsız tahmine dönüşmesin diye başarısız denemeler kullanıcı + amaç başına 24 saatte en çok 25 (`VerificationCode.MaxFailedAttemptsPerWindow`), kodlar arasında taşınır; yeni kod istemek bütçeyi sıfırlamaz, dolunca `Verify` `CodeExpired` döner, pencere bitince bütçe geri gelir. Önce düşünülen "günde 5 kod" tavanı kaldırıldı: başkası hesap için kod isteyerek sahibinin kod almasını engelleyebiliyordu. Kod üretimi yalnız 60 sn bekleme süresine bağlı. Bedel: bütçeyi tüketen saldırgan sahibin doğrulama/sıfırlamasını pencere bitene kadar durdurabilir; tahmin yüzeyini kapatmak için kabul edildi.
- **G5 (kısmen)** Kilitli hesap da bilinmeyen adres gibi bir hash öder. E-postanın istek dışına alınması PR 2'de.
- **G8 Rol yapılandırmadan.** `Identity:AdminEmails` her şifreli girişte okunur (`SignInService.Complete`): listeye eklenen hesap sonraki girişte TOTP kurulumuna gider, çıkarılan öğrenciye döner. Yükseltme anında hesabın var olan tüm refresh token'ları `RoleChanged` ile iptal edilir (hiçbiri ikinci faktörden geçmedi). Ek olarak `RefreshToken.IssuedWithMfa`: yalnız TOTP ile tamamlanan girişler (`CompleteTotpSignIn`, `ConfirmTotpSetup`) `true` yazar, rotasyon aileden devralır; refresh'te kullanıcı admin ve token `IssuedWithMfa = false` ise aile `RoleChanged` ile iptal edilir. Refresh'te rol yükseltilmez; yalnız düşürme yapılır: tüm oturumlar `RoleChanged` ile iptal edilir. Eski admin token'ları (kolon varsayılanı `false`) bir sonraki refresh'te düşer, admin yeniden giriş yapar.
- **G9 Admin oturumu 12 saat.** `Identity:AdminRefreshTokenLifetime`; ikinci faktör en geç 12 saatte bir tekrar sorulur.
- **M3 Refresh tolerans penceresi.** Döndürülmüş (`Rotated`) token 60 sn içinde (`Identity:RefreshReuseGrace`) tekrar gelirse yanıt yolda kaybolmuş sayılır: `ReplacedById` zinciri en çok 5 adım izlenir, her halkanın iptali tolerans içinde olmalı; ilk halka `Rotated`, sonrakiler `Superseded` olmalı ve zincirin sonundaki aktif halef `Superseded` nedeniyle döndürülür, yeni çift verilir. Böylece art arda iki kez kaybolan yanıt da oturumu düşürmez. Zincirde `Rotated` (halef sunulup kullanılmış), `Logout`, `ReuseDetected` gibi başka neden ya da süresi geçmiş halka görülürse ya da `Superseded` token doğrudan sunulursa yeniden kullanım: aile iptal. Bedel: tokenı 60 sn içinde çalan biri meşru istemcinin halefini geçersiz kılabilir; meşru istemci halefi sunduğunda aile ölür, saldırı yine yakalanır.
- **İ4 Yaygın şifre.** Kayıt ve sıfırlamada uçlardaki rakam/işaret atılınca (uçtaki rakam kelimenin parçası olabilir: her soyma miktarı denenir) kalan çekirdek, leet katlamasından sonra (0→o, 1→i, 3→e, 4→a, 5→s, 7→t, @→a, $→s; yalnız kelime karşılaştırması için) yaygın kelime listesinde, klavye dizisi, ≤ 3 farklı karakter ya da kişinin adı (± 2 karakter) ise ret. 10 bin yaygın şifre listesi denendi; uzunluk + harf + rakam kuralını geçen yalnız 5 kayıt çıktığı için kelime + dizi kuralı seçildi.

## Sonuçlar

- Eski mobil sürüm doğrulamada şifre göndermez ve 400 alır; beta öncesi olduğumuz için uyumluluk katmanı yazılmadı.
- `verification_codes` tablosuna `attempt_window_started_at`, `failed_attempts_in_window`; `refresh_tokens` tablosuna `issued_with_mfa` eklendi (migration `VerificationCodeAttemptWindow`).
- Kayıt tekrarı (`ReplacePendingRegistration`) kilit sayacını sıfırlar: yeniden kayıt olan, önceki denemelerin kilidini devralmaz.
- Regresyonlar `Host.IntegrationTests/IdentityHardeningTests.cs` içinde; G1 testi düzeltmeden önce kırmızıydı.
