"""Score 7K rating sources (rice7, another calculator, plain note density) on labelled charts.

Four steps, so that any calculator can be scored on exactly the same charts:

  list   collects the labelled charts and writes charts.tsv (one row per chart, with its
         label and its plain note density).
  rate   rates every chart of charts.tsv with rice7.dll and writes ratings.tsv
         (path <tab> rating). Any other calculator only has to write the same two columns.
  score  prints, for every ratings file given and for two baselines that only count notes
         (notes per second, and the 90th percentile of notes in any one second):

           dan charts    the same scores as calibrate_dans7.py
           jack packs    dan-labelled jack charts: wrong-order pairs, and how far the rating
                         sits from the label, read off the source's own dan curve
           dan courses   the full dan courses, the same way
           BMS tables    charts labelled with a BMS table level in the Version line
                         ([n_5], [i_12], [sl_3], [st_7] ...): order inside each table, order
                         on one shared axis, and the dan each region of the axis comes out at

  resample  says how much of a difference between sources is luck of which charts happen
         to be labelled: the first ratings file against every other one and against p90
         density, with the labelled charts drawn again with replacement (whole dans for
         the dan charts, single charts everywhere else).

usage:
    python -I eval7.py list  --out charts.tsv [--dans DIR ...] [--library SONGS] [--bms DIR]
    python -I eval7.py rate  --dll rice7.dll --charts charts.tsv --out rice7.tsv
    python -I eval7.py score --charts charts.tsv --ratings rice7=rice7.tsv [--ratings name=file ...]
    python -I eval7.py resample --charts charts.tsv --ratings rice7=rice7.tsv [--ratings name=file ...]

--dans     folders of dan charts, as for calibrate_dans7.py
--library  an osu! Songs folder: charts from "7k Jack Maps in ..." sets (labelled "// reg5",
           "// Gamma" ...) and from the "7K Dan Course" regular courses are taken from it
--bms      a folder of 7K charts carrying BMS table levels in their Version line

Only 7K charts with 200 notes or more and at most 5% long notes are used outside the dan
folders (rice7 reads a long note as a tap). Nothing here knows where anything is installed.
"""

import argparse
import collections
import concurrent.futures
import csv
import math
import os
import re
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rice7_lib as L  # noqa: E402
import calibrate_dans7 as C  # noqa: E402

MIN_NOTES = 200
MAX_LN_SHARE = 0.05


# ---------------------------------------------------------------------------- plain density

def features(data, keys=7):
    """notes, long notes (held over 30 ms), length, notes per second, and the 90th percentile
    of the notes in a one-second window stepped every 250 ms."""
    i = data.find(b"[HitObjects]")
    notes = []
    holds = 0
    if i >= 0:
        for line in data[i:].split(b"\n")[1:]:
            f = line.strip().split(b",")
            if len(f) < 5:
                continue
            try:
                t = int(float(f[2]))
                typ = int(f[3])
                if typ & 128 and len(f) > 5 and int(float(f[5].split(b":")[0])) - t > 30:
                    holds += 1
            except ValueError:
                continue
            notes.append(t)
    n = len(notes)
    if n < 2:
        return {"n": n, "ln": holds, "dur": 0.0, "nps": 0.0, "p90": 0.0}
    notes.sort()
    t0, t1 = notes[0], notes[-1]
    dur = max((t1 - t0) / 1000.0, 1.0)
    dens = []
    j0 = j1 = 0
    t = t0
    while t <= t1:
        while j0 < n and notes[j0] < t:
            j0 += 1
        while j1 < n and notes[j1] < t + 1000:
            j1 += 1
        dens.append(j1 - j0)
        t += 250
    dens.sort()
    return {"n": n, "ln": holds, "dur": dur, "nps": n / dur, "p90": float(dens[min(len(dens) - 1, int(0.9 * len(dens)))])}


# ---------------------------------------------------------------------------- labels

JACK_LEVELS = {"gamma": 11, "azimuth": 12, "zenith": 13, "stellium": 14, "stellium+": 14.5, "omega": 15.5}


def jack_label(version):
    m = re.search(r"//\s*(reg\s*\d+|gamma|azimuth|zenith\??|stellium\+?|omega)\s*\??\s*$", version.strip(), re.I)
    if not m:
        return None
    t = m.group(1).lower().replace(" ", "").rstrip("?")
    return float(t[3:]) if t.startswith("reg") else float(JACK_LEVELS[t])


def jack_type(version):
    m = re.match(r"\s*\[([^\]]+)\]", version)
    t = (m.group(1).lower() if m else "?").replace(" ", "").rstrip("-")
    for key in ("chordjack", "midjack", "minijack", "anchorjack", "speedjack", "fulljack", "dense"):
        if key in t:
            return "dense chordjack" if (key == "chordjack" and "dense" in t) else key
    return "other"


COURSE_RE = re.compile(r"\s*(0th|1st|2nd|3rd|[4-9]th|10th|Gamma|Azimuth|Zenith|Stellium)\s+Dan\b(?!.*\(LN\))", re.I)
COURSE_SET_RE = re.compile(r"7K Dan Course - (Regular Dan Phase|Insane Level 3 \(Regular\)|Extra Level \(Regular\)|Dan Phase IV)", re.I)
JACK_SET_RE = re.compile(r"7k Jack Maps in", re.I)

BMS_LABEL_RE = re.compile(r"\[\s*(n2|i2|n|i|oj|sr|sl|st)_([^\]\s]*)\s*\]", re.I)
BMS_TABLES = [("n", "Normal"), ("n2", "Normal2"), ("sr", "Starlight"), ("i", "Insane"), ("i2", "Insane2"),
              ("sl", "Satellite"), ("oj", "Overjoy"), ("st", "Stella")]

# where each table's levels sit on one shared axis: the rows (1 to 47) of DealerOfMelons'
# chart of the 7K BMS tables against the dans.
INSANE_ROW = {0: 12.5, 24: 37.5, 25: 41.5}
for _k in range(1, 24):
    INSANE_ROW[_k] = 12.0 + _k
OJ_ROW = {0: 32.0, 1: 33.0, 2: 34.0, 3: 35.0, 4: 37.5, 5: 41.5, 6: 44.5, 7: 46.0, 8: 47.0}
SL_ROW = {0: 11.5, 1: 13.5, 2: 15.33, 3: 16.67, 4: 18.5, 5: 20.5, 6: 22.33, 7: 24.0, 8: 25.67, 9: 27.33,
          10: 28.5, 11: 29.5, 12: 30.67}
ST_ROW = {0: 31.5, 1: 33.0, 2: 34.0, 3: 35.5, 4: 37.0, 5: 38.0, 6: 39.5, 7: 41.0, 8: 42.0, 9: 43.5,
          10: 45.0, 11: 46.0, 12: 47.0}
# the dans the chart puts beside its rows (Stella 6-7 Azimuth, 8-9 Zenith, 10-11 Stellium).
REGIONS = [(12.2, 15.0, "1st", 1, 1), (15.0, 17.0, "2nd", 2, 2), (17.0, 22.0, "3rd-4th", 3, 4),
           (22.0, 26.0, "5th-6th", 5, 6), (26.0, 31.5, "7th-9th", 7, 9), (31.5, 35.0, "10th", 10, 10),
           (35.0, 39.5, "Gamma", 11, 11), (39.5, 42.0, "Azimuth", 12, 12), (42.0, 45.0, "Zenith", 13, 13),
           (45.0, 99.0, "Stellium", 14, 14)]


def bms_level(x):
    """'11+' -> 11.33, '12-' -> 11.67, '7' -> 7.0; anything else -> None."""
    m = re.fullmatch(r"(\d+)([+-]?)", x)
    if not m:
        return None
    return int(m.group(1)) + {"": 0.0, "+": 0.33, "-": -0.33}[m.group(2)]


def _interp(table, v):
    lo, hi = math.floor(v), math.ceil(v)
    if lo not in table or hi not in table:
        return None
    return table[lo] + (table[hi] - table[lo]) * (v - lo)


def bms_row(tab, v):
    if tab in ("n", "n2"):
        return v if v <= 12.5 else None
    if tab in ("i", "i2"):
        return 12.2 if v < 0 else _interp(INSANE_ROW, v)
    if tab == "oj":
        return _interp(OJ_ROW, v)
    if tab == "sr":
        return 7.0 + (v - 1.0) * 5.0 / 12.0
    if tab == "sl":
        return _interp(SL_ROW, v)
    if tab == "st":
        return _interp(ST_ROW, v)
    return None


def bms_labels(version):
    """[(table, level, row or None)] for every table level in a Version line."""
    out = []
    for t, x in BMS_LABEL_RE.findall(version):
        v = bms_level(x)
        if v is not None:
            out.append((t.lower(), v, bms_row(t.lower(), v)))
    return out


# ---------------------------------------------------------------------------- list

FIELDS = ["set", "kind", "label", "n", "ln", "dur", "nps", "p90", "version", "path"]


def _usable(path):
    data = L.read_bytes(path)
    if data is None:
        return None
    meta = L.read_meta(data)
    if meta["Mode"] != "3" or meta["keys"] != 7:
        return None
    f = features(data)
    if f["n"] < MIN_NOTES or f["ln"] > MAX_LN_SHARE * f["n"]:
        return None
    f["version"] = meta.get("Version", "").replace("\t", " ").strip()
    f["path"] = path
    return f


def cmd_list(args):
    rows = []
    if args.dans:
        charts, _ = C.load_dan_charts([os.path.abspath(d) for d in args.dans])
        for c in charts:
            f = features(c["data"])
            rows.append({"set": "dan", "kind": c["kind"], "label": c["dan"], "version": c["version"].replace("\t", " "),
                         "path": c["path"], **f})
    pool = concurrent.futures.ThreadPoolExecutor(max_workers=max(1, (os.cpu_count() or 4) - 1))
    if args.library:
        todo = []
        for d in sorted(os.listdir(args.library)):
            full = os.path.join(args.library, d)
            group = "jack" if JACK_SET_RE.search(d) else "course" if COURSE_SET_RE.search(d) else None
            if not group or not os.path.isdir(full):
                continue
            todo += [(group, os.path.join(full, f)) for f in sorted(os.listdir(full)) if f.lower().endswith(".osu")]
        seen = set()
        for (group, _), f in zip(todo, pool.map(lambda gp: _usable(gp[1]), todo)):
            if f is None:
                continue
            if group == "jack":
                label, kind = jack_label(f["version"]), jack_type(f["version"])
            else:
                m = COURSE_RE.match(f["version"])
                label = float(next(i for i, x in enumerate(L.DANS7) if x.lower() == m.group(1).lower())) if m else None
                kind = "course"
            key = (group, f["version"], f["n"])     # the same chart shipped in two sets counts once
            if label is None or key in seen:
                continue
            seen.add(key)
            rows.append({"set": group, "kind": kind, "label": label, **f})
    if args.bms:
        paths = L.find_osu(args.bms)
        for f in pool.map(_usable, paths):
            if f is None:
                continue
            labs = bms_labels(f["version"])
            if not labs:
                continue
            rows.append({"set": "bms", "kind": "", "label": ";".join(f"{t}:{v:g}" for t, v, _ in labs), **f})
    with open(args.out, "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, FIELDS, delimiter="\t", quoting=csv.QUOTE_NONE, quotechar=None, escapechar="\\", extrasaction="ignore")
        w.writeheader()
        for r in rows:
            w.writerow(r)
    print(f"wrote {len(rows)} charts: " + ", ".join(f"{k} {v}" for k, v in collections.Counter(r["set"] for r in rows).items()))
    return 0


def read_charts(path):
    with open(path, encoding="utf-8", newline="") as fh:
        rows = list(csv.DictReader(fh, delimiter="\t", quoting=csv.QUOTE_NONE, escapechar="\\"))
    for r in rows:
        for k in ("n", "ln"):
            r[k] = int(float(r[k]))
        for k in ("dur", "nps", "p90"):
            r[k] = float(r[k])
    return rows


# ---------------------------------------------------------------------------- rate

def cmd_rate(args):
    engine = L.Rice7(args.dll)
    charts = read_charts(args.charts)

    def work(c):
        data = L.read_bytes(c["path"])
        try:
            return engine.rate(data)["rating"]
        except Exception as e:  # noqa: BLE001
            return str(e)

    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, (os.cpu_count() or 4) - 1)) as ex:
        res = list(ex.map(work, charts))
    bad = 0
    with open(args.out, "w", encoding="utf-8", newline="") as fh:
        for c, r in zip(charts, res):
            if isinstance(r, str):
                bad += 1
                continue
            fh.write(f"{c['path']}\t{r:.6f}\n")
    print(f"rated {len(charts) - bad} charts, {bad} failed; settings: " +
          ", ".join(f"{n}={v:g}" for n, v in zip(engine.names, engine.defaults)))
    return 0


def read_ratings(path):
    out = {}
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            p, _, v = line.rstrip("\r\n").rpartition("\t")
            try:
                out[p] = float(v)
            except ValueError:
                pass
    return out


# ---------------------------------------------------------------------------- score

def implied_dans(coef, ratings):
    """the dan each rating sits at on the source's own dan curve (its rising part, -3 to 18)."""
    import numpy as np
    xs = np.linspace(-3.0, 18.0, 2101)
    ys = np.polyval(np.asarray(coef), xs)
    keep = [0]
    for i in range(1, len(xs)):
        if ys[i] > ys[keep[-1]]:
            keep.append(i)
    return np.interp(np.asarray(ratings, dtype=float), ys[keep], xs[keep])


def score_source(name, rating_of, charts):
    """every score of one source. rating_of: path -> rating."""
    import numpy as np
    out = {"name": name}
    dan = [c for c in charts if c["set"] == "dan"]
    if not dan:
        raise SystemExit("charts.tsv has no dan charts: the dan curve needs them")
    dcharts = [{"dan": int(float(c["label"])), "kind": c["kind"]} for c in dan]
    s = C.score(dcharts, [rating_of[c["path"]] for c in dan])
    out["dan"] = s
    coef = s["coef"]
    for group in ("jack", "course"):
        cs = [c for c in charts if c["set"] == group]
        if not cs:
            continue
        lab = np.array([float(c["label"]) for c in cs])
        val = np.array([rating_of[c["path"]] for c in cs])
        err = implied_dans(coef, val) - lab
        g = {"n": len(cs), "wrong": L.wrong_pairs(lab, val)[0], "rho": L.spearman(lab, val), "bias": float(err.mean()),
             "sd": float(err.std()), "rms": float(np.sqrt((err ** 2).mean())), "kinds": {}}
        for k in sorted(set(c["kind"] for c in cs)):
            e = err[[i for i, c in enumerate(cs) if c["kind"] == k]]
            g["kinds"][k] = (len(e), float(e.mean() - err.mean()))
        out[group] = g
    bms = [c for c in charts if c["set"] == "bms"]
    if bms:
        b = {"tables": {}, "regions": {}}
        labs = [bms_labels(c["version"]) for c in bms]
        val = np.array([rating_of[c["path"]] for c in bms])
        for t, _ in BMS_TABLES:
            idx = [(i, v) for i, ls in enumerate(labs) for tt, v, _ in ls if tt == t]
            if len(idx) < 3:
                continue
            lv = [v for _, v in idx]
            vv = [val[i] for i, _ in idx]
            b["tables"][t] = (len(idx), L.wrong_pairs(lv, vv)[0], L.spearman(lv, vv))
        rows = []
        for i, ls in enumerate(labs):
            r = [rw for _, _, rw in ls if rw is not None]
            if r:
                rows.append((statistics.fmean(r), i))
        axis = np.array([r for r, _ in rows])
        aval = val[[i for _, i in rows]]
        b["axis"] = {"n": len(rows), "rho": L.spearman(axis, aval), "wrong": L.wrong_pairs(axis, aval)[0],
                     "wrong3": L.wrong_pairs(axis, aval, 3.0)[0]}
        hard = axis >= 12.2
        b["axis"]["rho_dans"] = L.spearman(axis[hard], aval[hard])
        b["axis"]["n_dans"] = int(hard.sum())
        imp = implied_dans(coef, aval)
        hits = tot = 0
        for lo, hi, rname, d0, d1 in REGIONS:
            sel = (axis >= lo) & (axis < hi)
            if not sel.any():
                continue
            v = imp[sel]
            inside = (v >= d0 - 1.0) & (v <= d1 + 1.0)
            b["regions"][rname] = (int(sel.sum()), float(np.median(v)), float(np.percentile(v, 25)), float(np.percentile(v, 75)),
                                   float(inside.mean()), 0.5 * (d0 + d1))
            hits += int(inside.sum())
            tot += int(sel.sum())
        b["region_hit"] = hits / tot if tot else float("nan")
        mids = [(m, n) for n, m, _, _, _, mid in b["regions"].values() for m in [m - mid]]
        b["region_bias"] = sum(m * n for m, n in mids) / max(1, sum(n for _, n in mids))
        out["bms"] = b
    return out


def cmd_score(args):
    charts = read_charts(args.charts)
    sources = collections.OrderedDict()
    sources["notes per second"] = {c["path"]: c["nps"] for c in charts}
    sources["p90 density"] = {c["path"]: c["p90"] for c in charts}
    for item in args.ratings:
        name, _, path = item.partition("=")
        sources[name] = read_ratings(path)
    # the same charts for every source.
    keep = [c for c in charts if all(c["path"] in r for r in sources.values())]
    print(f"{len(charts)} charts listed, {len(keep)} rated by every source: " +
          ", ".join(f"{k} {v}" for k, v in collections.Counter(c["set"] for c in keep).items()))
    res = [score_source(n, r, keep) for n, r in sources.items()]
    names = [r["name"] for r in res]
    wid = max(18, max(len(n) for n in names) + 2)

    def table(title, rows):
        print("\n" + title)
        print(f"  {'':34s}" + "".join(f"{n:>{wid}s}" for n in names))
        for label, fn in rows:
            cells = []
            for r in res:
                try:
                    cells.append(fn(r))
                except KeyError:
                    cells.append("-")
            print(f"  {label:34s}" + "".join(f"{c:>{wid}s}" for c in cells))

    pct = lambda x: f"{100 * x:.2f}%"
    table("dan charts (the four kinds of a dan are meant to be equally hard)", [
        ("dan error, rms (dans)", lambda r: f"{r['dan']['rms']:.3f}"),
        ("wrong-order pairs", lambda r: pct(r["dan"]["wrong"])),
        ("within one dan", lambda r: f"{100 * r['dan']['within1']:.0f}%"),
        ("exact dan", lambda r: f"{100 * r['dan']['exact']:.0f}%"),
        ("spread inside a dan (dans)", lambda r: f"{r['dan']['spread']:.2f}"),
    ] + [(f"bias of {k} (dans)", (lambda k: lambda r: f"{r['dan']['kind'][k]['bias']:+.2f}")(k)) for k in C.KINDS])
    if any("jack" in r for r in res):
        kinds = sorted(res[0]["jack"]["kinds"], key=lambda k: -res[0]["jack"]["kinds"][k][0])
        table(f"jack packs ({res[0]['jack']['n']} charts, labelled by their mapper)", [
            ("wrong-order pairs", lambda r: pct(r["jack"]["wrong"])),
            ("rank correlation", lambda r: f"{r['jack']['rho']:.3f}"),
            ("rating minus label, mean (dans)", lambda r: f"{r['jack']['bias']:+.2f}"),
            ("rating minus label, sd (dans)", lambda r: f"{r['jack']['sd']:.2f}"),
        ] + [(f"  {k} ({res[0]['jack']['kinds'][k][0]}) vs the rest", (lambda k: lambda r: f"{r['jack']['kinds'][k][1]:+.2f}")(k))
             for k in kinds])
    if any("course" in r for r in res):
        table(f"dan courses ({res[0]['course']['n']} charts)", [
            ("wrong-order pairs", lambda r: pct(r["course"]["wrong"])),
            ("rating minus label, mean (dans)", lambda r: f"{r['course']['bias']:+.2f}"),
            ("rating minus label, sd (dans)", lambda r: f"{r['course']['sd']:.2f}"),
        ])
    if any("bms" in r for r in res):
        tn = dict(BMS_TABLES)
        tabs = [t for t, _ in BMS_TABLES if t in res[0]["bms"]["tables"]]
        table("BMS tables: wrong-order pairs inside each table", [
            (f"{tn[t]} ({res[0]['bms']['tables'][t][0]})", (lambda t: lambda r: pct(r["bms"]["tables"][t][1]))(t)) for t in tabs])
        table("BMS tables: rank correlation inside each table", [
            (f"{tn[t]}", (lambda t: lambda r: f"{r['bms']['tables'][t][2]:.3f}")(t)) for t in tabs])
        a = res[0]["bms"]["axis"]
        table(f"BMS tables on one axis ({a['n']} charts; {a['n_dans']} at Insane 0 and up)", [
            ("rank correlation", lambda r: f"{r['bms']['axis']['rho']:.3f}"),
            ("rank correlation, Insane 0 and up", lambda r: f"{r['bms']['axis']['rho_dans']:.3f}"),
            ("wrong-order pairs", lambda r: pct(r["bms"]["axis"]["wrong"])),
            ("wrong-order, 3+ rows apart", lambda r: pct(r["bms"]["axis"]["wrong3"])),
        ])
        regs = [n for _, _, n, _, _ in REGIONS if n in res[0]["bms"]["regions"]]
        table("BMS regions: median dan the charts come out at (share within a dan of the region)", [
            (f"{n} ({res[0]['bms']['regions'][n][0]})",
             (lambda n: lambda r: f"{r['bms']['regions'][n][1]:.1f} ({100 * r['bms']['regions'][n][4]:.0f}%)")(n)) for n in regs
        ] + [("all regions: within a dan", lambda r: f"{100 * r['bms']['region_hit']:.0f}%"),
             ("all regions: mean offset (dans)", lambda r: f"{r['bms']['region_bias']:+.2f}")])
    return 0


# ---------------------------------------------------------------------------- resample

def cmd_resample(args):
    import random
    import numpy as np
    charts = read_charts(args.charts)
    sources = collections.OrderedDict()
    for item in args.ratings:
        name, _, path = item.partition("=")
        sources[name] = read_ratings(path)
    if not sources:
        raise SystemExit("give at least one --ratings name=file")
    sources["p90 density"] = {c["path"]: c["p90"] for c in charts}
    charts = [c for c in charts if all(c["path"] in r for r in sources.values())]
    first, others = list(sources)[0], list(sources)[1:]
    rng = random.Random(args.seed)
    print(f"{first} minus each other source, over resamples: below zero means {first} is the better one")

    def line(title, deltas):
        d = np.asarray(deltas)
        print(f"    {title:36s} median {np.median(d):+7.3f}   5% to 95%: {np.percentile(d, 5):+7.3f} to {np.percentile(d, 95):+7.3f}"
              f"   better in {100 * np.mean(d < 0):5.1f}%, worse in {100 * np.mean(d > 0):5.1f}%")

    def rms(dans, ratings):
        return float(np.sqrt(np.mean(C.dan_errors(C.fit_curve(dans, ratings), dans, ratings) ** 2)))

    def wrong(labels, values):
        return 100.0 * L.wrong_pairs(labels, values)[0]

    def values(cs):
        return {n: np.array([sources[n][c["path"]] for c in cs]) for n in sources}

    # dan charts: whole dans are drawn, so the four kinds of a dan stay together.
    dan = [c for c in charts if c["set"] == "dan"]
    if dan:
        lab = np.array([int(float(c["label"])) for c in dan])
        by = {d: np.where(lab == d)[0] for d in sorted(set(lab))}
        val = values(dan)
        draws = []
        for _ in range(4000):
            pick = [rng.choice(list(by)) for _ in by]
            if len(set(pick)) >= 6:
                draws.append(np.concatenate([by[d] for d in pick]))
        print(f"\n  dan charts ({len(dan)}): dan error rms in dans, {len(draws)} draws of the {len(by)} dans")
        for n in others:
            line(f"against {n}", [rms(lab[i], val[first][i]) - rms(lab[i], val[n][i]) for i in draws])
        print("  dan charts: wrong-order pairs in points, the first 1500 of those draws")
        for n in others:
            line(f"against {n}", [wrong(lab[i], val[first][i]) - wrong(lab[i], val[n][i]) for i in draws[:1500]])

    for group, title in (("jack", "jack packs"), ("course", "dan courses")):
        cs = [c for c in charts if c["set"] == group]
        if not cs:
            continue
        lab = np.array([float(c["label"]) for c in cs])
        val = values(cs)
        draws = [np.array([rng.randrange(len(cs)) for _ in cs]) for _ in range(1500)]
        print(f"\n  {title} ({len(cs)}): wrong-order pairs in points, 1500 draws of the charts")
        for n in others:
            line(f"against {n}", [wrong(lab[i], val[first][i]) - wrong(lab[i], val[n][i]) for i in draws])

    bms = [c for c in charts if c["set"] == "bms"]
    if bms:
        labs = [bms_labels(c["version"]) for c in bms]
        val = values(bms)
        print("\n  BMS tables: wrong-order pairs inside the table in points, 200 draws of its charts")
        for t, tname in BMS_TABLES:
            pairs = [(i, v) for i, ls in enumerate(labs) for tt, v, _ in ls if tt == t]
            if len(pairs) < 3:
                continue
            at = np.array([i for i, _ in pairs])
            lv = np.array([v for _, v in pairs])
            draws = [np.array([rng.randrange(len(pairs)) for _ in pairs]) for _ in range(200)]
            for n in others:
                line(f"{tname} ({len(pairs)}) against {n}",
                     [wrong(lv[k], val[first][at[k]]) - wrong(lv[k], val[n][at[k]]) for k in draws])
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    a = sub.add_parser("list")
    a.add_argument("--out", required=True)
    a.add_argument("--dans", action="append", default=[])
    a.add_argument("--library")
    a.add_argument("--bms")
    a = sub.add_parser("rate")
    a.add_argument("--dll", required=True)
    a.add_argument("--charts", required=True)
    a.add_argument("--out", required=True)
    a = sub.add_parser("score")
    a.add_argument("--charts", required=True)
    a.add_argument("--ratings", action="append", default=[])
    a = sub.add_parser("resample")
    a.add_argument("--charts", required=True)
    a.add_argument("--ratings", action="append", default=[])
    a.add_argument("--seed", type=int, default=7)
    args = ap.parse_args()
    return {"list": cmd_list, "rate": cmd_rate, "score": cmd_score, "resample": cmd_resample}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
