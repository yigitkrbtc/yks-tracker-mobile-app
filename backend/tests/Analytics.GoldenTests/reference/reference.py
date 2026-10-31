#!/usr/bin/env python3
"""Independent reference implementation of the Nirengi analysis spec (rule set 2026.1).

Written from the metric specification (M0-M3, M6, M7 lite, M11), not from the C# engine, using
scipy for every distribution. It fills the "expected" block of each golden scenario:

    pip install scipy
    python3 backend/tests/Analytics.GoldenTests/reference/reference.py            # rewrite all
    python3 backend/tests/Analytics.GoldenTests/reference/reference.py --check    # fail on drift

CI does not run this script; it compares the C# engine with the committed "expected" values.
"""
import argparse
import json
import math
import sys
from datetime import date
from pathlib import Path

from scipy import integrate, stats

SCENARIOS = Path(__file__).resolve().parent.parent / "Scenarios"

RULES = {
    "version": "2026.1",
    "half_life_days": 30.0,
    "repeat_weight": 0.5,
    "interval": 0.90,
    "mock_interval": 0.80,
    "prior_strength": 4.0,
    "prior_min_marked": 20,
    "prior_mean_clamp": (0.125, 0.875),
    "confidence_k": 20.0,
    "freshness_half_life_days": 40.0,
    "phi_clamp": (1.0, 3.0),
    "confidence_bands": (0.35, 0.60, 0.80),
    "optimistic_marking": 0.70,
    "band_min_questions": 20,
    "band_min_records": 2,
    "band_weak": 0.50,
    "band_strong": 0.80,
    "trend_recent_days": 21,
    "trend_previous_days": 60,
    "trend_min_records": 4,
    "trend_min_questions": 60,
    "trend_min_span_days": 14,
    "trend_min_recent_questions": 15,
    "trend_min_recent_records": 2,
    "trend_min_previous_records": 2,
    "trend_up_probability": 0.85,
    "trend_down_probability": 0.90,
    "trend_min_difference": 0.05,
    "target_mastery": 0.80,
    "lambda_review": 0.0,
    "mu_mock": 0.5,
    "priority_top": 5,
    "priority_per_subject": 2,
    "priority_low_data": 2,
    "aggregate_z": 1.6448536269514722,
    "coverage_confidence": 0.5,
    "mock_min_exams": 3,
    "mock_trend_recent": 3,
    "mock_trend_previous": 6,
    "mock_trend_min_previous": 2,
    "mock_trend_min_agreeing": 2,
    "gap_threshold": 0.15,
    "gap_min_topics": 3,
    "signal_lookback_exams": 10,
    "signal_min_exposure": 3.0,
}


def d(s):
    return date.fromisoformat(s)


def beta_summary(a, b, level):
    dist = stats.beta(a, b)
    tail = (1 - level) / 2
    return {"mean": a / (a + b), "low": float(dist.ppf(tail)), "high": float(dist.ppf(1 - tail))}


def beta_var(a, b):
    return a * b / ((a + b) ** 2 * (a + b + 1))


def p_greater(a1, b1, a2, b2):
    """P(X > Y), X ~ Beta(a1, b1), Y ~ Beta(a2, b2)."""
    x, y = stats.beta(a1, b1), stats.beta(a2, b2)
    value, _ = integrate.quad(lambda t: x.pdf(t) * y.cdf(t), 0, 1, limit=200, epsabs=1e-12)
    return value


def trend_label(p_up, diff):
    if p_up >= RULES["trend_up_probability"] and diff >= RULES["trend_min_difference"]:
        return "improving"
    if 1 - p_up >= RULES["trend_down_probability"] and -diff >= RULES["trend_min_difference"]:
        return "declining"
    return "stable"


def decay(age):
    return 0.5 ** (age / RULES["half_life_days"])


def valid_record(r, today):
    counts = (r["total"], r["correct"], r["wrong"], r["blank"])
    return (
        r["total"] > 0
        and min(counts) >= 0
        and r["correct"] + r["wrong"] + r["blank"] == r["total"]
        and d(r["date"]) <= today
    )


def analyze(inp):
    today = d(inp["today"])
    cat = inp["catalog"]
    topics = {t["id"]: t for t in cat["topics"]}
    order = {t["id"]: k for k, t in enumerate(cat["topics"])}  # catalog order breaks ties
    subjects = {s["code"]: s for s in cat["subjects"]}
    sections = {s["code"]: s for s in cat["sections"]}
    topics_by_subject = {}
    for t in cat["topics"]:
        topics_by_subject.setdefault(t["subject"], []).append(t["id"])

    invalid = 0
    recs = {}
    for r in inp.get("topicRecords", []):
        if r["topic"] not in topics or not valid_record(r, today):
            invalid += 1
            continue
        recs.setdefault(r["topic"], []).append(r)

    # Effective (decayed, repeat-weighted) counts per topic.
    eff = {}
    for tid, rs in recs.items():
        D = Y = B = 0.0
        for r in rs:
            w = decay((today - d(r["date"])).days) * (RULES["repeat_weight"] if r.get("repeat") else 1.0)
            D += w * r["correct"]
            Y += w * r["wrong"]
            B += w * r["blank"]
        eff[tid] = (D, Y, B)

    def prior(tid):
        subj = topics[tid]["subject"]
        d_sum = y_sum = 0.0
        raw = 0
        for other in topics_by_subject[subj]:
            if other == tid or other not in recs:
                continue
            d_sum += eff[other][0]
            y_sum += eff[other][1]
            raw += sum(r["correct"] + r["wrong"] for r in recs[other])
        if raw < RULES["prior_min_marked"]:
            return 1.0, 1.0
        lo, hi = RULES["prior_mean_clamp"]
        m = min(max(d_sum / (d_sum + y_sum), lo), hi)
        return RULES["prior_strength"] * m, RULES["prior_strength"] * (1 - m)

    out_topics = {}
    posterior = {}
    for tid in topics:
        a0, b0 = prior(tid)
        if tid not in recs:
            posterior[tid] = (a0, b0)
            out_topics[tid] = {"sufficiency": "none", "band": "none", "trend": "none"}
            continue
        rs = recs[tid]
        D, Y, B = eff[tid]
        a, b = a0 + D, b0 + Y
        posterior[tid] = (a, b)
        acc = beta_summary(a, b, RULES["interval"])
        mark = (1 + D + Y) / (2 + D + Y + B)
        n_eff = D + Y + B

        marked = [r for r in rs if r["correct"] + r["wrong"] > 0]
        phi = 1.0
        if len(marked) >= 2:
            sd_ = sum(r["correct"] for r in marked)
            sm = sum(r["correct"] + r["wrong"] for r in marked)
            p = sd_ / sm
            if 0 < p < 1:
                chi = sum((r["correct"] - (r["correct"] + r["wrong"]) * p) ** 2
                          / ((r["correct"] + r["wrong"]) * p * (1 - p)) for r in marked)
                phi = chi / (len(marked) - 1)
        phi = min(max(phi, RULES["phi_clamp"][0]), RULES["phi_clamp"][1])
        last = max(d(r["date"]) for r in rs)
        n_adj = n_eff / phi
        conf = n_adj / (n_adj + RULES["confidence_k"]) * 0.5 ** ((today - last).days / RULES["freshness_half_life_days"])
        dates = {r["date"] for r in rs}
        c1, c2, c3 = RULES["confidence_bands"]
        if conf < c1:
            suff = "veryLow"
        elif conf < c2:
            suff = "preliminary"
        elif conf < c3 or len(dates) < 2:
            suff = "reasonable"
        else:
            suff = "reliable"

        raw_q = sum(r["total"] for r in rs)
        if raw_q < RULES["band_min_questions"] or len(rs) < RULES["band_min_records"]:
            band = "insufficient"
        elif acc["mean"] < RULES["band_weak"]:
            band = "weak"
        elif acc["mean"] < RULES["band_strong"]:
            band = "developing"
        else:
            band = "strong"

        # Trend (M3): raw counts (repeat weighted, not decayed) in two windows.
        recent = [r for r in rs if (today - d(r["date"])).days < RULES["trend_recent_days"]]
        previous = [r for r in rs if RULES["trend_recent_days"] <= (today - d(r["date"])).days
                    < RULES["trend_recent_days"] + RULES["trend_previous_days"]]
        window = recent + previous
        trend = {"label": "insufficient"}
        if window:
            span = (max(d(r["date"]) for r in window) - min(d(r["date"]) for r in window)).days
            if not (len(window) < RULES["trend_min_records"]
                    or sum(r["total"] for r in window) < RULES["trend_min_questions"]
                    or span < RULES["trend_min_span_days"]
                    or sum(r["total"] for r in recent) < RULES["trend_min_recent_questions"]
                    or len(recent) < RULES["trend_min_recent_records"]
                    or len(previous) < RULES["trend_min_previous_records"]):
                def counts(rows):
                    dd = yy = 0.0
                    for r in rows:
                        w = RULES["repeat_weight"] if r.get("repeat") else 1.0
                        dd += w * r["correct"]
                        yy += w * r["wrong"]
                    return dd, yy
                rd, ry = counts(recent)
                pd, py = counts(previous)
                p_up = p_greater(1 + rd, 1 + ry, 1 + pd, 1 + py)
                diff = (1 + rd) / (2 + rd + ry) - (1 + pd) / (2 + pd + py)
                trend = {"label": trend_label(p_up, diff), "probabilityUp": p_up, "difference": diff}

        out_topics[tid] = {
            "mastery": acc["mean"], "masteryLow": acc["low"], "masteryHigh": acc["high"],
            "markingRate": mark, "optimistic": mark < RULES["optimistic_marking"],
            "expectedNetPerQuestion": mark * (1.25 * acc["mean"] - 0.25),
            "effectiveQuestions": n_eff, "dispersion": phi, "confidence": conf,
            "sufficiency": suff, "band": band, "trend": trend["label"],
            "trendProbabilityUp": trend.get("probabilityUp"), "trendDifference": trend.get("difference"),
            "records": len(rs), "questions": raw_q,
            "correct": sum(r["correct"] for r in rs), "wrong": sum(r["wrong"] for r in rs),
            "blank": sum(r["blank"] for r in rs),
        }

    # Mock exams (M11a): one entry per key (root test code always, subject line when entered).
    mock_invalid = 0
    entries = {}
    exams = sorted(inp.get("mockExams", []), key=lambda m: m["date"])  # stable: input order breaks ties
    valid_exams = []
    for m in exams:
        if d(m["date"]) > today:
            mock_invalid += 1
            continue
        rows = []
        for s in m["sections"]:
            sec = sections.get(s["code"])
            n = s["questionCount"]
            if sec is None or n <= 0 or s["correct"] < 0 or s["wrong"] < 0 or s["correct"] + s["wrong"] > n:
                mock_invalid += 1
                continue
            rows.append((sec, s))
        if not rows:
            continue
        valid_exams.append((m, rows))
        roots = {}
        for sec, s in rows:
            if sec["parent"] is None:
                roots[sec["code"]] = (s["questionCount"], s["correct"], s["wrong"])
        for sec, s in rows:
            if sec["parent"] is not None:
                entries.setdefault(sec["code"], []).append((m, s["questionCount"], s["correct"], s["wrong"]))
                if sec["parent"] not in {x["code"] for x, _ in rows}:
                    n0, c0, w0 = roots.get(("sum", sec["parent"]), (0, 0, 0))
                    roots[("sum", sec["parent"])] = (n0 + s["questionCount"], c0 + s["correct"], w0 + s["wrong"])
        for key, (n, c, w) in roots.items():
            code = key[1] if isinstance(key, tuple) else key
            entries.setdefault(code, []).append((m, n, c, w))

    out_sections = {}
    for code, es in entries.items():
        sec = sections[code]
        D = Y = B = 0.0
        for m, n, c, w in es:
            wt = decay((today - d(m["date"])).days)
            D += wt * c
            Y += wt * w
            B += wt * (n - c - w)
        enough = len(es) >= RULES["mock_min_exams"]
        res = {"exams": len(es), "sufficiency": "sufficient" if enough else "insufficient"}
        acc = beta_summary(1 + D, 1 + Y, RULES["mock_interval"])
        mark = (1 + D + Y) / (2 + D + Y + B)
        nq = sec["questionCount"]
        res.update({
            "accuracy": acc["mean"], "accuracyLow": acc["low"], "accuracyHigh": acc["high"],
            "markingRate": mark,
            "expectedNet": nq * mark * (1.25 * acc["mean"] - 0.25),
            "expectedNetLow": nq * mark * (1.25 * acc["low"] - 0.25),
            "expectedNetHigh": nq * mark * (1.25 * acc["high"] - 0.25),
        })
        res["trend"] = "insufficient"
        recent = es[-RULES["mock_trend_recent"]:]
        previous = es[:-RULES["mock_trend_recent"]][-RULES["mock_trend_previous"]:]
        if len(recent) == RULES["mock_trend_recent"] and len(previous) >= RULES["mock_trend_min_previous"]:
            rd, ry = sum(e[2] for e in recent), sum(e[3] for e in recent)
            pd, py = sum(e[2] for e in previous), sum(e[3] for e in previous)
            p_up = p_greater(1 + rd, 1 + ry, 1 + pd, 1 + py)
            prev_mean = (1 + pd) / (2 + pd + py)
            diff = (1 + rd) / (2 + rd + ry) - prev_mean
            label = trend_label(p_up, diff)
            # A single outlier exam must not make a trend: at least two of the last three must each sit
            # min_difference beyond the earlier pool in the same direction.
            def acc_of(e):
                return e[2] / (e[2] + e[3]) if e[2] + e[3] > 0 else 0.0
            gap = RULES["trend_min_difference"]
            below = sum(1 for e in recent if acc_of(e) <= prev_mean - gap)
            above = sum(1 for e in recent if acc_of(e) >= prev_mean + gap)
            if label == "declining" and below < RULES["mock_trend_min_agreeing"]:
                label = "stable"
            if label == "improving" and above < RULES["mock_trend_min_agreeing"]:
                label = "stable"
            res["trend"] = label
            res["trendProbabilityUp"] = p_up
        out_sections[code] = res

    def eligible(tid):
        t = out_topics[tid]
        return t["sufficiency"] not in ("none", "veryLow") and t["band"] not in ("none", "insufficient")

    # M11c: wrong/blank signal from fully tagged sections of the last N exams with one.
    tagged = [(m, rows) for m, rows in valid_exams if any(s.get("full") for _, s in rows)]
    tagged = tagged[-RULES["signal_lookback_exams"]:]
    exposure, missed, wrongs, blanks, covering = {}, {}, {}, {}, {}
    for m, rows in tagged:
        for sec, s in rows:
            if not s.get("full"):
                continue
            subj_topics = [t for sc in sec["subjects"] for t in topics_by_subject.get(sc, [])]
            q_sum = sum(topics[t]["q"] for t in subj_topics)
            if q_sum <= 0:
                continue
            for t in subj_topics:
                exposure[t] = exposure.get(t, 0.0) + topics[t]["q"] * s["questionCount"] / q_sum
                covering.setdefault(t, set()).add(m["id"])
            for mk in s.get("marks", []):
                if mk["topic"] in topics:
                    missed[mk["topic"]] = missed.get(mk["topic"], 0) + mk["wrong"] + mk["blank"]
                    wrongs[mk["topic"]] = wrongs.get(mk["topic"], 0) + mk["wrong"]
                    blanks[mk["topic"]] = blanks.get(mk["topic"], 0) + mk["blank"]
    subject_rate = {}
    for sc, ts in topics_by_subject.items():
        e = sum(exposure.get(t, 0.0) for t in ts)
        if e > 0:
            mm = sum(missed.get(t, 0) for t in ts)
            subject_rate[sc] = (1 + mm) / (2 + mm + max(e - mm, 0.0))
    signals = {}
    for t, e in exposure.items():
        mm = missed.get(t, 0)
        rate = (1 + mm) / (2 + mm + max(e - mm, 0.0))
        shown = e >= RULES["signal_min_exposure"]
        excess = max(0.0, rate - subject_rate[topics[t]["subject"]])
        signals[t] = {"exposure": e, "missed": mm, "wrong": wrongs.get(t, 0), "blank": blanks.get(t, 0),
                      "exams": len(covering[t]), "rate": rate, "subjectRate": subject_rate[topics[t]["subject"]],
                      "shown": shown}
        out_topics[t]["mockSignal"] = signals[t]

    # M6 priority within the active scope.
    active_subjects = {s for s, v in subjects.items() if set(v["tests"]) & set(inp["activeTests"])}
    for tid, t in out_topics.items():
        if not eligible(tid):
            t["priority"] = None
            continue
        m = t["mastery"]
        if m < 0.5:
            pg = 0.6
        elif m > 0.9:
            pg = 0.3
        else:
            pg = 1.0
        q = topics[tid]["q"]
        value = q * max(0.0, RULES["target_mastery"] - m) * pg
        sig = signals.get(tid)
        if sig and sig["shown"]:
            value += RULES["mu_mock"] * q * max(0.0, sig["rate"] - sig["subjectRate"])
        t["priority"] = value

    active = [tid for tid in topics if topics[tid]["subject"] in active_subjects]
    ranked = sorted((tid for tid in active if out_topics[tid]["priority"] and out_topics[tid]["priority"] > 0),
                    key=lambda tid: (-out_topics[tid]["priority"], -topics[tid]["q"], order[tid]))
    top, per_subject = [], {}
    for tid in ranked:
        sc = topics[tid]["subject"]
        if per_subject.get(sc, 0) >= RULES["priority_per_subject"]:
            continue
        top.append(tid)
        per_subject[sc] = per_subject.get(sc, 0) + 1
        if len(top) == RULES["priority_top"]:
            break
    low = sorted((tid for tid in active if not eligible(tid) and topics[tid]["q"] > 0),
                 key=lambda tid: (-topics[tid]["q"], order[tid]))[:RULES["priority_low_data"]]

    # M7 lite aggregates.
    def aggregate(tids):
        q_sum = sum(topics[t]["q"] for t in tids)
        if q_sum <= 0:
            return None
        mean = var = cov = 0.0
        for t in tids:
            w = topics[t]["q"] / q_sum
            a, b = posterior[t]
            mean += w * a / (a + b)
            var += w * w * beta_var(a, b)
            c = out_topics[t].get("confidence")
            if c is not None and c > RULES["coverage_confidence"]:
                cov += w
        sd = math.sqrt(var)
        z = RULES["aggregate_z"]
        return {"mastery": mean, "masteryLow": max(0.0, mean - z * sd), "masteryHigh": min(1.0, mean + z * sd),
                "coverage": cov, "studiedTopics": sum(1 for t in tids if t in recs), "topics": len(tids)}

    out_subjects = {}
    for sc in subjects:
        agg = aggregate(topics_by_subject.get(sc, []))
        if agg:
            out_subjects[sc] = agg
    tests = sorted({tc for s in subjects.values() for tc in s["tests"]})
    out_tests = {}
    for tc in tests:
        tids = [t for sc, s in subjects.items() if tc in s["tests"] for t in topics_by_subject.get(sc, [])]
        agg = aggregate(tids)
        if agg:
            out_tests[tc] = agg

    # M11b practice-mock gap.
    for code, res in out_sections.items():
        sec = sections[code]
        tids = [t for sc in sec["subjects"] for t in topics_by_subject.get(sc, []) if eligible(t)]
        if len(tids) < RULES["gap_min_topics"] or res["sufficiency"] != "sufficient":
            res["practiceGap"] = None
            res["gapWarning"] = False
            continue
        q_sum = sum(topics[t]["q"] for t in tids)
        if q_sum <= 0:
            res["practiceGap"] = None
            res["gapWarning"] = False
            continue
        practice = sum(topics[t]["q"] * out_topics[t]["mastery"] for t in tids) / q_sum
        res["practiceGap"] = practice - res["accuracy"]
        res["gapWarning"] = res["practiceGap"] > RULES["gap_threshold"]

    return {
        "ruleSetVersion": RULES["version"],
        "invalidRecords": invalid + mock_invalid,
        "topics": out_topics,
        "subjects": out_subjects,
        "tests": out_tests,
        "sections": out_sections,
        "priorities": top,
        "lowDataTopics": low,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    drift = []
    for path in sorted(SCENARIOS.glob("*.json")):
        scenario = json.loads(path.read_text(encoding="utf-8"))
        expected = analyze(scenario["input"])
        if args.check:
            if json.dumps(expected, sort_keys=True) != json.dumps(scenario.get("expected"), sort_keys=True):
                drift.append(path.name)
            continue
        scenario["expected"] = expected
        path.write_text(json.dumps(scenario, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"wrote {path.name}")
    if drift:
        print("stale expected values: " + ", ".join(drift))
        sys.exit(1)


if __name__ == "__main__":
    main()
