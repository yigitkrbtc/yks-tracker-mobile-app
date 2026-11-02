# Operasyon: sprint yönetimi ve model yönlendirme

Sprint işi bir **operasyon şefi** (OŞ) tarafından görev kartlarına bölünür ve `.claude/agents/` altındaki rol ajanlarına dağıtılır. Karar: [ADR-0020](../adr/0020-ajan-tabanli-gelistirme.md). Kod kuralları [`CLAUDE.md`](../../CLAUDE.md)'de; burada tekrar edilmez.

## Roller

Model ve efor **yalnız** ajan dosyasının frontmatter'ında tanımlıdır; bu tablo ajan adına atıf yapar.

| Görev türü | Ajan | Kademe |
| --- | --- | --- |
| Mimari karar, ADR, modül sınırı; güvenlik/kimlik/KVKK tasarımı; analiz/öneri motoru kuralı | [`mimar`](../../.claude/agents/mimar.md) | Opus |
| Zor hata (yarış, senkron, LWW, idempotency, flaky) | [`hata-avcisi`](../../.claude/agents/hata-avcisi.md) | Opus |
| PR öncesi bağımsız inceleme (güvenlik/motor diff'inde çağrıda `effort: high`) | [`inceleyici`](../../.claude/agents/inceleyici.md) | Opus |
| Kalıplı, çok katmanlı uygulama | [`gelistirici-plus`](../../.claude/agents/gelistirici-plus.md) | Sonnet |
| Dış araştırma (kütüphane, mağaza kuralı, mevzuat, sağlayıcı) | [`arastirmaci`](../../.claude/agents/arastirmaci.md) | Sonnet |
| Rutin, tek katmanlı uygulama | [`gelistirici`](../../.claude/agents/gelistirici.md) | Sonnet |
| Test yazma, CI kırmızısı (lint/analyze/format) | [`testci`](../../.claude/agents/testci.md) | Sonnet |
| Doküman, durum satırı, metrik satırı | [`dokumanci`](../../.claude/agents/dokumanci.md) | Sonnet |

**Operasyon şefi** sprint thread'inin kendisidir: Opus medium; kırılım ve kapanışta Opus high. Kod yazmaz; kırılım, atama, doğrulama, entegrasyon düzeltmesi, PR ve bellek onun işidir.

İzinli kademeler, ucuzdan pahalıya: **Sonnet medium → Sonnet high → Opus medium → Opus high**. Haiku, Fable, `low` ve `max` efor yasak. `.claude/hooks/model_guard.py` (`PreToolUse`) bunu zorlar: yerleşik ajan (`Explore`, `general-purpose`, `Plan`) ancak `model: sonnet|opus` ile çağrılır.

### Opus xhigh

Yalnız şu tetiklerden biri varsa, gerekçe kartın "Gerekçe" alanına ve istemin başına `XHIGH-GEREKCE: <gerekçe>` olarak yazılarak ve Yiğit onayıyla; sprint başına en çok 1:

1. Opus high aynı kartı iki kez denedi, bitiş kriteri geçmedi.
2. Veri kaybı veya kişisel veri sızıntısı riski taşıyan, geri alınamaz migration/geri dönüş tasarımı.
3. Golden dataset ile scipy referansı arasında açıklanamayan sapma.

## Sprint akışı

1. **Kırılım (OŞ, Opus high):** Sprint hedefi 0,5–1 günlük, tek çıktılı kartlara bölünür (tipik 6–12). Kartlar dalgalara dizilir: ADR/sözleşme → domain → application/endpoint + OpenAPI → admin/mobil → test → doküman.
2. **Onay a:** Kırılım + ajan ataması Yiğit'e sunulur; onaysız kod başlamaz.
3. **Dalga yürütme:** Aynı dalgada yalnız dosya kümesi çakışmayan kartlar paralel (en çok 3; aynı modülün iki kartı asla). Ajana yalnız kart + ilgili dosya yolları + ilgili ADR verilir.
4. **Doğrulama (OŞ):** Bitiş kriteri komutu koşturulur, çıktı görülür; `git diff --stat` kapsam dışı dosya içermez. Kanıtsız "bitti" kabul edilmez.
5. **Dalga sonu:** Pahalı testler (`Host.IntegrationTests`, golden) dalga sonunda bir kez.
6. **İnceleme:** Tüm diff `inceleyici`'ye; kodu yazan ajan kendi işini incelemez. Güvenlik, kimlik, KVKK, analiz/öneri motoru içeren diff'te `effort: high`. Bulgular düzeltme kartı olur.
7. **Kapanış (OŞ, Opus high):** Sprint başına bir dal + bir PR (dalga başına commit), CI takibi, [`metrikler.md`](metrikler.md)'ye satır, proje belleği ve CLAUDE.md "Durum" güncellemesi.
8. **Kabul:** Yiğit sprint sonunda uygulamayı elle dener ve PR'ı birleştirir.

### Onay noktaları (Yiğit)

a) kırılım + atama, b) yeni ADR veya `RuleSet` sürüm artışı, c) Opus xhigh, d) geri alınamaz adım (prod, veri silme, dış servis, mağaza yüklemesi), e) PR birleştirme.

## Yükseltme ve düşürme

1. Bitiş kriteri geçmezse kart **bir üst kademede** yeniden çalışır; önceki denemenin hata çıktısı girdi olarak verilir. Aynı kademede ikinci deneme yok.
2. Yükseltmeden önce kart iki çıktıya bölünebiliyorsa önce bölünür.
3. Opus high'ta iki başarısızlık → dur, Yiğit'e engel olarak sun (xhigh önerisi veya kapsam kırpması).
4. Opus'un tasarımının uygulaması Sonnet'e iner. Aynı tür kart üst üste 3 kez ilk denemede geçerse rol tablosunda bir kademe düşürme önerilir (değişiklik = PR).
5. Ajan dönüşü ≤ 15 satır: değişen dosyalar, koşturulan komut ve gerçek sonucu, açık risk. Mimari soru için önce `graphify query`.

## Görev kartı

Kartlar sprint plan dokümanında tablo satırıdır; PR gövdesi kart numaralarını listeler. Kart, bitiş kriteri net değilse başlamaz.

| Alan | İçerik |
| --- | --- |
| No / dalga | `S7b-03` / W1 |
| Hedef | Tek cümle, tek çıktı |
| Ajan | `gelistirici-plus` (sapma varsa efor) |
| Gerekçe | Varsayılandan sapma veya xhigh için zorunlu |
| Girdi | Dosya yolları, ADR, önceki kart çıktısı |
| Çıktı | Değişen/yeni dosyalar |
| Bitiş kriteri | Komut + beklenen sonuç (ör. `dotnet test --project tests/Identity.UnitTests` yeşil) |
| Bağımlılık | Önceki kart numaraları |
| Deneme / son kademe | OŞ doldurur: `1 / Sonnet high` |

## Kalite kapıları

| Kapı | Nerede |
| --- | --- |
| Build (uyarı = hata), OpenAPI drift, mimari + birim + golden + entegrasyon testi, mobil analyze/test, admin lint/test/build | `.github/workflows/ci.yml` |
| Gizli bilgi taraması | `ci.yml` → `secrets` işi (gitleaks) |
| Bağımlılık güncellemesi (haftalık) | `.github/dependabot.yml` |
| Bağımsız inceleme | `inceleyici` ajanı, adım 6 |
| `main` koruması | GitHub ayarı, aşağıda |
| Elle kabul | Yiğit, adım 8 |

### `main` dal koruması (Yiğit bir kez açar)

GitHub → repo **Settings → Rules → Rulesets → New branch ruleset**: hedef `main`; **Restrict deletions**, **Block force pushes**, **Require a pull request before merging** (1 onay), **Require status checks to pass** (`backend`, `backend-image`, `mobile`, `admin`, `secrets`; "Require branches to be up to date" açık). Bypass listesi boş.
