# Uygulamayı kendi bilgisayarında ve Android telefonunda dene (Windows)

Sunucu bilgisayarında çalışır, uygulama telefonuna APK olarak kurulur. İkisi aynı Wi-Fi'da olmalı. İnternete açık bir sunucu gerekmez.

## 1. Bir kere: Docker Desktop

1. https://www.docker.com/products/docker-desktop adresinden Windows sürümünü indirip kur, bilgisayarı yeniden başlat.
2. Docker Desktop'ı aç; sol altta "Engine running" yazana kadar bekle.

## 2. Sunucuyu başlat

1. GitHub'da repoyu aç → yeşil **Code** düğmesi → **Download ZIP**. ZIP'i bir klasöre çıkar (ör. `C:\nirengi`).
2. Klasörde adres çubuğuna `cmd` yazıp Enter'a bas (komut penceresi açılır).
3. Şunu çalıştır:
   ```
   docker compose up -d --build
   ```
   İlk seferde 5–10 dakika sürebilir. Bitince http://localhost:8080/health/live adresi açılmalı.
4. Windows "güvenlik duvarı" sorarsa **Özel ağlara izin ver** de.

Durdurmak için aynı klasörde `docker compose down` (kayıtların silinmez); tekrar açmak için `docker compose up -d`.

## 3. Bilgisayarının adresini bul

Komut penceresinde `ipconfig` yaz. "Kablosuz LAN bağdaştırıcısı Wi-Fi" altındaki **IPv4 Adresi**'ne bak (ör. `192.168.1.20`). Uygulamanın adresi: `http://192.168.1.20:8080`.

## 4. APK'yı üret

1. GitHub'da repo → **Actions** → solda **Android APK (test)** → **Run workflow**.
2. Adres kutusuna 3. adımdaki adresi yaz → **Run workflow**. 5–8 dakika sonra çalışma yeşil olur.
3. Çalışmanın sayfasının altındaki **nirengi-test-apk** dosyasını indir, ZIP'ten çıkar: `app-debug.apk`.

## 5. Telefona kur

1. APK'yı telefona gönder (e-posta, Drive ya da USB kablosu).
2. Telefonda dosyaya dokun. "Bilinmeyen kaynaklar" izni isterse ver ve kur.
3. Uygulamayı aç, **Kayıt ol**. Doğrulama kodu e-postayla değil, bilgisayarında http://localhost:8025 (Mailpit) sayfasına düşer.

## Sorun olursa

- **Telefon sunucuya bağlanamıyor:** Telefon ve bilgisayar aynı Wi-Fi'da mı? Telefonun tarayıcısında `http://192.168.1.20:8080/health` açılıyor mu? Açılmıyorsa Windows Güvenlik Duvarı'nda Docker Desktop'a özel ağ izni ver.
- **Bilgisayarın IP adresi değişti:** Router yeniden başlayınca değişebilir; yeni adresle 4. adımı tekrarla.
