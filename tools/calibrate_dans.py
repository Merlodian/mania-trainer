"""Calibrate mania-trainer's dan thresholds from folders of Dan ~ REFORM ~ beatmaps.

Every 4K .osu file under the dan folders is rated with xdc1.dll (the same engine and the
same C interface the XDC1 Analyzer uses) at rate 1.0. Each file is assigned to a dan from
its "Version:" line, and the ratings of every file of a dan (the Jack/Tech/Speed/Stamina
maps, the full course, and all sample maps and sample courses) are averaged. That average
is the threshold a chart has to reach for mania-trainer to call it that dan.

Version lines it understands:
    "~ 9th ~ (Marathon)", "~ EXTRA-DELTA ~ (Marathon)", "9th (Marathon)"   full courses
    "The Fool 1.05x (cut) ~ 9th ~ (Marathon)", "Big Brother ~ Alpha ~ (Marathon)"  maps
    "Laur - Exitium ~ Eta Tech ~", "~ FINAL - ETA ~ ( Marathon )"           Eta
    "SEVEN {1.25x} ~ Iota Jack ~", "~ FINAL-KAPPA ~ (Marathon)"              Theta, Iota, Kappa
    "u.Dan1 (Marathon)" (ultra.Dan pre-v1): no dan in the name, so the dan is read from the
    songs listed in its Tags, matched against the individual maps.
Intro dans ("INTRO-1st" ...) are skipped.

usage:
    python -I calibrate_dans.py --dans "<Dan Beatmaps folder>" [--dans "<More Dans folder>" ...] --dll "<xdc1.dll>" --out <dir>

With one --dans folder, the file column of the CSV is relative to it; with several, relative to the
folder that holds them all.

writes <dir>/dan_calibration.csv (one row per file) and <dir>/dan_thresholds.json.
"""

import argparse
import concurrent.futures
import csv
import ctypes
import json
import os
import re
import statistics
import sys

DANS = ["1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th", "9th", "10th",
        "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa"]
GREEK = ["alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa"]
ORDINAL_RE = re.compile(r"\b(10th|[1-9](?:st|nd|rd|th))\b", re.I)


# ---------------------------------------------------------------------------- xdc1.dll

class Engine:
    def __init__(self, path):
        self.dll = ctypes.CDLL(path)
        self.dll.xdc1_skillset_count.restype = ctypes.c_int
        self.dll.xdc1_skillset_name.argtypes = [ctypes.c_int]
        self.dll.xdc1_skillset_name.restype = ctypes.c_char_p
        n = self.dll.xdc1_skillset_count()
        self.skillsets = [self.dll.xdc1_skillset_name(i).decode() for i in range(n)]

        class Result(ctypes.Structure):
            _fields_ = [("rating", ctypes.c_double), ("rating_rice", ctypes.c_double),
                        ("skillsets", ctypes.c_double * n), ("first_ms", ctypes.c_double),
                        ("last_ms", ctypes.c_double), ("n_notes", ctypes.c_int),
                        ("n_holds", ctypes.c_int), ("n_points", ctypes.c_int),
                        ("grid", ctypes.POINTER(ctypes.c_double)),
                        ("graph", ctypes.POINTER(ctypes.c_double)),
                        ("error", ctypes.c_char * 256)]

        self.Result = Result
        self.dll.xdc1_analyse.argtypes = [ctypes.c_char_p, ctypes.c_longlong, ctypes.c_double,
                                          ctypes.c_int, ctypes.POINTER(Result)]
        self.dll.xdc1_analyse.restype = ctypes.c_int
        self.dll.xdc1_free_result.argtypes = [ctypes.POINTER(Result)]
        self.dll.xdc1_free_result.restype = None

    def analyse(self, data, rate=1.0):
        r = self.Result()
        rc = self.dll.xdc1_analyse(data, len(data), rate, 0, ctypes.byref(r))
        try:
            if rc != 0:
                raise RuntimeError(r.error.decode(errors="replace"))
            return {
                "rating": r.rating,
                "rating_rice": r.rating_rice,
                "skillsets": dict(zip(self.skillsets, list(r.skillsets))),
                "n_notes": r.n_notes,
                "n_holds": r.n_holds,
                "length_s": (r.last_ms - r.first_ms) / 1000.0,
            }
        finally:
            self.dll.xdc1_free_result(ctypes.byref(r))


# ---------------------------------------------------------------------------- .osu metadata

def read_meta(data):
    text = data.decode("utf-8", errors="replace")
    meta = {}
    for line in text.splitlines():
        line = line.strip()
        if line.startswith("[HitObjects]"):
            break
        m = re.match(r"^(Title|Version|Tags|Mode|CircleSize)\s*:\s*(.*)$", line)
        if m and m.group(1) not in meta:
            meta[m.group(1)] = m.group(2).strip()
    return meta


def level_token(version):
    """The dan part of a Version line: the last "~ ... ~" group, or the word before
    "(Marathon)" in the v3 sample naming."""
    m = re.search(r"~\s*([^~]+?)\s*~\s*(?:\(\s*Marathon\s*\))?\s*$", version, re.I)
    if m:
        return m.group(1)
    m = re.match(r"^\s*(\S+)\s*\(\s*Marathon\s*\)\s*$", version, re.I)
    if m:
        return m.group(1)
    return None


def dan_from_token(token):
    t = token.lower()
    if "intro" in t:
        return "INTRO"
    m = ORDINAL_RE.search(t)
    if m:
        return m.group(1).lower()
    for g in GREEK:
        if re.search(r"\b" + g + r"\b", t):
            return g.capitalize()
    return None


def song_name(version):
    """"The Fool 1.05x (cut) ~ 9th ~ (Marathon)" -> "the fool"."""
    name = version.split("~")[0]
    name = re.sub(r"\(cut\)|\{cut\}|\b\d+(?:\.\d+)?x\b", "", name, flags=re.I)
    return re.sub(r"\s+", " ", name).strip().lower()


def category(title, token):
    t = title.lower()
    for key, cat in (("jackmap", "Jack"), ("techmap", "Tech"), ("speedmap", "Speed"),
                     ("staminamap", "Stamina")):
        if key in t:
            return cat
    if "finalmaps" in t or "map pack" in t:
        for cat in ("Jack", "Tech", "Speed", "Stamina"):
            if cat.lower() in token.lower():
                return cat
    if "missingmap" in t:
        return "Map (MissingMap)"
    return "Course"


# ---------------------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dans", required=True, action="append")
    ap.add_argument("--dll", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    engine = Engine(os.path.abspath(args.dll))
    roots = [os.path.abspath(d) for d in args.dans]
    base = roots[0] if len(roots) == 1 else os.path.commonpath(roots)
    paths = []
    for top in roots:
        for root, _, files in os.walk(top):
            paths += [os.path.join(root, f) for f in files if f.lower().endswith(".osu")]
    paths.sort()

    rows, skipped, ultra = [], [], []
    for p in paths:
        with open(p, "rb") as f:
            data = f.read()
        meta = read_meta(data)
        rel = os.path.relpath(p, base)
        version = meta.get("Version", "")
        if meta.get("Mode") != "3" or meta.get("CircleSize") not in ("4", "4.0"):
            skipped.append((rel, "not 4K mania"))
            continue
        token = level_token(version)
        if token is None:
            skipped.append((rel, "unrecognised Version: " + version))
            continue
        um = re.match(r"^u\.dan(\d+)$", token.strip(), re.I)
        dan = None if um else dan_from_token(token)
        if dan == "INTRO":
            skipped.append((rel, "intro dan"))
            continue
        row = {"file": rel, "folder": os.path.dirname(rel), "title": meta.get("Title", ""),
               "version": version, "token": token, "dan": dan, "tags": meta.get("Tags", ""),
               "data": data}
        if um:
            ultra.append(row)
        elif dan is None:
            skipped.append((rel, "no dan in Version: " + version))
            continue
        rows.append(row)

    # normalise ordinals to the DANS spelling
    for r in rows:
        if r["dan"] and r["dan"][0].isdigit():
            r["dan"] = r["dan"].lower()

    # ultra.Dan courses: read the dan from the songs in Tags.
    maps = [r for r in rows if r["dan"] and "~" in r["version"] and category(r["title"], r["token"]) != "Course"]
    for r in ultra:
        votes = {}
        for tag in [t.strip().lower() for t in r["tags"].split(",") if t.strip()]:
            for m in maps:
                s = song_name(m["version"])
                if s and (tag.startswith(s) or s.startswith(tag)):
                    votes[m["dan"]] = votes.get(m["dan"], 0) + 1
                    break
        if votes:
            best = max(votes.items(), key=lambda kv: kv[1])
            r["dan"] = best[0]
            r["token"] += f" (from Tags -> {best[0]}, {best[1]}/{len(r['tags'].split(','))} songs)"
        else:
            r["dan"] = None
    for r in [r for r in rows if r["dan"] is None]:
        skipped.append((r["file"], "dan could not be determined: " + r["version"]))
    rows = [r for r in rows if r["dan"] is not None]

    # rate every chart (the dll releases the GIL, so threads run in parallel)
    def work(r):
        try:
            return r, engine.analyse(r["data"], 1.0), None
        except Exception as e:  # noqa: BLE001
            return r, None, str(e)

    results = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, (os.cpu_count() or 4) - 1)) as ex:
        for r, a, err in ex.map(work, rows):
            if err:
                skipped.append((r["file"], "xdc1 error: " + err))
                continue
            r.pop("data", None)
            r.update(a)
            r["category"] = category(r["title"], r["token"])
            results.append(r)

    os.makedirs(args.out, exist_ok=True)
    with open(os.path.join(args.out, "dan_calibration.csv"), "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        w.writerow(["dan", "category", "rating", "rating_rice", "notes", "holds", "length_s",
                    "folder", "title", "version", "file"] + engine.skillsets)
        for r in sorted(results, key=lambda r: (DANS.index(r["dan"]), r["category"], r["rating"])):
            w.writerow([r["dan"], r["category"], f"{r['rating']:.4f}", f"{r['rating_rice']:.4f}",
                        r["n_notes"], r["n_holds"], f"{r['length_s']:.1f}", r["folder"], r["title"],
                        r["version"], r["file"]] + [f"{r['skillsets'][s]:.4f}" for s in engine.skillsets])

    summary = []
    for d in DANS:
        rs = [r for r in results if r["dan"] == d]
        if not rs:
            summary.append({"dan": d, "count": 0})
            continue
        ratings = [r["rating"] for r in rs]
        by_cat = {}
        for r in rs:
            by_cat.setdefault(r["category"], []).append(r["rating"])
        summary.append({
            "dan": d,
            "count": len(rs),
            "mean": statistics.fmean(ratings),
            "median": statistics.median(ratings),
            "stdev": statistics.stdev(ratings) if len(ratings) > 1 else 0.0,
            "min": min(ratings),
            "max": max(ratings),
            "by_category": {c: {"count": len(v), "mean": statistics.fmean(v)} for c, v in sorted(by_cat.items())},
        })

    out = {
        "engine": os.path.basename(args.dll),
        "rate": 1.0,
        "method": "threshold = mean xdc1 rating of every chart of that dan (maps, courses, samples)",
        "thresholds": {s["dan"]: round(s["mean"], 4) for s in summary if s["count"]},
        "summary": summary,
        "skipped": [{"file": f, "reason": why} for f, why in skipped],
    }
    with open(os.path.join(args.out, "dan_thresholds.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)

    print(f"rated {len(results)} charts, skipped {len(skipped)}")
    print(f"{'dan':8} {'n':>3} {'mean':>8} {'median':>8} {'stdev':>7} {'min':>8} {'max':>8}   by category")
    for s in summary:
        if not s["count"]:
            print(f"{s['dan']:8} {0:>3}")
            continue
        cats = ", ".join(f"{c} {v['mean']:.2f}({v['count']})" for c, v in s["by_category"].items())
        print(f"{s['dan']:8} {s['count']:>3} {s['mean']:8.3f} {s['median']:8.3f} {s['stdev']:7.3f} "
              f"{s['min']:8.3f} {s['max']:8.3f}   {cats}")
    print("\nskipped:")
    for f, why in skipped:
        print(f"  {why}: {f}")
    print("\nultra.Dan:")
    for r in ultra:
        print(f"  {r['version']} -> {r['dan']}  [{r['token']}]")
    return 0


if __name__ == "__main__":
    sys.exit(main())
