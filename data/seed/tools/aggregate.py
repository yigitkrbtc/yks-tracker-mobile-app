"""Etiket CSV'lerini birleştirir, soru sayılarını doğrular, konu ağırlıklarını (q_i) hesaplar."""
import csv
import glob
import os
import sys
from collections import defaultdict

BASE = "/home/claude/osym"
OUT = sys.argv[1] if len(sys.argv) > 1 else f"{BASE}/out"
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
    for path in sorted(glob.glob(f"{BASE}/tags/*.csv")):
        with open(path, encoding="utf-8") as f:
            for r in csv.DictReader(f):
                r["_file"] = os.path.basename(path)
                rows.append(r)
    for path in sorted(glob.glob(f"{BASE}/taxonomy/*.csv")):
        with open(path, encoding="utf-8") as f:
            for r in csv.DictReader(f):
                taxonomy[r["topic_code"].strip()] = r

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

    # q_i
    per = defaultdict(lambda: defaultdict(int))
    meta = {}
    for r in core:
        code = r["topic_code"].strip()
        per[code][int(r["year"])] += 1
        meta.setdefault(code, (r["session"].strip(), r["unit"].strip(), r["topic"].strip()))
    for code, t in taxonomy.items():  # hiç çıkmamış konular da listede kalsın
        meta.setdefault(code, (t["session"].strip(), t["unit"].strip(), t["topic"].strip()))

    out_rows = []
    for code, (sess, unit, topic) in meta.items():
        by_year = [per[code].get(y, 0) for y in YEARS]
        # 2020 ve 2023'te kapsam daraltıldı (12. sınıf konuları çıkmadı); bu konularda o yıllar ortalamaya girmez
        grade = taxonomy.get(code, {}).get("grade", "")
        # resmî kapsam listesi elimizde yok: 12. sınıf konusu o yıl hiç çıkmadıysa kapsam dışı sayılır (küçük yukarı yanlılık)
        skip = {y for y in RESTRICTED_YEARS if "12" in grade and per[code].get(y, 0) == 0}
        all_y = [y for y in YEARS if y not in skip]
        rec_y = [y for y in RECENT if y not in skip]
        m8 = sum(per[code].get(y, 0) for y in all_y) / len(all_y)
        m5 = sum(per[code].get(y, 0) for y in rec_y) / len(rec_y)
        present = sum(1 for y in all_y if per[code].get(y, 0) > 0)
        out_rows.append({
            "session": sess, "unit": unit, "topic_code": code, "topic": topic,
            **{str(y): v for y, v in zip(YEARS, by_year)},
            "mean_2019_2026": round(m8, 2), "mean_2022_2026": round(m5, 2),
            "years_present": f"{present}/{len(all_y)}", "tier": tier(m5, present, len(all_y)),
            "grade": grade,
        })
    # yanlılık düzeltmesi sonrası toplam beklenen soru sayısını aşabilir: q_i test grubu toplamına normalize edilir
    totals = defaultdict(float)
    for r in out_rows:
        totals[(r["session"], group_of(r["unit"]))] += r["mean_2022_2026"]
    for r in out_rows:
        key = (r["session"], group_of(r["unit"]))
        exp, tot = EXPECTED.get(key), totals[key]
        r["q_i"] = round(r["mean_2022_2026"] * exp / tot, 2) if exp and tot else r["mean_2022_2026"]
        r["tier"] = tier(r["q_i"], int(r["years_present"].split("/")[0]), int(r["years_present"].split("/")[1]))
    out_rows.sort(key=lambda r: (r["session"], r["unit"], -r["q_i"], r["topic_code"]))

    fields = ["session", "unit", "topic_code", "topic"] + [str(y) for y in YEARS] + [
        "mean_2019_2026", "mean_2022_2026", "q_i", "years_present", "tier", "grade"]
    with open(f"{OUT}/konu_agirliklari.csv", "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(out_rows)

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
