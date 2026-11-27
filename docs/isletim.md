# İşletim el kitabı

Üretim ortamını ayakta tutmak için adımlar. Kararlar ve gerekçeleri [ADR-0022](adr/0022-uretim-dagitimi-ve-isletim.md)'de; burada yalnız "ne yapılır". Henüz yazılmamış dosyalar "F2 kartı N" ile işaretli; kart bitince bağlantı ve komut ayrıntısı eklenir.

> Durum: iskelet. Komutlar kartlar bittikçe doğrulanıp kesinleşir.

## 1. Genel bakış

| Parça | Nerede | Not |
| --- | --- | --- |
| VPS | Türkiye, Ubuntu 24.04 LTS | `/opt/nirengi/` altında her şey |
| Compose | `/opt/nirengi/compose.prod.yml` ← [`deploy/compose.prod.yml`](../deploy/compose.prod.yml) | `caddy`, `api`, `migrate` (profil `tools`), `postgres` |
| Ters vekil | Caddy, [`deploy/Caddyfile`](../deploy/Caddyfile) | 80/443 tek açık port; `api.<alan>` |
| Sır olmayan ayar | `/opt/nirengi/.env` ← [`deploy/.env.example`](../deploy/.env.example) | `NIRENGI_VERSION`, alan adı, Sentry DSN, `Clients__MinimumVersion`, SMTP sunucusu |
| Sırlar | `/opt/nirengi/secrets/` (0700 root, dosyalar 0400) | Liste bölüm 7'de |
| Betikler | `/opt/nirengi/bin/` ← [`deploy/bin/`](../deploy/bin/) (`deploy.sh`, `ci-entry`: F2 kartı 9) | `deploy.sh`, `ci-entry`, `backup.sh`, `restore.sh`, `verify-restore.sh`, `diskcheck.sh` |
| Zamanlayıcılar | systemd ← [`deploy/systemd/`](../deploy/systemd/) | `nirengi-backup.timer` 03:30 UTC, `nirengi-disk.timer` 15 dk |
| Yedek kopya 1 | VPS `/var/backups/nirengi` | 7 günlük + son 3 dağıtım öncesi |
| Yedek kopya 2 | Türkiye'de ikinci sağlayıcıdaki depolama sunucusu | 05:00 UTC'de çeker; 7 günlük + 4 haftalık |
| İzleme | Sentry (AB), UptimeRobot, Healthchecks.io | Bölüm 8 |
| İmaj | `ghcr.io/<sahip>/nirengi-api:X.Y.Z` | `api-vX.Y.Z` etiketiyle üretilir (F2 kartı 5) |

Sürüm geçmişi: `/opt/nirengi/deploy-history` (`zaman sürüm imaj-özeti sonuç`).

## 2. Sunucu ilk kurulum (bir kez)

1. VPS'i aç, yönetici kullanıcıyı oluştur, SSH'yi yalnız anahtara çevir, root girişini kapat.
2. `sudo ./bootstrap.sh` ([`deploy/bin/bootstrap.sh`](../deploy/bin/bootstrap.sh)): Docker Engine + compose eklentisi, ufw (sshd'nin gerçek portu `limit` ile, 80, 443/tcp, 443/udp; sağlayıcı duvarında da 443/udp açılmalı), `unattended-upgrades` (Ubuntu güvenlik + Docker deposu, 04:30 yeniden başlatma), saat dilimi UTC, `/opt/nirengi` dizinleri, `deploy` kullanıcısı (docker grubunda değil; sudoers yalnız `/opt/nirengi/bin/deploy.sh`).
3. CI dağıtım anahtarı: kendi makinende `ssh-keygen -t ed25519 -f nirengi-ci -C nirengi-ci`. Açık anahtarı `/home/deploy/.ssh/authorized_keys`'e **tek satır** olarak yaz: `command="/opt/nirengi/bin/ci-entry",restrict ssh-ed25519 AAAA... nirengi-ci` (`chown deploy: ...`, 0600). GitHub → Settings → Environments → `production` ortamını aç, **Deployment branches** = yalnız `main`; sırları oraya ekle: `NIRENGI_DEPLOY_SSH_KEY` (özel anahtar), `NIRENGI_DEPLOY_HOST` (sunucu adı/IP), `NIRENGI_DEPLOY_KNOWN_HOSTS` (`ssh-keyscan [-p <port>] <sunucu>` çıktısı, parmak izini sağlayıcı panelinden doğrula), SSH 22 dışındaysa `NIRENGI_DEPLOY_PORT`. Özel repoda ortam sırları planınızda yoksa aynı adlarla repo sırları kullanılır (iş akışı ikisiyle de çalışır; o zaman dal kısıtı yoktur). Anahtar yalnız `deploy X.Y.Z` ve `status` çalıştırabilir; en eski bir önceki başarılı sürüme dönebilir, daha eskisi sunucuda elle `sudo deploy.sh` ister.
4. GHCR girişi (root): `docker login ghcr.io` (`read:packages` jetonu; süresi takvime yazılır).
5. `deploy/compose.prod.yml`, `deploy/Caddyfile`, `deploy/sql/` ve `deploy/bin/` dosyalarını `/opt/nirengi/`'ye kopyala (`bin/` root sahipli, başkası yazamaz: `deploy.sh` sudo ile root çalışır). Sırları bölüm 7'deki düzene göre yaz; her konteynerin kendi dizini vardır ve dizindeki her dosya adıyla bir ayar olur. Parolalar `openssl rand -hex 32` (bağlantı dizesinde kaçış gerektirmez):
   ```bash
   cd /opt/nirengi/secrets
   openssl rand -hex 32 > owner_password; openssl rand -hex 32 > app_password; chmod 0400 owner_password app_password
   openssl rand -hex 32 > postgres/postgres_password
   printf 'Host=postgres;Port=5432;Database=nirengi;Username=nirengi_app;Password=%s' "$(cat app_password)" > api/ConnectionStrings__Database
   printf 'Host=postgres;Port=5432;Database=nirengi;Username=nirengi_owner;Password=%s' "$(cat owner_password)" > migrate/ConnectionStrings__Database
   # api/: Jwt__SigningKey, Identity__Security__HashKey, DataProtection__Certificate, DataProtection__CertificatePassword, Smtp__Password
   chown -R 1654 api migrate && chown -R 70 postgres          # APP_UID (api, migrate), postgres (alpine)
   chmod 0400 api/* migrate/* postgres/* && chmod 0500 api migrate postgres
   ```
   `Smtp__Password` sağlayıcı parola istemiyorsa boş dosya olur; SMTP sunucusu ve gönderen adresi `.env`'de zorunludur (API onlarsız açılmaz).
6. `.env`'yi `.env.example`'dan doldur (`chmod 0600`). DNS'te yalnız A kaydı (AAAA yok: portlar IPv4'e bağlı; IPv6 istemciler tek adres gibi görünürdü).
7. Postgres'i başlat, rolleri kur (tekrar çalıştırılabilir; parolaları da eşitler). Parolalar komut satırında değil ortamda geçer:
   ```bash
   cd /opt/nirengi
   docker compose -f compose.prod.yml up -d --wait postgres
   NIRENGI_OWNER_PASSWORD=$(cat secrets/owner_password) NIRENGI_APP_PASSWORD=$(cat secrets/app_password) \
     docker compose -f compose.prod.yml exec -T -e NIRENGI_OWNER_PASSWORD -e NIRENGI_APP_PASSWORD postgres \
     psql -U postgres -d nirengi -f /sql/00-roles.sql
   ```
8. İlk dağıtım: bölüm 3.
9. DNS: `api.<alan>` A kaydı; posta kayıtları (SPF, DKIM, DMARC) bölüm 9.
10. Yedek anahtarı ve zamanlayıcılar. `age` anahtar çiftini **kendi makinende** üret; sunucuya yalnız açık anahtar gider:
    ```bash
    age-keygen -o nirengi-backup.key          # özel anahtar: parola yöneticisi + kâğıt; sunucuya koyma
    age-keygen -y nirengi-backup.key          # çıktıyı sunucuda /opt/nirengi/backup.pub dosyasına yaz
    ```
    Healthchecks.io'da `backup` (günlük, 26 sa tolerans), `disk` (15 dk) denetimlerini aç, ping adreslerini `.env`'ye yaz (`HC_BACKUP_URL`, `HC_DISK_URL`). Sonra:
    ```bash
    cp deploy/systemd/nirengi-backup.* deploy/systemd/nirengi-disk.* /etc/systemd/system/
    systemctl daemon-reload && systemctl enable --now nirengi-backup.timer nirengi-disk.timer
    sudo /opt/nirengi/bin/backup.sh --tag manual   # ping'in geldiğini gör
    ```
11. Kopya 2 sunucusu (Türkiye'de başka sağlayıcı):
    - Orada bir anahtar çifti üret; açık anahtarı VPS'te yedek okuyucu bir kullanıcının `authorized_keys`'ine `command="rrsync -ro /var/backups/nirengi",restrict <anahtar>` olarak ekle (`chgrp <grup> /var/backups/nirengi && chmod 2750 /var/backups/nirengi`: setgid sayesinde yeni yedekler o grupla 0640 oluşur).
    - Kopya 2'de VPS'in sunucu anahtarını `known_hosts`'a ekle (`ssh-keyscan <vps> >> ~/.ssh/known_hosts`, parmak izini sağlayıcı panelinden doğrula); çekme `StrictHostKeyChecking=yes` ile çalışır.
    - Kopya 2'ye `deploy/bin/backup-pull.sh` → `/usr/local/bin/nirengi-backup-pull.sh`, `/etc/nirengi-pull.env`: `PULL_SOURCE=<kullanıcı>@<vps>:`, `PULL_DEST=/srv/nirengi-backups`, `PULL_SSH_KEY=<anahtar yolu>`, `HC_PULL_URL=<ping>`.
    - `deploy/systemd/nirengi-backup-pull.*` → `/etc/systemd/system/`, `systemctl enable --now nirengi-backup-pull.timer`.

## 3. Dağıtım

Ön koşul: `main` yeşil; sürüm etiketi `api-vX.Y.Z` itildi ve `release-api.yml` (F2 kartı 5) imajı GHCR'ye yükledi.

- **GitHub'dan:** Actions → `deploy-api` → Run workflow → `version = X.Y.Z` (etiket `api-vX.Y.Z` var olmalı). Çıktının sonunda `deploy-history`'nin son satırları görünür.
- **Sunucudan:** `sudo /opt/nirengi/bin/deploy.sh X.Y.Z`. Aynı sürümü yeniden vermek API'yi yeniden yaratır (değişen sırları okur).
- Dağıtım ve geri yükleme aynı kilidi paylaşır (`/opt/nirengi/.operations.lock`); yedek onu bekler. SSH ya da CI işi kopsa bile dağıtım sunucuda sonuna kadar sürer; çıktısı `/opt/nirengi/deploy.log`'da. Başlamadan Caddy'nin çalıştığı denetlenir.
- `deploy-history` satırı: `zaman sürüm özet sonuç (was <önceki>)`. Sonuçlar: `ok`; `ok-degraded` (açıldı ama `/health/ready` Degraded: takılı outbox alarmı sürüyor, yeni sürüm onu düzeltiyorsa birkaç dakikada `Healthy` olur); `rolled-back` (yeni sürüm açılmadı, öncekine dönüldü); `migrate-failed` (çalışan sürüm değişmedi; uygulanmış migration olduysa genişletme olduğu için zararsız); `backup-failed`, `pull-failed` (hiçbir şey değişmedi); `failed` (dönülecek başka sürüm yoktu); `rollback-failed` (önceki sürüm de açılmadı: hemen bak).

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

- Otomatik: her gün 03:30 UTC (`nirengi-backup.timer`) ve her dağıtımdan önce `backup.sh`: `pg_dump -Fc` → `pg_restore --list` doğrulaması → `age` ile şifreleme → `/var/backups/nirengi/nirengi-<UTC>-<etiket>.dump.age`, `.sha256` ve `.counts` (çekirdek tablo satır sayıları; prova karşılaştırır), Healthchecks başlangıç/başarı/hata ping'i. Kopya 1 saklama: 7 gün + son 3 `pre-deploy`.
- Elle: `sudo /opt/nirengi/bin/backup.sh --tag manual`.
- Kopya 2 her gün 05:00'te çeker; çekmeyi sunucu değil kopya 2 başlatır. Saklama dosyanın kopya 2'ye **varış** zamanına göredir (ele geçmiş bir VPS geçmiş tarihli adla gerçek yedekleri itemez); sağlama tutmayan dosya `quarantine/`'e alınır ve çekme başarısız sayılır.
- Döküm ile `.counts` aynı dışa aktarılmış anlık görüntüden (snapshot) okunur; prova birebir karşılaştırır.
- `age` özel anahtarı sunucuda **yoktur**; parola yöneticisinde ve kâğıtta.

## 6. Yedekten geri yükleme

Ne zaman: veri bozulması, DB kaybı, sunucu kaybı. Hedef süre (RTO): 2 saat. Veri kaybı (RPO): en çok 24 saat (dağıtım öncesi yedek daha yeniyse daha az).

1. Kullanılacak yedeği seç (kopya 1, yoksa kopya 2), SHA-256'yı doğrula.
2. Özel `age` anahtarını sunucuya geçici kopyala (`/root/restore.key`, 0400).
3. `sudo /opt/nirengi/bin/restore.sh <dosya>` (onay için `RESTORE` yazılır; anahtar yolu `RESTORE_KEY` ile değişir): önce mevcut DB'nin güvenlik yedeğini alır (`pre-restore`), API'yi durdurur; **tek transaction'da** modül şemalarını düşürür (yedekten yeni migration'ların nesneleri dahil), dökümü `nirengi_owner` olarak yükler ve senkron dönemini yeniler (`UPDATE platform.sync_epoch SET epoch = gen_random_uuid()`); sonra `migrate`, API'yi başlatır ve `Healthy` bekler. Transaction'dan önce ya da içinde hata olursa veri değişmez ve API yeniden başlatılır; `migrate` hata verirse betik API'yi başlatmaz ve bunu söyler. Yeni dönemi gören cihaz imlecini 0'a çeker, elindeki kayıtları yeniden gönderir (sunucu LWW ile birleştirir). Sunucuda aynı ya da daha yeni hâli olan kayıtlar yeniden gönderilmez; yalnız yedekte olmayanlar gider. Sınırlar: yedek anından sonra silinip silmesi eşitlenmiş kayıtlar cihazda iz bırakmadığı için geri gelir (kullanıcı yeniden silebilir); kaybolan eski bir kayıt bugünkü kurallarla (365 günlük tarih penceresi, emekli konu/kaynak) reddedilip cihazda "reddedildi" olarak kalabilir. Dönem yenilemesi atlanırsa (betik dışı elle `pg_restore`) kayıp sessiz olur: geri yükleme yalnız `restore.sh` ile yapılır.
4. Özel anahtarı sil: `shred -u /root/restore.key`.
5. `/health/ready` = `Healthy`; admin girişi (Data Protection sertifikası doğruysa TOTP çalışır; sertifika kayıpsa admin TOTP'yi yeniden kurar).
6. Silinen hesaplar: yedek anından sonra kalıcı silinen hesaplar yedekte "silme istendi" durumunda ve eski tarihle durur; saatlik temizlik işi onları bir saat içinde yeniden siler, elle iş gerekmez. Yedek anından **sonra** istenen silmeler kaybolur (log kullanıcı kimliği taşımaz): o aralıkta hesabını silen biri hesabı açık görür, yeniden silmesi gerekir. Bu, RPO'nun (≤ 24 sa) kabul edilen sonucudur.
7. Mobil istemciler yeni dönemi görünce kayıtlarını yeniden gönderir (F2 kartı 12); bir test cihazında kaydın geri geldiğini doğrula.

Sunucu tümden kaybolduysa: bölüm 2'yi yeni VPS'te uygula, sırları parola yöneticisinden geri yaz, sonra bu bölüm; DNS'i yeni IP'ye çevir.

### Geri yükleme provası

Sıklık: ilk üç ay ayda bir, sonra üç ayda bir. Healthchecks "restore-drill" denetimi kaçırılan provayı alarm yapar.

1. Kopya 2'den en yeni yedeği kendi makinene indir (şifreli disk).
2. Üretimdeki sürümü `deploy-history`'nin son `ok` satırından al, sonra:
   ```bash
   HC_DRILL_URL=<restore-drill ping> deploy/bin/verify-restore.sh nirengi-....dump.age \
     --identity nirengi-backup.key --image ghcr.io/<sahip>/nirengi-api:<X.Y.Z>
   ```
   Atılabilir bir PostgreSQL 17'ye yükler; yedeğin imajın şema sürümünde olduğunu (migrate uygulanacak bir şey bulmamalı), satır sayılarının `.counts` ile aynı olduğunu ve API'nin bu DB'ye karşı `Healthy` açıldığını denetler; başarıda ping atar.
3. İndirilen dosyayı ve çözülmüş dökümü sil.
4. Sonuç satırı `~/nirengi-drill-history`'ye kendiliğinden yazılır (`zaman drill dosya imaj ok`; yol `DRILL_HISTORY` ile değişir).

## 7. Sırlar ve döndürme

| Sır (dosya adı `/opt/nirengi/secrets/`) | Kim okur | Döndürme etkisi |
| --- | --- | --- |
Düzen: `/opt/nirengi/secrets/` (0700 root) altında konteyner başına dizin (0500, okuyan konteynerin kullanıcısı: `api/` ve `migrate/` 1654, `postgres/` 70), dosyalar 0400. Dizin konteynerde `/run/secrets` olur; sonradan eklenen dosya (örn. `PreviousCertificates__0`) yeniden başlatmada okunur.

| Sır (dosya) | Kim okur | Döndürme etkisi |
| --- | --- | --- |
| `api/ConnectionStrings__Database` | api | Yok (yeni parola + yeniden başlatma) |
| `migrate/ConnectionStrings__Database` | migrate | Yok |
| `owner_password`, `app_password` (kökte, root) | yalnız rol kurulumu (`00-roles.sql`) | Bağlantı dizesiyle birlikte |
| `postgres/postgres_password` | postgres (süper kullanıcı) | Yalnız ilk açılışta okunur; sonra `ALTER ROLE postgres` |
| `api/Jwt__SigningKey` | api | Erişim jetonları (≤ 15 dk) geçersiz; istemci refresh ile sürer |
| `api/Identity__Security__HashKey` | api | **Yıkıcı:** tüm refresh token'lar, kurtarma kodları, bekleyen kodlar ve kimlik postaları geçersiz; herkes yeniden giriş yapar |
| `api/DataProtection__Certificate` (+ `__CertificatePassword`) | api | Eski sertifika `DataProtection__PreviousCertificates__N`'e taşınır, **silinmez** |
| `api/Smtp__Password` | api | Yok |
| `age` açık anahtarı (`/opt/nirengi/backup.pub`) | backup.sh | Yeni yedekler yeni anahtarla; eski özel anahtar eski yedekler bitene dek (~35 gün) saklanır |

Her sırrın çevrimdışı kopyası parola yöneticisindedir (sunucu kaybında geri yazmak için).

Genel döndürme adımları:
1. Yeni değeri üret (örn. `openssl rand -base64 64`; sertifika için aşağıdaki **Data Protection sertifikası** maddesi).
2. Dosyayı yaz (`install -m 0400 -o <uid>`), parola yöneticisini güncelle.
3. `docker compose -f /opt/nirengi/compose.prod.yml restart api` (dizin bağlı olduğu için yeni dosya yeniden başlatmada okunur).
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

Sentry ayarı (Sentry Logs/Metrics kodda kapalı; sunucuda da açılmaz): API `.env`'de `SENTRY_DSN` (boşsa kapalı; compose `Sentry__Dsn`'e verir, ortam `production`); mobil `SENTRY_DSN_MOBILE` depo değişkeni. Sunucu tarafında (Sentry proje ayarı) "Data Scrubber", "Use Default Scrubbers" ve "Prevent Storing of IP Addresses" açık; ek hassas alanlar `password, code, email, token, refreshToken, note, title, sourceName` (ADR-0022 §5 katman 4).

## 9. E-posta (SMTP) ve DNS

- Sağlayıcı: ADR-0022 §6'daki ölçütlerle seçilir; değişim yalnız `Smtp__*` ayarları.
- DNS (`posta.<alan>`): SPF `include`, DKIM kayıtları, DMARC `p=none` → iki hafta sonra `p=quarantine`. Ana alan posta göndermiyorsa `v=spf1 -all` + DMARC `p=reject`.
- Denetim: kayıt ol → kodun Gmail ve Outlook'ta gelen kutusuna düştüğünü gör; başlıklarda `spf=pass dkim=pass dmarc=pass`.

## 10. Mobil yayın

Bir kez (Play Console ve GitHub):
- Play Console'da uygulamayı aç, **Play App Signing**'i kabul et (uygulama anahtarı Google'da; bizimki yalnız yükleme anahtarı, kaybolursa Play desteğiyle sıfırlanır).
- Yükleme anahtarı (kendi makinende; `.jks` ve parolalar parola yöneticisine + çevrimdışı kopya):
  ```bash
  keytool -genkeypair -keystore nirengi-upload.jks -storetype PKCS12 -alias nirengi-upload -keyalg RSA -keysize 4096 -validity 10000
  base64 -w0 nirengi-upload.jks   # → NIRENGI_UPLOAD_KEYSTORE_BASE64
  ```
- GitHub → Settings → Environments → **`mobile-release`** ortamını aç; **Deployment branches and tags** = yalnız `mobile-v*` etiketleri (dal ve PR'lar anahtara ulaşamaz). İsteğe bağlı: zorunlu onaylayıcı (sen). Ayrıca Settings → Rules → yeni etiket kuralı: `mobile-v*` etiketini yalnız sen oluşturabil.
  - Ortam sırları: `NIRENGI_UPLOAD_KEYSTORE_BASE64`, `NIRENGI_KEYSTORE_PASSWORD`, `NIRENGI_KEY_ALIAS` (`nirengi-upload`), `NIRENGI_KEY_PASSWORD` (PKCS12'de mağaza parolasıyla aynı). Depo düzeyinde **koyma**.
  - Değişkenler (depo ya da ortam): `API_BASE_URL_PROD` = `https://api.<alan>` (önizleme APK'larının `API_BASE_URL`'ünden ayrı), `SENTRY_DSN_MOBILE` (boşsa sürüm hata raporlamaz, iş uyarı verir).

Her sürüm:
1. `mobile/pubspec.yaml` → `version: X.Y.Z+1` (`+`'dan sonrası yok sayılır), PR, birleştir.
2. `git tag mobile-vX.Y.Z <main'deki commit> && git push origin mobile-vX.Y.Z`. `mobile-release` iş akışı: etiket `main` üzerinde mi, sürüm `pubspec` ile aynı mı denetler; `versionCode = X*1000000 + Y*1000 + Z`; imzalı AAB'yi derler, imzanın yükleme anahtarına ait olduğunu doğrular.
3. AAB'yi iş akışı çıktısından indir (`nirengi-X.Y.Z-aab`, 30 gün), Play Console → Test → İç test → Yeni sürüm → yükle. Aynı `versionCode` iki kez yüklenemez: düzeltme yeni PATCH ister.
4. Kırıcı API değişikliğiyse yayılım bitince `.env`'de `CLIENTS_MINIMUM_VERSION=X.Y.Z` ve `sudo deploy.sh <mevcut sürüm>` (yeniden başlatma).

İş akışı her PR'da (`mobile/android/**`, `mobile/tool/**` ya da iş akışı değişince) ve daldan elle çalıştırmada aynı betikle (`mobile/tool/build_release_aab.sh`) atılabilir bir anahtarla kuru çalışır; hiçbir sırra dokunmaz. Bir etiketten elle çalıştırma gerçek sürüm üretir (aynı etiketin AAB'sini yeniden almak için kullanılabilir).

## 11. Periyodik bakım

- Haftalık: Sentry'deki açık sorunlara bak.
- Aylık: geri yükleme provası (ilk üç ay); `apt` güvenlik güncellemelerinin uygulandığını ve yeniden başlatmanın olduğunu gör; disk eğilimi.
- Üç ayda bir: prova; PostgreSQL 17 minor sürümünü yükselt (compose değişikliği + dağıtım).
- Yıllık: GHCR jetonu, alan adı yenileme, TLS (Caddy kendisi yeniler; izleme doğrular).
