# ADR-0010: Deneme kaydı ayrı aggregate

- Durum: Kabul edildi
- Tarih: 2026-10-27

## Bağlam

Öğrenci konudan bağımsız TYT/AYT/branş denemesi çözer ve ders bazında net girer (Yiğit, 27.10.2026).

## Karar

- `MockExam` Tracking modülünde konu testinden ayrı aggregate'tir; deneme soruları `topic_results`'a yazılmaz (çifte sayım yok).
- Giriş formu Curriculum'daki `exam_year` sürümlü sınav şablonundan gelir (TYT, AYT alanları, YDT, branş).
- Ders başına D ve Y girilir, B ve net (D − Y/4) sunucuda hesaplanır; çözülmeyen bölüm `null` saklanır.
- Yanlışların konuya etiketlenmesi isteğe bağlıdır; yalnız tam etiketli bölümler M11c'ye girer.
- Puan ve sıralama tahmini yoktur.

## Sonuçlar

Analiz tarafı gereksinim dokümanındaki "Deneme kaydı ve analizi" ve M11 bölümüne birebir uyar. Uygulama ayrıntıları ve şablon tablosundaki sapma: ADR-0015.
