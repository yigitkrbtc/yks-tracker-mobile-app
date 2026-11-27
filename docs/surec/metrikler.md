# Süreç metrikleri

Her sprint kapanışında operasyon şefi bir satır ekler ([operasyon.md](operasyon.md)). Amaç rol tablosunun doğru kademede olup olmadığını görmek: ilk denemede geçme oranı düşük olan kart türü bir kademe yükselir, sürekli yüksek olan düşer.

| Sprint | Kart | İlk denemede geçen | Yükseltme | Opus kartı | xhigh | İnceleme bulgusu | Not |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F2 (ADR-0022) | 13 (1–13; bu oturumda 3, 4, 7+11, 8, 9, 10, 12) | 7/7 kart ilk incelemede "düzeltmeyle"; CI ilk koşuda: 3/7 | Yok | Hepsi (Opus, high) | Yok | 6 kritik (mobil Sentry Android'de gönderim yok; backend Sentry Logs sızıntısı; CI'da duman testi hiç koşmadı ×2; yedek $! ve SIGPIPE), önemlilerin tamamı düzeltildi | Kod yazan ajan incelemedi; her kartta `inceleyici` (effort high). Altyapı betikleri yalnız CI'da sınanabildi; yerelde sahte docker + PostgreSQL ile ek koşu. |
