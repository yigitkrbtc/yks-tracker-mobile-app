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

## 4. Bir kere: adresi GitHub'a gir

GitHub → repo **Settings** → **Secrets and variables** → **Actions** → **Variables** sekmesi → **New repository variable**. Name: `API_BASE_URL`, Value: 3. adımdaki adres (ör. `http://192.168.1.20:8080`) → **Add variable**.

## 5. Telefona kur ve güncelle

`mobile/` dosyalarına dokunan her PR'da APK kendiliğinden üretilir (5–8 dk). PR sayfasında **📱 Mobil önizleme** yorumu çıkar.

1. Telefondan yorumdaki `nirengi-pr-<numara>.apk` linkine dokun (ya da QR'ı okut). Repo gizliyse telefonda GitHub'a giriş yapılmış olmalı.
2. İnen dosyayı aç. "Bilinmeyen kaynaklar" izni isterse ver. Uygulama zaten kuruluysa **Güncelle** de; veriler korunur.
3. İlk kurulumda uygulamayı aç, **Kayıt ol**. Doğrulama kodu e-postayla değil, bilgisayarında http://localhost:8025 (Mailpit) sayfasına düşer.

Tüm debug APK'lar repodaki ortak anahtarla (`mobile/android/app/preview-debug.keystore`) imzalanır; bu yüzden silmeden üstüne kurulur. Bu değişiklikten önce kurulmuş eski bir APK varsa **yalnız bir kere** silip yenisini kur.

PR dışında APK lazımsa: **Actions** → **Android APK (preview)** → **Run workflow** (adres kutusu boşsa değişken kullanılır); dosya çalışmanın altındaki **nirengi-test-apk** ZIP'inde.

## Sorun olursa

- **Telefon sunucuya bağlanamıyor:** Telefon ve bilgisayar aynı Wi-Fi'da mı? Telefonun tarayıcısında `http://192.168.1.20:8080/health` açılıyor mu? Açılmıyorsa Windows Güvenlik Duvarı'nda Docker Desktop'a özel ağ izni ver.
- **Bilgisayarın IP adresi değişti:** Router yeniden başlayınca değişebilir; 4. adımdaki `API_BASE_URL` değişkenini yeni adresle güncelle, sonra PR'a yeni bir commit gelince (ya da elle Run workflow) yeni APK'yı kur.
