"""Checks that xdc7 is xdc1 widened, not xdc1 changed, and that it behaves:

  4k        xdc7 on 4K charts gives xdc1's rice rating (same code path, two fingers a hand).
  embed     a 4K chart moved onto four of the seven columns (never the middle one) rates
            the same as the 4K chart, under either thumb: the three- and four-finger code
            with only two fingers in use is still xdc1.
  mirror    a mirrored chart rates the same. xdc1's own wobble on 4K is printed beside it.
  rates     the rating never falls as the rate rises. xdc1's own on 4K beside it.

usage:
    python -I xdc7_checks.py --dll7 xdc7.dll --dll1 xdc1.dll --k4 <folder or list.txt> --k7 <folder or list.txt>
                             [--sample N] [--seed S] [--only 4k,embed,mirror,rates]
                             [--set name=value ...] [--published]

--k4 / --k7 take a folder of .osu files or a text file with one path per line, and can
be given more than once.

The 4k and embed checks run at the neutral settings (xdc7_param_neutral), where xdc7 has to
be xdc1 exactly. --set changes settings on top of those, to show that a setting which only
concerns a third or fourth finger leaves two fingers alone. --published runs those two
checks at the published settings instead (they differ from xdc1 wherever a published
setting rescales one of xdc1's own terms). mirror and rates always use the published ones.
"""

import argparse
import concurrent.futures
import os
import random
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xdc7_lib as L  # noqa: E402

EMBEDDINGS = [[1, 2, 4, 5], [0, 1, 5, 6], [0, 2, 4, 6], [0, 2, 5, 6], [1, 2, 4, 6]]
RATES = [0.7, 0.8, 0.9, 1.0, 1.05, 1.1, 1.15, 1.2, 1.3, 1.4, 1.5]


def gather(sources, keys, sample, rng):
    paths = []
    for s in sources:
        if os.path.isdir(s):
            paths += L.find_osu(s)
        else:
            with open(s, encoding="utf-8") as f:
                paths += [line.rstrip("\r\n") for line in f if line.strip()]
    rng.shuffle(paths)
    out = []
    for p in paths:
        data = L.read_bytes(p)
        if data is None:
            continue
        meta = L.read_meta(data)
        if meta["Mode"] != "3" or meta["keys"] != keys:
            continue
        n, ln = L.note_stats(data)
        if n < 50:
            continue
        out.append({"path": p, "data": data, "n": n, "ln": ln})
        if len(out) >= sample:
            break
    return out


def pmap(fn, items):
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, (os.cpu_count() or 4) - 1)) as ex:
        return list(ex.map(fn, items))


def summary(name, diffs, tol=1e-9):
    if not diffs:
        print(f"  {name}: nothing to compare")
        return
    d = sorted(abs(x) for x in diffs)
    over = sum(x > tol for x in d)
    print(f"  {name}: {len(d)} charts, max |diff| {d[-1]:.3g}, median {statistics.median(d):.3g}, "
          f"99th pct {d[min(len(d) - 1, int(0.99 * len(d)))]:.3g}, over {tol:g}: {over}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dll7", required=True)
    ap.add_argument("--dll1", required=True)
    ap.add_argument("--k4", action="append", default=[])
    ap.add_argument("--k7", action="append", default=[])
    ap.add_argument("--sample", type=int, default=400)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--only", default="4k,embed,mirror,rates")
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--published", action="store_true")
    args = ap.parse_args()
    only = set(args.only.split(","))
    rng = random.Random(args.seed)
    e7, e1 = L.Xdc7(args.dll7), L.Xdc1(args.dll1)
    k4 = gather(args.k4, 4, args.sample, rng) if args.k4 else []
    k7 = gather(args.k7, 7, args.sample, rng) if args.k7 else []
    print(f"4K charts {len(k4)} ({sum(c['ln'] == 0 for c in k4)} without long notes), 7K charts {len(k7)}")
    neutral = list(e7.defaults if args.published else e7.neutral)
    for item in args.set:
        name, _, value = item.partition("=")
        if name not in e7.names:
            raise SystemExit(f"unknown setting {name!r}; known: {', '.join(e7.names)}")
        neutral[e7.names.index(name)] = float(value)
    print("4k/embed settings: " + ", ".join(f"{n}={v:g}" for n, v in zip(e7.names, neutral)))

    def safe(fn):
        def run(c):
            try:
                return fn(c)
            except Exception as e:  # noqa: BLE001
                return ("error", str(e), c["path"])
        return run

    def split(res):
        ok = [r for r in res if not (isinstance(r, tuple) and r and r[0] == "error")]
        for r in res:
            if isinstance(r, tuple) and r and r[0] == "error":
                print("    error:", r[1], "|", r[2])
        return ok

    if "4k" in only and k4:
        print("\n4k: xdc7 (two fingers a hand) against xdc1's rice rating")
        res = split(pmap(safe(lambda c: (c["ln"], e7.rate(c["data"], 1.0, neutral)["rating"] - e1.rate(c["data"])["rating_rice"])), k4))
        summary("charts without long notes", [d for ln, d in res if ln == 0])
        summary("charts with long notes   ", [d for ln, d in res if ln > 0])
        res = split(pmap(safe(lambda c: e7.rate(c["data"], 1.3, neutral)["rating"] - e1.rate(c["data"], 1.3)["rating_rice"]), k4[:150]))
        summary("at rate 1.3              ", res)

    if "embed" in only and k4:
        print("\nembed: 4K chart on four of seven columns against the 4K chart")
        for emb in EMBEDDINGS:
            def work(c, emb=emb):
                base = e7.rate(c["data"], 1.0, neutral)["rating"]
                r = e7.rate(L.remap_columns(c["data"], emb, 7), 1.0, neutral)
                return r["thumb_left"] - base, r["thumb_right"] - base
            res = split(pmap(safe(work), k4[:200]))
            summary(f"columns {emb} left thumb ", [a for a, _ in res])
            summary(f"columns {emb} right thumb", [b for _, b in res])

    if "mirror" in only:
        print("\nmirror: rating of the mirrored chart minus the chart's")
        if k4:
            res = split(pmap(safe(lambda c: e1.rate(L.mirror(c["data"]))["rating_rice"] - e1.rate(c["data"])["rating_rice"]), k4))
            summary("xdc1, 4K (inherited)     ", res, tol=0.05)
        if k7:
            def work7(c):
                a, b = e7.rate(c["data"]), e7.rate(L.mirror(c["data"]))
                # mirrored, the left-thumb hold is the right-thumb hold of the original.
                return b["rating"] - a["rating"], b["thumb_left"] - a["thumb_right"], a["rating"]
            res = split(pmap(safe(work7), k7))
            summary("xdc7, 7K, rating         ", [x for x, _, _ in res], tol=0.05)
            summary("xdc7, 7K, one hold       ", [y for _, y, _ in res], tol=0.05)
            rel = sorted(abs(x) / max(r, 1e-9) for x, _, r in res)
            print(f"    relative: median {100 * statistics.median(rel):.3f}%, 99th pct {100 * rel[int(0.99 * len(rel))]:.3f}%, max {100 * rel[-1]:.3f}%")

    if "rates" in only:
        print("\nrates: steps down along " + " ".join(str(r) for r in RATES))

        def drops(seq):
            return [b - a for a, b in zip(seq, seq[1:]) if b < a]

        def report(name, res):
            alld = [d for r in res for d in r]
            charts = sum(1 for r in res if r)
            worst = min(alld) if alld else 0.0
            print(f"  {name}: {len(res)} charts, {charts} with a step down, {len(alld)} steps down of "
                  f"{len(res) * (len(RATES) - 1)}, worst {worst:.4f}")
        if k4:
            res = split(pmap(safe(lambda c: drops([e1.rate(c["data"], r)["rating_rice"] for r in RATES])), k4[:150]))
            report("xdc1, 4K (inherited)", res)
        if k7:
            res = split(pmap(safe(lambda c: drops([e7.rate(c["data"], r)["rating"] for r in RATES])), k7[:150]))
            report("xdc7, 7K            ", res)
    return 0


if __name__ == "__main__":
    sys.exit(main())
