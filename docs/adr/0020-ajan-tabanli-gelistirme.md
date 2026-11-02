# ADR-0020: Ajan tabanlı geliştirme süreci ve model yönlendirme

- Durum: Kabul edildi
- Tarih: 2026-11-02

## Bağlam

Geliştirme Yiğit + Claude ile yürüyor. Sprintler büyüdükçe tek oturumun her işi aynı modelle yapması hem pahalı (rutin iş için Opus) hem riskli (zor karar için düşük kademe). Yiğit'in kısıtı: yalnız Sonnet (medium/high) ve Opus (medium/high); Opus xhigh çok nadir ve gerekçeli. Ajanların birbirini incelemesi ortak kör nokta taşır; süreç ölçülmezse kademe seçiminin doğruluğu bilinemez.

## Karar

1. Her sprint bir thread'de **operasyon şefi** (Opus medium, kırılım/kapanış high) tarafından yürütülür: kart kırılımı, dalga sırası, en çok 3 paralel kart, doğrulama, tek PR, bellek.
2. İşi 8 rol ajanı yapar (`.claude/agents/`): mimar, hata-avcisi, inceleyici (Opus); gelistirici-plus, arastirmaci, gelistirici, testci, dokumanci (Sonnet). Model ve efor yalnız ajan frontmatter'ında tanımlıdır.
3. Kısıt `PreToolUse` kancasıyla zorlanır (`.claude/hooks/model_guard.py`): haiku/fable/low/max ret; xhigh istemde `XHIGH-GEREKCE:` ister; modelsiz yerleşik ajan ret.
4. Başarısızlıkta bir kademe yükseltme; Opus high'ta iki başarısızlıkta dur ve Yiğit'e sor. xhigh 3 tetikle sınırlı, Yiğit onaylı, sprint başına ≤ 1.
5. Kalite kapıları: kodu yazan ajan incelemez (inceleyici salt okunur; güvenlik/motor diff'inde high), `main` dal koruması, Dependabot + gitleaks, sprint başına süreç metriği (`docs/surec/metrikler.md`), Yiğit'in elle kabul testi.
6. Görev kartları sprint plan dokümanında tablo satırıdır; GitHub Issues kullanılmaz.

Ayrıntı: [`docs/surec/operasyon.md`](../surec/operasyon.md).

## Sonuçlar

- Uygulama ve test Sonnet'e iner; Opus karar, zor hata ve incelemede kalır.
- Alt ajan kullanımı bu akışta onaylıdır (CLAUDE.md "Süreç").
- Kanca yalnız Claude Code'da çalışır; Python 3 gerektirir. Efor alanını yok sayan bir ortamda efor yalnız ajan talimatıyla kalır.
- Kademe tablosu metriklere göre PR ile değişir.
- Yayın hazırlığı sprintinin denetim/KVKK kararı ADR-0021 olur.
