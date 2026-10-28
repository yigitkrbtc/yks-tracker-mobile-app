# Seed verisi

ÖSYM 2019–2026 TYT ve AYT kitapçıklarından soru soru çıkarılan konu taksonomisi ve konu ağırlıkları. Curriculum modülü bu dosyaları derlemeye gömer ve her migrate sonrası `curriculum` şemasına idempotent yükler (içerik hash'i değişmediyse atlar; ADR-0013).

| Dosya | Satır | İçerik |
| --- | --- | --- |
| `exam_structure.json` | 8 test, 20 ders, 4 puan türü | Sınav yılı, testler, ders → test ve puan türü → test eşlemesi. DİL'de YDT testi yok (taksonomisi henüz yok). |
| `konu_taksonomisi.csv` | 370 konu | `session, unit, topic_code, topic, subtopics, grade, description`; `topic_code` kalıcı anahtar (ör. `AYT.AMAT.26`) |
| `konu_agirliklari.csv` | 370 konu | Yıl başına soru sayısı (2019–2026), `q_i` (2022–2026 ortalaması, test toplamına normalize), `tier` (çok/orta/az/nadir) |
| `tum_etiketler.csv` | 2.328 soru | Soru bazında etiket: yıl, oturum, test, soru no, konu, alt konu, ikincil konu, güven |

`tools/aggregate.py` etiketlerden ağırlıkları üreten betik (kaynak PDF'ler repoda değil). Kurallar:

- `q_i` = 2022–2026 ortalama soru sayısı, testin toplam soru sayısına normalize.
- 2020 ve 2023 kısıtlı müfredat yıllarında 12. sınıf konusu 0 soruysa o yıl ortalamaya girmez.
- Kademe: yılların yarısından azında çıktıysa `nadir`; aksi halde ortalama ≥ 2 `çok`, ≥ 0,8 `orta`, değilse `az`.

CSV'ler elle düzenlenmez; değişiklik betikle yeniden üretilir ve PR'da fark incelenir. `topic_code` asla değiştirilmez veya yeniden kullanılmaz: konu kimliği ondan türer. `tests/Curriculum.UnitTests` dosyaların tutarlılığını denetler.
