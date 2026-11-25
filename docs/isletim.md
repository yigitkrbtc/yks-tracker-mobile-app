# İşletim el kitabı

Üretim ortamını ayakta tutmak için adımlar. Kararlar ve gerekçeleri [ADR-0022](adr/0022-uretim-dagitimi-ve-isletim.md)'de; burada yalnız "ne yapılır". Henüz yazılmamış dosyalar "F2 kartı N" ile işaretli; kart bitince bağlantı ve komut ayrıntısı eklenir.

> Durum: iskelet. Komutlar kartlar bittikçe doğrulanıp kesinleşir.

## 1. Genel bakış

| Parça | Nerede | Not |
| --- | --- | --- |
| VPS | Türkiye, Ubuntu 24.04 LTS | `/opt/nirengi/` altında her şey |
| Compose | `/opt/nirengi/compose.prod.yml` ← `deploy/compose.prod.yml` (F2 kartı 7) | `caddy`, `api`, `migrate` (profil `tools`), `postgres` |
| Ters vekil | Caddy, `deploy/Caddyfile` (F2 kartı 7) | 80/443 tek açık port; `api.<alan>` |
| Sır olmayan ayar | `/opt/nirengi/.env` ← `deploy/.env.example` (F2 kartı 7) | `NIRENGI_VERSION`, alan adı, Sentry DSN, `Clients__MinimumVersion`, SMTP sunucusu |
| Sırlar | `/opt/nirengi/secrets/` (0700 root, dosyalar 0400) | Liste bölüm 7'de |
| Betikler | `/opt/nirengi/bin/` ← `deploy/bin/` (F2 kartı 7, 8) | `deploy.sh`, `ci-entry`, `backup.sh`, `restore.sh`, `verify-restore.sh`, `diskcheck.sh` |
| Zamanlayıcılar | systemd ← `deploy/systemd/` (F2 kartı 8) | `nirengi-backup.timer` 03:30 UTC, `nirengi-disk.timer` 15 dk |
| Yedek kopya 1 | VPS `/var/backups/nirengi` | 7 günlük + son 3 dağıtım öncesi |
| Yedek kopya 2 | Türkiye'de ikinci sağlayıcıdaki depolama sunucusu | 05:00 UTC'de çeker; 7 günlük + 4 haftalık |
| İzleme | Sentry (AB), UptimeRobot, Healthchecks.io | Bölüm 8 |
| İmaj | `ghcr.io/<sahip>/nirengi-api:X.Y.Z` | `api-vX.Y.Z` etiketiyle üretilir (F2 kartı 5) |

Sürüm geçmişi: `/opt/nirengi/deploy-history` (`zaman sürüm imaj-özeti sonuç`).

## 2. Sunucu ilk kurulum (bir kez)

1. VPS'i aç, yönetici kullanıcıyı oluştur, SSH'yi yalnız anahtara çevir, root girişini kapat.
2. `deploy/bin/bootstrap.sh` (F2 kartı 7): Docker Engine + compose eklentisi, ufw (22, 80, 443/tcp, 443/udp), `unattended-upgrades` (yalnız güvenlik, 04:30 yeniden başlatma), saat dilimi UTC, `/opt/nirengi` dizinleri, `deploy` kullanıcısı (docker grubunda değil; sudoers yalnız `/opt/nirengi/bin/deploy.sh`).
3. `deploy` kullanıcısının `authorized_keys` satırı: `command="/opt/nirengi/bin/ci-entry",restrict <CI açık anahtarı>`.
4. GHCR girişi (root): `docker login ghcr.io` (`read:packages` jetonu; süresi takvime yazılır).
5. Sırları bölüm 7'deki listeye göre `/opt/nirengi/secrets/` altına yaz, izinleri ayarla.
6. `.env`'yi `.env.example`'dan doldur.
7. Postgres'i başlat, rolleri kur: `deploy/sql/00-roles.sql` (F2 kartı 7).
8. İlk dağıtım: bölüm 3.
9. DNS: `api.<alan>` A kaydı; posta kayıtları (SPF, DKIM, DMARC) bölüm 9.
10. Zamanlayıcıları etkinleştir, Healthchecks.io ping adreslerini `.env`'ye yaz; bir yedeği elle tetikleyip ping'in geldiğini gör.
11. Kopya 2 sunucusunu kur: salt okunur `rrsync` anahtarı, çekme zamanlayıcısı, kendi Healthchecks ping'i (F2 kartı 8).

## 3. Dağıtım

Ön koşul: `main` yeşil; sürüm etiketi `api-vX.Y.Z` itildi ve `release-api.yml` (F2 kartı 5) imajı GHCR'ye yükledi.

- **GitHub'dan:** Actions → `deploy-api` (F2 kartı 9) → Run workflow → `version = X.Y.Z`.
- **Sunucudan:** `sudo /opt/nirengi/bin/deploy.sh X.Y.Z`.

`deploy.sh` sırası (ayrıntı ADR-0022 §2): imajı çek → dağıtım öncesi yedek → `migrate` → `api`'yi yeni sürümle başlat → 120 sn içinde `/health/ready` = `Healthy` bekle → olmazsa önceki sürüme kendiliğinden dön.

Dağıtım sonrası denetim:
- [ ] `deploy-history` son satırı `ok`.
- [ ] Sentry'de yeni sürüm (`release`) görünüyor, yeni sorun yok.
- [ ] Admin paneli `https://api.<alan>/admin` açılıyor.

Migration içeren sürümden önce: [ ] migration genişlet/daralt kuralına uyuyor (ADR-0022 §3; `ArchitectureTests` zorlar, F2 kartı 6).

## 4. Geri dönüş

1. `deploy-history`'den son `ok` sürümünü bul.
2. `sudo /opt/nirengi/bin/deploy.sh <önceki sürüm>` (ya da `deploy-api` iş akışı).
3. Migration geri alınmaz; önceki sürüm yeni şemada çalışır (genişlet/daralt). `migrate` adımı eski imajla etkisizdir.
4. Sorun veri bozulmasıysa geri dönüş yetmez: bölüm 6.

## 5. Yedekleme

- Otomatik: her gün 03:30 UTC ve her dağıtımdan önce `backup.sh` (F2 kartı 8): `pg_dump -Fc` → `pg_restore --list` doğrulaması → `age` ile şifreleme → `/var/backups/nirengi`, SHA-256 dosyası, Healthchecks ping'i.
- Elle: `sudo /opt/nirengi/bin/backup.sh --tag manual`.
- Kopya 2 her gün 05:00'te çeker; çekmeyi sunucu değil kopya 2 başlatır.
- `age` özel anahtarı sunucuda **yoktur**; parola yöneticisinde ve kâğıtta.

## 6. Yedekten geri yükleme

Ne zaman: veri bozulması, DB kaybı, sunucu kaybı. Hedef süre (RTO): 2 saat. Veri kaybı (RPO): en çok 24 saat (dağıtım öncesi yedek daha yeniyse daha az).

1. Kullanılacak yedeği seç (kopya 1, yoksa kopya 2), SHA-256'yı doğrula.
2. Özel `age` anahtarını sunucuya geçici kopyala (`/root/restore.key`, 0400).
3. `sudo /opt/nirengi/bin/restore.sh <dosya>` (F2 kartı 8): API'yi durdurur, çözer, `pg_restore --clean --if-exists --no-owner --role=nirengi_owner`, `migrate`, senkron dönemini (`platform.sync_epoch`) yeniler, API'yi başlatır.
4. Özel anahtarı sil: `shred -u /root/restore.key`.
5. `/health/ready` = `Healthy`; admin girişi (Data Protection sertifikası doğruysa TOTP çalışır; sertifika kayıpsa admin TOTP'yi yeniden kurar).
6. Yedek anından sonra silinmiş hesapları yeniden sil: o aralıktaki `UserDeleted` log satırlarından kullanıcı kimliklerini al, silmeyi tekrar çalıştır (komut F2 kartı 8'de).
7. Mobil istemciler yeni dönemi görünce kayıtlarını yeniden gönderir (F2 kartı 12); bir test cihazında kaydın geri geldiğini doğrula.

Sunucu tümden kaybolduysa: bölüm 2'yi yeni VPS'te uygula, sırları parola yöneticisinden geri yaz, sonra bu bölüm; DNS'i yeni IP'ye çevir.

### Geri yükleme provası

Sıklık: ilk üç ay ayda bir, sonra üç ayda bir. Healthchecks "restore-drill" denetimi kaçırılan provayı alarm yapar.

1. Kopya 2'den en yeni yedeği kendi makinene indir (şifreli disk).
2. `deploy/bin/verify-restore.sh <dosya>` (F2 kartı 8): yerelde PostgreSQL 17 + aynı sürüm imajı ayağa kaldırır, çözer, yükler; migration geçmişi, çekirdek tablo satır sayıları, `/health/ready` denetler; başarıda "restore-drill" ping'i atar.
3. İndirilen dosyayı ve çözülmüş dökümü sil.
4. Sonucu prova günlüğüne yaz (`/opt/nirengi/drill-history`; biçim F2 kartı 8).

## 7. Sırlar ve döndürme

| Sır (dosya adı `/opt/nirengi/secrets/`) | Kim okur | Döndürme etkisi |
| --- | --- | --- |
| `app_db_connection` → `ConnectionStrings__Database` | api | Yok (yeni parola + yeniden başlatma) |
| `owner_db_connection` → `ConnectionStrings__Database` | migrate | Yok |
| `postgres_password` | postgres | Yok |
| `Jwt__SigningKey` | api | Erişim jetonları (≤ 15 dk) geçersiz; istemci refresh ile sürer |
| `Identity__Security__HashKey` | api | **Yıkıcı:** tüm refresh token'lar, kurtarma kodları, bekleyen kodlar ve kimlik postaları geçersiz; herkes yeniden giriş yapar |
| `DataProtection__Certificate` (+ `__CertificatePassword`) | api | Eski sertifika `DataProtection__PreviousCertificates__N`'e taşınır, **silinmez** |
| `Smtp__Password` | api | Yok |
| `age` açık anahtarı (`/opt/nirengi/backup.pub`) | backup.sh | Yeni yedekler yeni anahtarla; eski özel anahtar eski yedekler bitene dek (~35 gün) saklanır |

Her sırrın çevrimdışı kopyası parola yöneticisindedir (sunucu kaybında geri yazmak için).

Genel döndürme adımları:
1. Yeni değeri üret (örn. `openssl rand -base64 64`; sertifika için aşağıdaki **Data Protection sertifikası** maddesi).
2. Dosyayı yaz (`install -m 0400 -o <uid>`), parola yöneticisini güncelle.
3. `sudo /opt/nirengi/bin/deploy.sh <mevcut sürüm>` (yeniden başlatma).
4. `/health/ready` ve bir giriş denemesi.

Özel durumlar:
- **HashKey:** yalnız sızıntıda. Önce bekleyen kimlik postalarını boşalt (`SELECT count(*) FROM identity.outbox_messages WHERE processed_at IS NULL` sıfıra inene dek bekle), sonra döndür.
- **Data Protection sertifikası:** RSA 3072, özel anahtarıyla PFX, tek satır base64 (API daha azını açılışta reddeder):
  ```bash
  openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes -subj /CN=nirengi-dataprotection -keyout dp.key -out dp.crt
  openssl pkcs12 -export -inkey dp.key -in dp.crt -passout pass:"$DP_PFX_PASSWORD" | base64 -w0 > DataProtection__Certificate
  shred -u dp.key dp.crt
  ```
  Tüm sertifikalar (güncel ve `PreviousCertificates__N`) tek `DataProtection__CertificatePassword` ile açılır: döndürmede parola aynı kalır. Parola değişecekse eski PFX'ler yeni parolayla yeniden paketlenir (`openssl pkcs12 -in eski.pfx -nodes | openssl pkcs12 -export -passout pass:"$YENI"`). Döndürme: eski değeri sıradaki boş `DataProtection__PreviousCertificates__N` dosyasına taşı, yenisini `DataProtection__Certificate`'e yaz; eskiler hiç silinmez (TOTP sırları onlarla korunmuş anahtarlarda kalır).
- **DB parolası:** `ALTER ROLE ... PASSWORD` → bağlantı dizesi dosyası → yeniden başlat.
- **CI dağıtım anahtarı:** yeni anahtar çifti, `authorized_keys`'te eskisini değiştir, GitHub sırrını güncelle.
- **GHCR jetonu:** süresi dolmadan yenisi, `docker login ghcr.io`.
- **Play yükleme anahtarı:** kaybolursa Play Console'dan yükleme anahtarı sıfırlama talebi (Play App Signing açık olduğu için uygulama anahtarı etkilenmez).

## 8. İzleme ve alarmlar

| Alarm | Kaynak | İlk bakılacak yer |
| --- | --- | --- |
| `/health/ready` `Healthy` değil | UptimeRobot (anahtar kelime) | `curl https://api.<alan>/health/ready`; `Degraded` ise outbox: `SELECT ... FROM <şema>.outbox_messages WHERE processed_at IS NULL ORDER BY occurred_at LIMIT 20` ve `last_error`; `Unhealthy` ise postgres |
| Erişilemiyor / TLS süresi | UptimeRobot | `docker compose ps`, Caddy logu |
| Yeni hata türü | Sentry e-postası | Sorunun `release`'i; gerekirse geri dönüş (bölüm 4) |
| Yedek gelmedi | Healthchecks `backup` | `journalctl -u nirengi-backup` |
| Kopya 2 çekmedi | Healthchecks `backup-pull` | Kopya 2 sunucusu, SSH anahtarı |
| Disk > %80 | Healthchecks `disk` | `docker system df`, eski imajlar, log boyutu |
| Prova kaçtı | Healthchecks `restore-drill` | Bölüm 6 prova |

Sentry'de kişisel veri görülürse: olayı sil, süzgeç testine (F2 kartı 3/4) o alanı ekle, düzelt.

## 9. E-posta (SMTP) ve DNS

- Sağlayıcı: ADR-0022 §6'daki ölçütlerle seçilir; değişim yalnız `Smtp__*` ayarları.
- DNS (`posta.<alan>`): SPF `include`, DKIM kayıtları, DMARC `p=none` → iki hafta sonra `p=quarantine`. Ana alan posta göndermiyorsa `v=spf1 -all` + DMARC `p=reject`.
- Denetim: kayıt ol → kodun Gmail ve Outlook'ta gelen kutusuna düştüğünü gör; başlıklarda `spf=pass dkim=pass dmarc=pass`.

## 10. Mobil yayın

1. `pubspec.yaml` `version: X.Y.Z+<herhangi>` güncelle, PR, birleştir.
2. `mobile-vX.Y.Z` etiketini it; `mobile-release.yml` (F2 kartı 10) imzalı AAB üretir (`versionCode = X*1000000 + Y*1000 + Z`).
3. AAB'yi iş akışı çıktısından indir, Play Console → İç test → yeni sürüm → yükle.
4. Kırıcı API değişikliğiyse yayılım bitince `Clients__MinimumVersion=X.Y.Z` (`.env`) ve `deploy.sh <mevcut sürüm>`.

## 11. Periyodik bakım

- Haftalık: Sentry'deki açık sorunlara bak.
- Aylık: geri yükleme provası (ilk üç ay); `apt` güvenlik güncellemelerinin uygulandığını ve yeniden başlatmanın olduğunu gör; disk eğilimi.
- Üç ayda bir: prova; PostgreSQL 17 minor sürümünü yükselt (compose değişikliği + dağıtım).
- Yıllık: GHCR jetonu, alan adı yenileme, TLS (Caddy kendisi yeniler; izleme doğrular).
