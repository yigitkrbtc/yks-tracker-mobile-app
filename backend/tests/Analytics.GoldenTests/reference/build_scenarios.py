#!/usr/bin/env python3
"""Writes the input half of each golden scenario. Run reference.py afterwards to fill "expected".

Scenarios are plain JSON so a reviewer can read them; this script only saves typing. A scenario
added by hand is fine too, as long as its name does not collide with one below.
"""
import json
from datetime import date, timedelta
from pathlib import Path

SCENARIOS = Path(__file__).resolve().parent.parent / "Scenarios"
TODAY = date(2026, 10, 31)


def ago(days):
    return (TODAY - timedelta(days=days)).isoformat()


CATALOG = {
    "subjects": [
        {"code": "TUR", "tests": ["TYT-TUR"]},
        {"code": "MAT", "tests": ["TYT-MAT"]},
        {"code": "TAR", "tests": ["TYT-SOS"]},
        {"code": "COG", "tests": ["TYT-SOS"]},
        {"code": "FIZ", "tests": ["AYT-FEN"]},
    ],
    "topics": [
        {"id": "paragraf", "subject": "TUR", "q": 24.0},
        {"id": "sozcuk", "subject": "TUR", "q": 6.0},
        {"id": "dilbilgisi", "subject": "TUR", "q": 10.0},
        {"id": "problemler", "subject": "MAT", "q": 14.0},
        {"id": "sayilar", "subject": "MAT", "q": 8.0},
        {"id": "fonksiyon", "subject": "MAT", "q": 4.0},
        {"id": "olasilik", "subject": "MAT", "q": 3.0},
        {"id": "ucgen", "subject": "MAT", "q": 7.0},
        {"id": "kumeler", "subject": "MAT", "q": 1.5},
        {"id": "permutasyon", "subject": "MAT", "q": 2.5},
        {"id": "osmanli", "subject": "TAR", "q": 3.0},
        {"id": "inkilap", "subject": "TAR", "q": 2.0},
        {"id": "iklim", "subject": "COG", "q": 3.0},
        {"id": "nufus", "subject": "COG", "q": 2.0},
        {"id": "elektrik", "subject": "FIZ", "q": 6.0},
    ],
    "sections": [
        {"code": "TYT-TUR", "test": "TYT-TUR", "parent": None, "subjects": ["TUR"], "questionCount": 40},
        {"code": "TYT-MAT", "test": "TYT-MAT", "parent": None, "subjects": ["MAT"], "questionCount": 40},
        {"code": "TYT-SOS", "test": "TYT-SOS", "parent": None, "subjects": ["TAR", "COG"], "questionCount": 10},
        {"code": "TYT-SOS.TAR", "test": "TYT-SOS", "parent": "TYT-SOS", "subjects": ["TAR"], "questionCount": 5},
        {"code": "TYT-SOS.COG", "test": "TYT-SOS", "parent": "TYT-SOS", "subjects": ["COG"], "questionCount": 5},
    ],
}

TYT = ["TYT-TUR", "TYT-MAT", "TYT-SOS"]


def rec(topic, days, correct, wrong, blank=0, repeat=False, test=None):
    return {"test": test or f"{topic}-{days}-{correct}-{wrong}-{blank}", "topic": topic, "date": ago(days),
            "total": correct + wrong + blank, "correct": correct, "wrong": wrong, "blank": blank, "repeat": repeat}


def sec(code, n, correct, wrong, full=False, marks=()):
    return {"code": code, "questionCount": n, "correct": correct, "wrong": wrong, "full": full,
            "marks": [{"topic": t, "wrong": w, "blank": b} for t, w, b in marks]}


def mock(i, days, *sections):
    return {"id": f"m{i:02d}", "date": ago(days), "sections": list(sections)}


def spread(topic, correct, wrong, blank=0, days=(1, 8, 15, 22), size=None):
    """Splits counts evenly over several records on the given days."""
    n = len(days)
    out = []
    for k, dd in enumerate(days):
        c = correct // n + (1 if k < correct % n else 0)
        w = wrong // n + (1 if k < wrong % n else 0)
        b = blank // n + (1 if k < blank % n else 0)
        out.append(rec(topic, dd, c, w, b))
    return out


S = {}

S["01_no_data"] = ("Hiç kayıt yok: konular gri, öncelik yok, az verili liste q'ya göre.", [], [])
S["02_single_record"] = ("Tek 10 soruluk kayıt: güven 'çok az veri', bant yetersiz.", [rec("sayilar", 0, 7, 3)], [])
S["03_wald_trap_20_of_20"] = ("20/20: aralık [1;1] değil, üst sınır 1'in altında.",
                              [rec("fonksiyon", 0, 10, 0), rec("fonksiyon", 2, 10, 0)], [])
S["04_pool_not_average"] = ("9/10 + 20/40 havuzlanır (%58), yüzdelerin ortalaması (%70) değil.",
                            [rec("sayilar", 0, 9, 1), rec("sayilar", 0, 20, 20, test="t2")], [])
S["05_subject_prior"] = ("Ders havuzu ≥ 20 işaretli soru: konu önseli ders ortalamasına kayar (güç 4).",
                         spread("problemler", 30, 10) + [rec("olasilik", 0, 2, 3)], [])
S["06_prior_below_minimum"] = ("Ders havuzu < 20 işaretli: önsel Beta(1,1).",
                               [rec("problemler", 1, 8, 2), rec("olasilik", 0, 2, 3)], [])
S["07_decay_old_records"] = ("90 gün önceki kayıtlar 1/8 ağırlıkla girer, güven tazelikle düşer.",
                             [rec("ucgen", 90, 30, 10), rec("ucgen", 95, 30, 10)], [])
S["08_long_gap_freshness"] = ("Son kayıt 120 gün önce: tazelik çarpanı güveni çökertir.",
                              spread("dilbilgisi", 120, 40, days=(120, 125, 130, 140)), [])
S["09_repeat_half_weight"] = ("Tekrar çözüm yarım ağırlıkla girer.",
                              [rec("kumeler", 1, 5, 5), rec("kumeler", 1, 10, 0, repeat=True, test="r1")], [])
S["10_optimistic_many_blanks"] = ("Çok boş bırakan: işaretleme < 0,70, doğruluk 'iyimser'.",
                                  [rec("permutasyon", 1, 8, 1, 11), rec("permutasyon", 3, 7, 1, 12)], [])
S["11_overdispersion_clamped"] = ("Kayıtlar arası aşırı yayılım: φ 3'e kırpılır, güven düşer.",
                                  [rec("sayilar", d_, 10, 0) if k % 2 == 0 else rec("sayilar", d_, 0, 10)
                                   for k, d_ in enumerate((1, 2, 3, 4, 5, 6))], [])
S["12_single_day_not_reliable"] = ("Tek günde 200 soru: güven yüksek ama tek tarih 'makul'de kalır.",
                                   [rec("problemler", 0, 70, 30, test="a"), rec("problemler", 0, 70, 30, test="b")], [])
S["13_reliable_two_dates"] = ("İki tarihte 200 soru, taze: 'güvenilir'.",
                              [rec("problemler", 0, 70, 30), rec("problemler", 3, 70, 30)], [])
S["14_trend_declining_spec"] = ("Spesifikasyon örneği: önce 60/80, son 14/25 → P ≈ 0,96 düşüyor.",
                                spread("paragraf", 60, 20, days=(25, 35, 45, 55))
                                + spread("paragraf", 14, 11, days=(2, 9)), [])
S["15_trend_stable_spec"] = ("Spesifikasyon örneği: önce 60/80, son 17/25 → P ≈ 0,77 kararlı.",
                             spread("paragraf", 60, 20, days=(25, 35, 45, 55))
                             + spread("paragraf", 17, 8, days=(2, 9)), [])
S["16_trend_improving"] = ("Önce 40/80, son 36/45 → gelişiyor.",
                           spread("problemler", 40, 40, days=(30, 40, 50, 60))
                           + spread("problemler", 36, 9, days=(1, 6, 12)), [])
S["17_trend_too_few_records"] = ("3 kayıt: trend yetersiz veri.",
                                 [rec("ucgen", 1, 20, 5), rec("ucgen", 30, 20, 5), rec("ucgen", 40, 20, 5)], [])
S["18_trend_short_span"] = ("Kayıtlar 10 günlük aralıkta: yayılım < 14 gün.",
                            spread("ucgen", 60, 20, days=(1, 4, 7, 10)), [])
S["19_trend_one_recent_record"] = ("Son pencerede tek kayıt: trend yetersiz.",
                                   [rec("ucgen", 1, 20, 10)] + spread("ucgen", 60, 20, days=(25, 35, 45)), [])
S["20_invalid_records_skipped"] = ("Gelecek tarih, D+Y+B≠n ve bilinmeyen konu atlanır, sayılır.",
                                   [rec("sayilar", 0, 7, 3), rec("sayilar", -2, 5, 5, test="future"),
                                    {**rec("sayilar", 1, 4, 4, test="bad"), "total": 10},
                                    rec("yok-boyle-konu", 0, 5, 5)], [])
S["21_band_needs_two_records"] = ("Tek 40 soruluk kayıt: güven var ama zayıf/güçlü etiketi yok.",
                                  [rec("problemler", 0, 10, 30)], [])
S["22_priority_spec_example"] = ("M6 örneği: q yüksek ve ustalığı 0,55 olan konu, q düşük 0,35 olandan önce; 0,92 öncelik dışı.",
                                 spread("fonksiyon", 55, 45, days=(1, 5)) + spread("kumeler", 35, 65, days=(1, 5))
                                 + spread("olasilik", 92, 8, days=(1, 5)), [])
S["23_priority_per_subject_cap"] = ("Bir dersten en fazla 2 konu ilk 5'e girer.",
                                    spread("problemler", 20, 30, days=(1, 4)) + spread("sayilar", 20, 30, days=(1, 4))
                                    + spread("ucgen", 20, 30, days=(1, 4)) + spread("fonksiyon", 20, 30, days=(1, 4))
                                    + spread("dilbilgisi", 30, 20, days=(1, 4)), [])
S["24_priority_active_scope"] = ("AYT-FEN konusu TYT profilinde önceliğe girmez.",
                                 spread("elektrik", 10, 40, days=(1, 4)) + spread("sozcuk", 30, 20, days=(1, 4)), [])
S["25_paragraph_heavy_weight"] = ("Paragraf q=24: listenin başında ama ders sınırıyla tek başına doldurmaz.",
                                  spread("paragraf", 50, 50, days=(1, 4)) + spread("dilbilgisi", 40, 40, days=(1, 4))
                                  + spread("sozcuk", 30, 30, days=(1, 4)) + spread("osmanli", 10, 20, days=(1, 4)), [])
S["26_mock_insufficient"] = ("2 deneme: ders formu yetersiz veri.", [],
                             [mock(1, 3, sec("TYT-MAT", 40, 20, 10)), mock(2, 10, sec("TYT-MAT", 40, 22, 8))])
S["27_mock_form"] = ("4 TYT-MAT denemesi: form ve %80 aralıklı beklenen net.", [],
                     [mock(i, dd, sec("TYT-MAT", 40, c, w)) for i, (dd, c, w) in
                      enumerate([(2, 25, 8), (9, 22, 10), (16, 24, 6), (23, 20, 12)])])
S["28_mock_single_drop_not_trend"] = ("Son denemedeki tek düşüş trend sayılmaz (son 3 havuzu).", [],
                                      [mock(i, dd, sec("TYT-MAT", 40, c, w)) for i, (dd, c, w) in
                                       enumerate([(1, 12, 20), (8, 30, 5), (15, 29, 6), (22, 30, 6), (29, 31, 5)])])
S["29_mock_declining"] = ("Son 3 deneme belirgin düşük: düşüyor.", [],
                          [mock(i, dd, sec("TYT-MAT", 40, c, w)) for i, (dd, c, w) in
                           enumerate([(1, 15, 20), (8, 14, 21), (15, 16, 18), (22, 32, 5), (29, 31, 6), (36, 33, 4)])])
S["30_mock_block_total_and_subjects"] = ("Sosyal blok: toplam girişi yalnız köke, ders girişi hem derse hem köke.", [],
                                         [mock(1, 2, sec("TYT-SOS", 10, 6, 2)),
                                          mock(2, 9, sec("TYT-SOS.TAR", 5, 3, 1), sec("TYT-SOS.COG", 5, 4, 0)),
                                          mock(3, 16, sec("TYT-SOS.TAR", 5, 2, 2), sec("TYT-SOS.COG", 5, 3, 1)),
                                          mock(4, 23, sec("TYT-SOS", 10, 7, 1))])
S["31_mock_unsolved_section_absent"] = ("Çözülmeyen bölüm satırı yok; o bölümün formu oluşmaz.", [],
                                        [mock(1, 2, sec("TYT-MAT", 40, 25, 5)),
                                         mock(2, 9, sec("TYT-MAT", 40, 24, 6), sec("TYT-TUR", 40, 30, 6)),
                                         mock(3, 16, sec("TYT-MAT", 40, 26, 4))])
S["32_signal_partial_ignored"] = ("Kısmi etiketli bölüm M11c'ye girmez.", [],
                                  [mock(i, dd, sec("TYT-MAT", 40, 25, 10, full=False, marks=[("problemler", 5, 0)]))
                                   for i, dd in enumerate((2, 9, 16, 23))])
S["33_signal_full_tagged"] = ("Tam etiketli 4 denemede problemlerden sürekli kayıp: sinyal ve öncelik katkısı.",
                              spread("problemler", 45, 15, days=(1, 5)) + spread("sayilar", 40, 20, days=(1, 5)),
                              [mock(i, dd, sec("TYT-MAT", 40, 28, 8, full=True,
                                                marks=[("problemler", 6, 2), ("sayilar", 1, 1), ("ucgen", 1, 1)]))
                               for i, dd in enumerate((2, 9, 16, 23))])
S["34_signal_low_exposure"] = ("Beklenen maruziyet < 3: sinyal gösterilmez.", [],
                               [mock(1, 2, sec("TYT-MAT", 40, 30, 4, full=True, marks=[("kumeler", 2, 0)]))])
S["35_practice_gap_warning"] = ("Konu testlerinde yüksek, denemede düşük: pratik-deneme farkı uyarısı.",
                                spread("problemler", 85, 15, days=(1, 5)) + spread("sayilar", 85, 15, days=(1, 5))
                                + spread("ucgen", 80, 20, days=(1, 5)),
                                [mock(i, dd, sec("TYT-MAT", 40, 18, 14)) for i, dd in enumerate((2, 9, 16))])
S["36_practice_gap_needs_three_topics"] = ("Pratikte yalnız 2 yeterli konu: fark hesaplanmaz.",
                                           spread("problemler", 85, 15, days=(1, 5)) + spread("sayilar", 85, 15, days=(1, 5)),
                                           [mock(i, dd, sec("TYT-MAT", 40, 18, 14)) for i, dd in enumerate((2, 9, 16))])
S["37_future_mock_skipped"] = ("Gelecek tarihli deneme atlanır, geçersiz sayılır.", [],
                               [mock(1, -1, sec("TYT-MAT", 40, 30, 4)), mock(2, 3, sec("TYT-MAT", 40, 20, 4))])
S["38_simpson_subject_weighting"] = ("Hacim kolay konuya kayınca ders ustalığı hacimle değil q ile ağırlıklanır.",
                                     spread("kumeler", 190, 10, days=(1, 3, 5, 7)) + spread("problemler", 20, 20, days=(1, 3)),
                                     [])


def main():
    SCENARIOS.mkdir(parents=True, exist_ok=True)
    for name, (desc, records, mocks) in S.items():
        path = SCENARIOS / f"{name}.json"
        old = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
        scenario = {
            "description": desc,
            "input": {"today": TODAY.isoformat(), "activeTests": TYT, "catalog": CATALOG,
                      "topicRecords": records, "mockExams": mocks},
            "expected": old.get("expected"),
        }
        path.write_text(json.dumps(scenario, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(S)} scenarios")


if __name__ == "__main__":
    main()
