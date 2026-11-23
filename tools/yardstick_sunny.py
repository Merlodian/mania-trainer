"""Rate the charts of charts.tsv (see eval7.py) with sunnyxxy's Star-Rating-Rebirth, so that it
can be scored beside xdc7 as a yardstick: eval7.py score --ratings sunny=sunny.tsv.

None of that project's code is here. Download it yourself (github.com/sunnyxxy/Star-Rating-Rebirth;
it has no licence file, so it is not ours to copy or ship) and point --repo at the folder
that holds its algorithm.py. Its algorithm runs unchanged. Two things around it are ours:

  * the chart is read here, read-only, and handed to it in the shape its own reader returns
    (its reader opens charts for writing and stops at the first odd character);
  * if pandas is not installed, the one thing it uses pandas for (sorting a table by one
    column) is done by a few lines of numpy.

usage:
    python -I yardstick_sunny.py --repo <folder with algorithm.py> --charts charts.tsv --out sunny.tsv
                                 [--od 9] [--jobs N]

--od rates every chart as if it had that OverallDifficulty (its hit leniency depends on the
chart's OD; xdc7's does not). Left out, each chart's own OD is used, as the program does.
"""

import argparse
import concurrent.futures
import csv
import os
import sys
import types

_ALGO = None
_OD = None


def _pandas_stand_in():
    import numpy as np

    class _Col:
        def __init__(self, a):
            self.values = a

    class _Index:
        def __init__(self, a):
            self._a = a

        def to_numpy(self):
            return self._a

    class DataFrame:
        def __init__(self, data, index=None):
            self._d = {k: np.asarray(v) for k, v in data.items()}
            n = len(next(iter(self._d.values())))
            self.index = _Index(np.arange(n) if index is None else index)

        def sort_values(self, by):
            order = np.argsort(self._d[by], kind="stable")
            return DataFrame({k: v[order] for k, v in self._d.items()}, self.index.to_numpy()[order])

        def __getitem__(self, k):
            return _Col(self._d[k])

    m = types.ModuleType("pandas")
    m.DataFrame = DataFrame
    return m


class _Reader:
    """a chart in the shape the project's own reader returns: key count, then each note's
    column, start, end and type, then the OD. read-only."""

    def __init__(self, path):
        self.path = path

    def process(self):
        with open(self.path, "rb") as f:
            text = f.read().decode("utf-8", errors="replace")
        self.keys, self.od = -1, -1.0
        self.cols, self.starts, self.ends, self.types = [], [], [], []
        section = ""
        for line in text.splitlines():
            s = line.strip()
            if s.startswith("["):
                section = s
                continue
            if section == "[Difficulty]":
                k, _, v = s.partition(":")
                if k.strip() == "CircleSize":
                    self.keys = int(float(v))
                elif k.strip() == "OverallDifficulty":
                    self.od = float(v)
            elif section == "[HitObjects]" and s:
                p = s.split(",")
                if len(p) < 6 or self.keys <= 0:
                    continue
                try:
                    col = int(int(float(p[0])) / int(512 / self.keys))
                    start, typ = int(float(p[2])), int(p[3])
                    end = int(float(p[5].split(":")[0]))
                except ValueError:
                    continue
                self.cols.append(min(max(col, 0), self.keys - 1))
                self.starts.append(start)
                self.ends.append(end)
                self.types.append(typ)
        if _OD is not None:
            self.od = _OD

    def get_parsed_data(self):
        return [self.keys, self.cols, self.starts, self.ends, self.types, self.od]


def _init(repo, od):
    global _ALGO, _OD
    _OD = od
    try:
        import pandas  # noqa: F401
    except ImportError:
        sys.modules["pandas"] = _pandas_stand_in()
    sys.path.append(repo)        # last, so nothing in it can stand in for a library
    import algorithm
    algorithm.osu_parser.parser = _Reader
    _ALGO = algorithm


def _rate(path):
    try:
        return float(_ALGO.calculate(path, "NM"))
    except Exception as e:  # noqa: BLE001
        return f"{type(e).__name__}: {e}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--charts", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--od", type=float)
    ap.add_argument("--jobs", type=int, default=max(1, (os.cpu_count() or 4) - 2))
    args = ap.parse_args()
    repo = os.path.abspath(args.repo)
    if not os.path.isfile(os.path.join(repo, "algorithm.py")):
        raise SystemExit(f"no algorithm.py in {repo}")
    with open(args.charts, encoding="utf-8", newline="") as fh:
        paths = [r["path"] for r in csv.DictReader(fh, delimiter="\t", quoting=csv.QUOTE_NONE, escapechar="\\")]
    with concurrent.futures.ProcessPoolExecutor(max_workers=args.jobs, initializer=_init, initargs=(repo, args.od)) as ex:
        res = list(ex.map(_rate, paths, chunksize=8))
    bad = [(p, r) for p, r in zip(paths, res) if isinstance(r, str)]
    with open(args.out, "w", encoding="utf-8", newline="") as fh:
        for p, r in zip(paths, res):
            if not isinstance(r, str):
                fh.write(f"{p}\t{r:.6f}\n")
    print(f"rated {len(paths) - len(bad)} charts, {len(bad)} failed")
    for p, r in bad[:10]:
        print("  failed:", os.path.basename(p), "|", r)
    return 0


if __name__ == "__main__":
    sys.exit(main())
