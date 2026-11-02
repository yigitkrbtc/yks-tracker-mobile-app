"""Etiket CSV'lerini birleştirir, soru sayılarını doğrular, konu ağırlıklarını (q_i) test bazında hesaplar.

Kullanım:
  python3 aggregate.py [çıktı_klasörü]           # ham etiketlerden (BASE/tags, BASE/taxonomy; repoda değil)
  python3 aggregate.py --repo [çıktı_klasörü]    # repodaki tum_etiketler.csv + konu_taksonomisi.csv'den
"""
import csv
import glob
import json
import os
import sys
from collections import defaultdict

ARGS = [a for a in sys.argv[1:] if a != "--repo"]
FROM_REPO = "--repo" in sys.argv[1:]
SEED = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = "/home/claude/osym"
OUT = ARGS[0] if ARGS else (SEED if FROM_REPO else f"{BASE}/out")
YEARS = list(range(2019, 2027))
RECENT = [y for y in YEARS if y >= 2022]
RESTRICTED_YEARS = {2020, 2023}

# (oturum, test grubu) -> yıllık beklenen soru sayısı; geometri ilgili matematik testine dahil
EXPECTED = {
    ("TYT", "TUR"): 40, ("TYT", "MAT"): 40, ("TYT", "FIZ"): 7, ("TYT", "KIM"): 7,
    ("TYT", "BIY"): 6, ("TYT", "TAR"): 5, ("TYT", "COG"): 5, ("TYT", "FEL"): 5, ("TYT", "DKAB"): 5,
    ("AYT", "EDB"): 24, ("AYT", "MAT"): 40, ("AYT", "FIZ"): 14, ("AYT", "KIM"): 13,
    ("AYT", "BIY"): 13, ("AYT", "TAR"): 21, ("AYT", "COG"): 17, ("AYT", "FEL"): 12, ("AYT", "DKAB"): 6,
}


# AYT tarih ve coğrafya iki testte sorulur (ADR-0019): q test bazında, test grubu toplamına normalize edilir
TEST_EXPECTED = {
    ("AYT", "TAR", "AYT-TDE-SB1"): 10, ("AYT", "TAR", "AYT-SB2"): 11,
    ("AYT", "COG", "AYT-TDE-SB1"): 6, ("AYT", "COG", "AYT-SB2"): 11,
}

# kitapçıktaki test adı -> exam_structure.json test kodu
TEST_CODES = {
    ("TYT", "Türkçe"): "TYT-TUR", ("TYT", "Sosyal Bilimler"): "TYT-SOS",
    ("TYT", "Temel Matematik"): "TYT-MAT", ("TYT", "Fen Bilimleri"): "TYT-FEN",
    ("AYT", "TDE-SB1"): "AYT-TDE-SB1", ("AYT", "SB2"): "AYT-SB2",
    ("AYT", "Matematik"): "AYT-MAT", ("AYT", "Fen Bilimleri"): "AYT-FEN",
}


def group_of(unit):
    return "MAT" if unit in ("TMAT", "AMAT", "GEO") else unit


def tier(mean_q, years_present, n_years):
    if years_present < n_years / 2:
        return "nadir"
    if mean_q >= 2.0:
        return "çok"
    if mean_q >= 0.8:
        return "orta"
    return "az"


def main():
    os.makedirs(OUT, exist_ok=True)
    rows, taxonomy = [], {}
    tag_files = [f"{SEED}/tum_etiketler.csv"] if FROM_REPO else sorted(glob.glob(f"{BASE}/tags/*.csv"))
    tax_files = [f"{SEED}/konu_taksonomisi.csv"] if FROM_REPO else sorted(glob.glob(f"{BASE}/taxonomy/*.csv"))
    for path in tag_files:
        with open(path, encoding="utf-8") as f:
            for r in csv.DictReader(f):
                r["_file"] = os.path.basename(path)
                rows.append(r)
    for path in tax_files:
        with open(path, encoding="utf-8") as f:
            for r in csv.DictReader(f):
                taxonomy[r["topic_code"].strip()] = r
    with open(f"{SEED}/exam_structure.json", encoding="utf-8") as f:
        unit_tests = {u["code"]: u["tests"] for u in json.load(f)["units"]}

    alt = [r for r in rows if "ilave" in r.get("note", "").lower() or "alternatif" in r.get("note", "").lower()]
    core = [r for r in rows if r not in alt]

    # doğrulama
    counts = defaultdict(int)
    seen = defaultdict(int)
    for r in core:
        key = (int(r["year"]), r["session"].strip(), group_of(r["unit"].strip()))
        counts[key] += 1
        seen[(key, r["test"].strip(), r["qno"].strip())] += 1
    problems = []
    for (sess, grp), exp in EXPECTED.items():
        for y in YEARS:
            got = counts.get((y, sess, grp), 0)
            if got != exp:
                problems.append(f"{y} {sess} {grp}: {got} / beklenen {exp}")
    dups = [k for k, v in seen.items() if v > 1]
    missing_codes = sorted({r["topic_code"].strip() for r in core} - set(taxonomy))

    # q_i: (konu, test) başına; tek testli dersin konusunda tek satır
    per = defaultdict(lambda: defaultdict(int))
    meta = {}
    for r in core:
        code = r["topic_code"].strip()
        sess = r["session"].strip()
        test = TEST_CODES[(sess, r["test"].strip())]
        per[(code, test)][int(r["year"])] += 1
        meta.setdefault(code, (sess, r["unit"].strip(), r["topic"].strip()))
    for code, t in taxonomy.items():  # hiç çıkmamış konular da listede kalsın
        meta.setdefault(code, (t["session"].strip(), t["unit"].strip(), t["topic"].strip()))

    out_rows = []
    for code, (sess, unit, topic) in meta.items():
        grade = taxonomy.get(code, {}).get("grade", "")
        for test in unit_tests[f"{sess}.{unit}"]:
            counts_by_year = per[(code, test)]
            by_year = [counts_by_year.get(y, 0) for y in YEARS]
            # 2020 ve 2023'te kapsam daraltıldı (12. sınıf konuları çıkmadı); bu konularda o yıllar ortalamaya girmez
            # resmî kapsam listesi elimizde yok: 12. sınıf konusu o yıl hiç çıkmadıysa kapsam dışı sayılır (küçük yukarı yanlılık)
            skip = {y for y in RESTRICTED_YEARS if "12" in grade and counts_by_year.get(y, 0) == 0}
            all_y = [y for y in YEARS if y not in skip]
            rec_y = [y for y in RECENT if y not in skip]
            m8 = sum(counts_by_year.get(y, 0) for y in all_y) / len(all_y)
            m5 = sum(counts_by_year.get(y, 0) for y in rec_y) / len(rec_y)
            present = sum(1 for y in all_y if counts_by_year.get(y, 0) > 0)
            out_rows.append({
                "session": sess, "unit": unit, "topic_code": code, "test_code": test, "topic": topic,
                **{str(y): v for y, v in zip(YEARS, by_year)},
                "mean_2019_2026": round(m8, 2), "mean_2022_2026": round(m5, 2),
                "years_present": f"{present}/{len(all_y)}", "tier": tier(m5, present, len(all_y)),
                "grade": grade,
            })

    def expected_of(r):
        return TEST_EXPECTED.get((r["session"], r["unit"], r["test_code"])) or EXPECTED.get((r["session"], group_of(r["unit"])))

    def norm_key(r):
        split = (r["session"], r["unit"], r["test_code"]) in TEST_EXPECTED
        return (r["session"], group_of(r["unit"]), r["test_code"] if split else "")

    # yanlılık düzeltmesi sonrası toplam beklenen soru sayısını aşabilir: q_i test grubu toplamına normalize edilir
    totals = defaultdict(float)
    for r in out_rows:
        totals[norm_key(r)] += r["mean_2022_2026"]
    for r in out_rows:
        exp, tot = expected_of(r), totals[norm_key(r)]
        r["q_i"] = round(r["mean_2022_2026"] * exp / tot, 2) if exp and tot else r["mean_2022_2026"]
        r["tier"] = tier(r["q_i"], int(r["years_present"].split("/")[0]), int(r["years_present"].split("/")[1]))
    out_rows.sort(key=lambda r: (r["session"], r["unit"], r["test_code"], -r["q_i"], r["topic_code"]))

    fields = ["session", "unit", "topic_code", "test_code", "topic"] + [str(y) for y in YEARS] + [
        "mean_2019_2026", "mean_2022_2026", "q_i", "years_present", "tier", "grade"]
    with open(f"{OUT}/konu_agirliklari.csv", "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(out_rows)

    if not FROM_REPO:  # repoda tum_etiketler.csv kaynağın kendisi; yeniden yazılmaz
        with open(f"{OUT}/tum_etiketler.csv", "w", encoding="utf-8", newline="") as f:
            keys = ["year", "session", "test", "unit", "qno", "topic_code", "topic", "subtopic",
                    "secondary_topic_code", "confidence", "note"]
            w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
            w.writeheader()
            w.writerows(sorted(rows, key=lambda r: (r["year"], r["session"], r["test"], int(r["qno"]) if r["qno"].isdigit() else 0)))

    low = sum(1 for r in core if r.get("confidence", "").strip() == "low")
    print(f"satır: {len(rows)} (çekirdek {len(core)}, ilave {len(alt)}), konu: {len(meta)}, düşük güven: {low}")
    print("sayım sorunları:", problems or "yok")
    print("tekrarlı soru:", dups[:20] or "yok")
    print("taksonomide olmayan kod:", missing_codes or "yok")


if __name__ == "__main__":
    main()
