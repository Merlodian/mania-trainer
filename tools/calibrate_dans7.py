"""Rate the 7K dan charts with xdc7.dll and score how well the ratings follow the dans.

Every 7K .osu under the dan folders is read; its dan comes from the Version line
("~ 9th ~ ...", "- Gamma ~ ...") and its kind (Jack, Tech, Speed, Stream) from the folder
or file name. Charts with under 50 notes (the packs' "Delete Upon download" placeholders)
are skipped.

Scores, all on the ratings at rate 1.0:
  dan error    one smooth rising curve (a cubic in the dan number) is fitted through all
               the charts; a chart's error is how far its rating sits from the curve,
               divided by the curve's slope there, so it reads in dans and cannot be
               improved by squeezing or stretching the rating scale. The four kinds of a
               dan are meant to be equally hard, so a perfect calculator scores 0.
  wrong order  of every two charts from different dans, the share rated in the wrong
               order (a tie counts half).
  kind bias    each kind's average error, in dans: how far that kind rates high or low.

usage:
    python -I calibrate_dans7.py --dans "<7k Individual Maps>" [--dans ...] --dll xdc7.dll
                                 [--set name=value ...] [--out <dir>]

--set changes a setting of the engine by name (see xdc7_param_name) for this run only.
--out writes dan7_calibration.csv (one row per chart) and dan7_curve.json.
"""

import argparse
import concurrent.futures
import csv
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xdc7_lib as L  # noqa: E402

DAN_RE = re.compile(r"^\s*[-~]\s*(0th|1st|2nd|3rd|[4-9]th|10th|Gamma|Azimuth|Zenith|Stellium)\s*~", re.I)
KINDS = ["Jack", "Speed", "Stream", "Tech"]
CURVE_DEGREE = 3


def kind_of(path):
    p = path.lower()
    for key, kind in (("jack practice", "Jack"), ("tech practice", "Tech"), ("speed practice", "Speed"),
                      ("stream practice", "Stream"), ("stamina practice", "Stream")):
        if key in os.path.basename(os.path.dirname(p)) or key in os.path.basename(p):
            return kind
    return "Course"


def load_dan_charts(roots):
    """every 7K dan chart under the folders: dicts with path, data, dan (0..14), kind."""
    charts, skipped = [], []
    for top in roots:
        for p in L.find_osu(top):
            data = L.read_bytes(p)
            if data is None:
                skipped.append((p, "unreadable"))
                continue
            meta = L.read_meta(data)
            if meta["Mode"] != "3" or meta["keys"] != 7:
                skipped.append((p, "not 7K mania"))
                continue
            m = DAN_RE.match(meta.get("Version", ""))
            if not m:
                skipped.append((p, "no dan in Version: " + meta.get("Version", "")))
                continue
            n, ln = L.note_stats(data)
            if n < 50:
                skipped.append((p, "under 50 notes"))
                continue
            name = m.group(1)
            dan = next(i for i, d in enumerate(L.DANS7) if d.lower() == name.lower())
            charts.append({"path": p, "data": data, "dan": dan, "kind": kind_of(p), "version": meta.get("Version", ""),
                           "notes": n, "holds": ln})
    charts.sort(key=lambda c: (c["dan"], c["kind"], c["path"]))
    return charts, skipped


_POOL = concurrent.futures.ThreadPoolExecutor(max_workers=max(1, (os.cpu_count() or 4) - 1))


def rate_all(engine, charts, params=None, rate=1.0):
    """[(rating, thumb_left, thumb_right)] in chart order."""
    def work(c):
        r = engine.rate(c["data"], rate, params)
        return r["rating"], r["thumb_left"], r["thumb_right"]
    return list(_POOL.map(work, charts))


def fit_curve(dans, ratings, degree=CURVE_DEGREE):
    """least-squares polynomial rating(dan). returns coefficients, highest power first."""
    import numpy as np
    return np.polyfit(np.asarray(dans, dtype=float), np.asarray(ratings, dtype=float), degree)


def dan_errors(coef, dans, ratings):
    """(rating - curve) / slope, in dans. the slope is floored at a fifth of the curve's mean
    slope so a flat stretch cannot blow one chart up."""
    import numpy as np
    d = np.asarray(dans, dtype=float)
    r = np.asarray(ratings, dtype=float)
    slope = np.polyval(np.polyder(coef), d)
    mean_slope = (np.polyval(coef, 14.0) - np.polyval(coef, 0.0)) / 14.0
    slope = np.maximum(slope, 0.2 * mean_slope)
    return (r - np.polyval(coef, d)) / slope


def score(charts, ratings):
    """the scores of one set of ratings. ratings: one number per chart."""
    import numpy as np
    dans = [c["dan"] for c in charts]
    coef = fit_curve(dans, ratings)
    err = dan_errors(coef, dans, ratings)
    out = {"rms": float(np.sqrt(np.mean(err ** 2))), "coef": [float(x) for x in coef], "err": [float(x) for x in err]}
    out["wrong"], out["pairs"] = L.wrong_pairs(dans, ratings)
    out["kind"] = {}
    for k in sorted(set(c["kind"] for c in charts)):
        idx = [i for i, c in enumerate(charts) if c["kind"] == k]
        w, _ = L.wrong_pairs([dans[i] for i in idx], [ratings[i] for i in idx])
        out["kind"][k] = {"n": len(idx), "bias": float(np.mean(err[idx])), "rms": float(np.sqrt(np.mean(err[idx] ** 2))),
                          "wrong": w}
    implied = np.asarray(dans) + err
    out["exact"] = float(np.mean(np.abs(np.rint(implied) - np.asarray(dans)) < 0.5))
    out["within1"] = float(np.mean(np.abs(np.rint(implied) - np.asarray(dans)) < 1.5))
    # the kinds' spread inside a dan, in dans: range of the four charts' errors, averaged.
    spread = []
    for d in sorted(set(dans)):
        e = [err[i] for i, c in enumerate(charts) if c["dan"] == d]
        if len(e) > 1:
            spread.append(max(e) - min(e))
    out["spread"] = float(np.mean(spread)) if spread else 0.0
    out["slope_ok"] = bool(np.all(np.polyval(np.polyder(coef), np.linspace(0, 14, 57)) > 0))
    return out


def describe(s):
    kinds = "  ".join(f"{k} {v['bias']:+.2f} (rms {v['rms']:.2f}, wrong {100 * v['wrong']:.1f}%)" for k, v in s["kind"].items())
    return (f"dan error rms {s['rms']:.3f}  wrong order {100 * s['wrong']:.2f}%  exact {100 * s['exact']:.0f}%  "
            f"within one {100 * s['within1']:.0f}%  spread in a dan {s['spread']:.2f}\n    kind bias: {kinds}")


def parse_sets(engine, sets):
    p = list(engine.defaults)
    for item in sets or []:
        name, _, value = item.partition("=")
        if name not in engine.names:
            raise SystemExit(f"unknown setting {name!r}; known: {', '.join(engine.names)}")
        p[engine.names.index(name)] = float(value)
    return p


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dans", required=True, action="append")
    ap.add_argument("--dll", required=True)
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--out")
    ap.add_argument("--table", action="store_true", help="print every chart")
    args = ap.parse_args()

    engine = L.Xdc7(args.dll)
    params = parse_sets(engine, args.set)
    charts, skipped = load_dan_charts([os.path.abspath(d) for d in args.dans])
    res = rate_all(engine, charts, params)
    ratings = [r[0] for r in res]
    s = score(charts, ratings)
    print(f"{len(charts)} dan charts, {len(skipped)} files skipped")
    print("settings: " + ", ".join(f"{n}={v:g}" for n, v in zip(engine.names, params)))
    print(describe(s))
    print("curve rating(dan): " + "  ".join(f"{L.DANS7[d]} {sum(c * d ** (len(s['coef']) - 1 - i) for i, c in enumerate(s['coef'])):.2f}" for d in range(15)))
    if args.table:
        print(f"\n{'dan':9}" + "".join(f"{k:>22}" for k in KINDS))
        for d in range(15):
            row = f"{L.DANS7[d]:9}"
            for k in KINDS:
                hit = [i for i, c in enumerate(charts) if c["dan"] == d and c["kind"] == k]
                row += "".join(f"{ratings[i]:9.2f} ({s['err'][i]:+5.2f}) {'L' if res[i][1] <= res[i][2] else 'R'}   " for i in hit[:1]) or " " * 22
            print(row)
    if args.out:
        os.makedirs(args.out, exist_ok=True)
        with open(os.path.join(args.out, "dan7_calibration.csv"), "w", newline="", encoding="utf-8-sig") as f:
            w = csv.writer(f)
            w.writerow(["dan", "kind", "rating", "thumb_left", "thumb_right", "dan_error", "notes", "holds", "version", "file"])
            for c, r, e in zip(charts, res, s["err"]):
                w.writerow([L.DANS7[c["dan"]], c["kind"], f"{r[0]:.4f}", f"{r[1]:.4f}", f"{r[2]:.4f}", f"{e:.3f}",
                            c["notes"], c["holds"], c["version"], os.path.basename(c["path"])])
        with open(os.path.join(args.out, "dan7_curve.json"), "w", encoding="utf-8") as f:
            json.dump({"engine": os.path.basename(args.dll), "rate": 1.0,
                       "settings": dict(zip(engine.names, params)),
                       "curve": {L.DANS7[d]: round(float(sum(c * d ** (len(s["coef"]) - 1 - i) for i, c in enumerate(s["coef"]))), 4) for d in range(15)},
                       "scores": {k: v for k, v in s.items() if k not in ("err",)},
                       "skipped": [{"file": os.path.basename(p), "reason": why} for p, why in skipped]}, f, indent=2)
    return 0


if __name__ == "__main__":
    sys.exit(main())
