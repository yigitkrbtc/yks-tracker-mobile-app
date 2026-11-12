# ADR-0021: Güvenlik denetimi sertleştirmesi

- Durum: Kabul edildi (PR 1: kimlik)
- Tarih: 2026-11-12

## Bağlam

2026-11-12 mimari + güvenlik denetimi 19 güvenlik, 9 mantık bulgusu çıkardı ([rapor](https://claude.ai/artifact/WruuoUKGBMwuvfzATyfBPq)). Koddan çözülebilenler dört PR'da sırayla kapanır: kimlik, olay güvenilirliği, API sınırları, mobil + CI. Bu ADR her PR'da büyür; bulgu numaraları rapordaki gibidir.

## Karar

**PR 1: Kimlik**

- **G1 E-posta doğrulaması şifreye bağlı.** `POST /v1/auth/email/verify` gövdesi `password` ister; önce şifre (`CredentialChecker`, kilit sayacı dahil), sonra kod kontrol edilir. Doğrulanmamış kayıt tekrarı ADR-0012'deki gibi bilgileri değiştirir, ama kurbanın elindeki kod artık saldırganın şifresiyle hesabı etkinleştiremez: kurbanın şifresi tutmaz, `identity.invalid_credentials` döner, kurban yeniden kayıt olup hesabı geri alır. `registrationId` yerine şifre seçildi: yeni kolon ve istemci deposu gerekmez, uygulama yeniden başlasa da kullanıcı şifresini bilir. Mobil şifreyi rota `extra`'sıyla taşır (URL'de değil), yoksa doğrulama ekranı sorar.
- **G4 Kod üretim tavanı.** Kullanıcı + amaç başına 24 saatte en çok 5 kod (`VerificationCode.MaxIssuesPerWindow`). Kod başına 5 denemeyle günde en çok 25 tahmin.
- **G5 (kısmen)** Kilitli hesap da bilinmeyen adres gibi bir hash öder. E-postanın istek dışına alınması PR 2'de.
- **G8 Rol yapılandırmadan.** `Identity:AdminEmails` her şifreli girişte okunur (`SignInService.Complete`): listeye eklenen hesap sonraki girişte TOTP kurulumuna gider, çıkarılan öğrenciye döner. Refresh'te yalnız düşürme yapılır (yükseltme TOTP ister): tüm oturumlar `RoleChanged` ile iptal edilir.
- **G9 Admin oturumu 12 saat.** `Identity:AdminRefreshTokenLifetime`; ikinci faktör en geç 12 saatte bir tekrar sorulur.
- **M3 Refresh tolerans penceresi.** Döndürülmüş token 60 sn içinde (`Identity:RefreshReuseGrace`) tekrar gelirse ve halefi hiç kullanılmadıysa yanıt yolda kaybolmuş sayılır: halef `Superseded` nedeniyle döndürülür, yeni çift verilir. Halef kullanılmışsa ya da `Superseded` token gelirse yeniden kullanım: aile iptal. Bedel: tokenı 60 sn içinde çalan biri meşru istemcinin halefini geçersiz kılabilir; meşru istemci halefi sunduğunda aile ölür, saldırı yine yakalanır.
- **İ4 Yaygın şifre.** Kayıt ve sıfırlamada uçlardaki rakam/işaret atılınca kalan çekirdek yaygın kelime listesinde, klavye dizisi, ≤ 3 farklı karakter ya da kişinin adı (± 2 karakter) ise ret. 10 bin yaygın şifre listesi denendi; uzunluk + harf + rakam kuralını geçen yalnız 5 kayıt çıktığı için kelime + dizi kuralı seçildi.

## Sonuçlar

- Eski mobil sürüm doğrulamada şifre göndermez ve 400 alır; beta öncesi olduğumuz için uyumluluk katmanı yazılmadı.
- `verification_codes` tablosuna `issue_window_started_at`, `issues_in_window` eklendi (migration `VerificationCodeIssueWindow`).
- Regresyonlar `Host.IntegrationTests/IdentityHardeningTests.cs` içinde; G1 testi düzeltmeden önce kırmızıydı.
