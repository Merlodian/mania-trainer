"""Shared pieces of the rice7 (7K) tools: both engines through ctypes, .osu helpers that
rewrite columns, and the order statistics every tool reports.

Nothing here knows where anything is installed; every path comes from the caller.
"""

import ctypes
import math
import os
import re

DANS7 = ["0th", "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th", "9th", "10th",
         "Gamma", "Azimuth", "Zenith", "Stellium"]


# ---------------------------------------------------------------------------- engines

class Rice7:
    """rice7.dll. rate() is safe to call from several threads (ctypes releases the GIL)."""

    def __init__(self, path):
        self.dll = ctypes.CDLL(os.path.abspath(path))
        self.dll.rice7_param_count.restype = ctypes.c_int
        self.dll.rice7_param_name.argtypes = [ctypes.c_int]
        self.dll.rice7_param_name.restype = ctypes.c_char_p
        self.dll.rice7_param_default.argtypes = [ctypes.c_int]
        self.dll.rice7_param_default.restype = ctypes.c_double
        n = self.dll.rice7_param_count()
        self.names = [self.dll.rice7_param_name(i).decode() for i in range(n)]
        self.defaults = [self.dll.rice7_param_default(i) for i in range(n)]
        self.dll.rice7_param_neutral.argtypes = [ctypes.c_int]
        self.dll.rice7_param_neutral.restype = ctypes.c_double
        self.neutral = [self.dll.rice7_param_neutral(i) for i in range(n)]

        class Result(ctypes.Structure):
            _fields_ = [("rating", ctypes.c_double), ("thumb_left", ctypes.c_double),
                        ("thumb_right", ctypes.c_double), ("first_ms", ctypes.c_double),
                        ("last_ms", ctypes.c_double), ("keys", ctypes.c_int),
                        ("n_notes", ctypes.c_int), ("n_holds", ctypes.c_int),
                        ("n_points", ctypes.c_int),
                        ("grid", ctypes.POINTER(ctypes.c_double)),
                        ("graph", ctypes.POINTER(ctypes.c_double)),
                        ("error", ctypes.c_char * 256)]

        self.Result = Result
        self.dll.rice7_analyse.argtypes = [ctypes.c_char_p, ctypes.c_longlong, ctypes.c_double,
                                          ctypes.POINTER(ctypes.c_double), ctypes.c_int,
                                          ctypes.c_int, ctypes.c_int, ctypes.POINTER(Result)]
        self.dll.rice7_analyse.restype = ctypes.c_int
        self.dll.rice7_free_result.argtypes = [ctypes.POINTER(Result)]
        self.dll.rice7_free_result.restype = None
        self.dll.rice7_curve_count.restype = ctypes.c_int
        self.dll.rice7_curve_name.argtypes = [ctypes.c_int]
        self.dll.rice7_curve_name.restype = ctypes.c_char_p
        self.curve_names = [self.dll.rice7_curve_name(i).decode() for i in range(self.dll.rice7_curve_count())]
        self.dll.rice7_curves.argtypes = [ctypes.c_char_p, ctypes.c_longlong, ctypes.c_double,
                                         ctypes.POINTER(ctypes.c_double), ctypes.c_int, ctypes.c_int,
                                         ctypes.POINTER(ctypes.POINTER(ctypes.c_double)),
                                         ctypes.POINTER(ctypes.c_int), ctypes.POINTER(ctypes.c_double),
                                         ctypes.c_char_p]
        self.dll.rice7_curves.restype = ctypes.c_int
        self.dll.rice7_free.argtypes = [ctypes.POINTER(ctypes.c_double)]
        self.dll.rice7_free.restype = None

    def curves(self, data, thumb, rate=1.0, params=None):
        """(rating, {curve name: numpy array}) for one thumb hold (1 left, 2 right):
        what the strain is made of at every grid point."""
        import numpy as np
        arr = (ctypes.c_double * len(params))(*params) if params is not None else None
        out = ctypes.POINTER(ctypes.c_double)()
        n = ctypes.c_int(0)
        rating = ctypes.c_double(0.0)
        err = ctypes.create_string_buffer(256)
        rc = self.dll.rice7_curves(data, len(data), rate, arr, len(params) if params is not None else 0,
                                  thumb, ctypes.byref(out), ctypes.byref(n), ctypes.byref(rating), err)
        if rc != 0:
            raise RuntimeError(err.value.decode(errors="replace"))
        try:
            k = len(self.curve_names)
            a = np.ctypeslib.as_array(out, shape=(n.value * k,)).reshape(n.value, k).copy()
        finally:
            self.dll.rice7_free(out)
        return rating.value, {name: a[:, i] for i, name in enumerate(self.curve_names)}

    def params(self, base=None, **changes):
        """the published settings (or base) with some replaced by name."""
        p = list(self.defaults if base is None else base)
        for k, v in changes.items():
            p[self.names.index(k)] = v
        return p

    def rate(self, data, rate=1.0, params=None, thumb=0, graph=False):
        r = self.Result()
        arr = None
        if params is not None:
            arr = (ctypes.c_double * len(params))(*params)
        rc = self.dll.rice7_analyse(data, len(data), rate, arr, len(params) if params is not None else 0,
                                   thumb, 1 if graph else 0, ctypes.byref(r))
        try:
            if rc != 0:
                raise RuntimeError(r.error.decode(errors="replace"))
            out = {"rating": r.rating, "thumb_left": r.thumb_left, "thumb_right": r.thumb_right,
                   "keys": r.keys, "n_notes": r.n_notes, "n_holds": r.n_holds,
                   "length_s": (r.last_ms - r.first_ms) / 1000.0}
            if graph:
                out["grid"] = [r.grid[i] for i in range(r.n_points)]
                out["graph"] = [r.graph[i] for i in range(r.n_points)]
            return out
        finally:
            self.dll.rice7_free_result(ctypes.byref(r))


class Xdc1:
    """xdc1.dll, for the 4K regression."""

    def __init__(self, path):
        self.dll = ctypes.CDLL(os.path.abspath(path))
        self.dll.xdc1_skillset_count.restype = ctypes.c_int
        n = self.dll.xdc1_skillset_count()

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

    def rate(self, data, rate=1.0):
        r = self.Result()
        rc = self.dll.xdc1_analyse(data, len(data), rate, 0, ctypes.byref(r))
        try:
            if rc != 0:
                raise RuntimeError(r.error.decode(errors="replace"))
            return {"rating": r.rating, "rating_rice": r.rating_rice, "n_notes": r.n_notes,
                    "n_holds": r.n_holds}
        finally:
            self.dll.xdc1_free_result(ctypes.byref(r))


# ---------------------------------------------------------------------------- .osu files

def read_bytes(path):
    """the file's bytes, or None. tries the long-path form too."""
    for p in (path, "\\\\?\\" + os.path.abspath(path)):
        try:
            with open(p, "rb") as f:
                return f.read()
        except OSError:
            continue
    return None


def find_osu(top):
    out = []
    for root, _, files in os.walk(top):
        out += [os.path.join(root, f) for f in files if f.lower().endswith(".osu")]
    out.sort()
    return out


META_RE = re.compile(rb"^(Title|Version|Mode|CircleSize|OverallDifficulty|Creator|BeatmapID)\s*:\s*(.*)$")


def read_meta(data):
    """header fields as text. keys: the key count (0 if unreadable)."""
    meta = {}
    for line in data.split(b"\n"):
        line = line.strip()
        if line.startswith(b"[HitObjects]"):
            break
        m = META_RE.match(line)
        if m and m.group(1).decode() not in meta:
            meta[m.group(1).decode()] = m.group(2).strip().decode("utf-8", errors="replace")
    try:
        meta["keys"] = int(round(float(meta.get("CircleSize", "0"))))
    except ValueError:
        meta["keys"] = 0
    meta.setdefault("Mode", "0")
    return meta


def note_stats(data):
    """(notes, long notes) of the hit objects, without rating anything."""
    i = data.find(b"[HitObjects]")
    if i < 0:
        return 0, 0
    n = ln = 0
    for line in data[i:].split(b"\n")[1:]:
        f = line.strip().split(b",")
        if len(f) < 4:
            continue
        try:
            n += 1
            ln += 1 if int(f[3]) & 128 else 0
        except ValueError:
            pass
    return n, ln


def remap_columns(data, mapping, new_keys):
    """the chart with every note moved from column c to mapping[c] on a new_keys playfield.
    everything else (timing, header) is kept byte for byte."""
    i = data.find(b"[HitObjects]")
    if i < 0:
        raise ValueError("no [HitObjects]")
    head, body = data[:i], data[i:]
    keys = read_meta(data)["keys"]
    head, n = re.subn(rb"(?m)^(\s*CircleSize\s*:\s*)[^\r\n]*", lambda m: m.group(1) + str(new_keys).encode(), head)
    if n != 1:
        raise ValueError("no CircleSize line")
    lines = body.split(b"\n")
    out = [lines[0]]
    for line in lines[1:]:
        f = line.split(b",")
        if len(f) < 4:
            out.append(line)
            continue
        col = min(max(int(math.floor(float(f[0]) * keys / 512.0)), 0), keys - 1)
        f[0] = str(int((mapping[col] + 0.5) * 512.0 / new_keys)).encode()
        out.append(b",".join(f))
    return head + b"\n".join(out)


def mirror(data):
    keys = read_meta(data)["keys"]
    return remap_columns(data, [keys - 1 - c for c in range(keys)], keys)


# ---------------------------------------------------------------------------- order statistics

def wrong_pairs(labels, values, min_gap=0.0):
    """of every two charts whose labels differ (by more than min_gap), the share the value
    puts in the wrong order. a tie counts half. returns (share, pairs)."""
    import numpy as np
    lab = np.asarray(labels, dtype=float)
    val = np.asarray(values, dtype=float)
    order = np.argsort(lab, kind="stable")
    lab, val = lab[order], val[order]
    bad = 0.0
    tot = 0
    for i in range(len(lab) - 1):
        hi = lab[i + 1:] - lab[i] > min_gap
        k = int(hi.sum())
        if not k:
            continue
        v = val[i + 1:][hi]
        bad += float((v < val[i]).sum()) + 0.5 * float((v == val[i]).sum())
        tot += k
    return (bad / tot if tot else float("nan")), tot


def spearman(a, b):
    import numpy as np

    def rank(v):
        v = np.asarray(v, dtype=float)
        o = np.argsort(v, kind="stable")
        r = np.empty(len(v))
        i = 0
        while i < len(o):
            j = i
            while j + 1 < len(o) and v[o[j + 1]] == v[o[i]]:
                j += 1
            r[o[i:j + 1]] = (i + j) / 2.0
            i = j + 1
        return r

    if len(a) < 3:
        return float("nan")
    ra, rb = rank(a), rank(b)
    ra -= ra.mean()
    rb -= rb.mean()
    den = math.sqrt(float((ra * ra).sum()) * float((rb * rb).sum()))
    return float((ra * rb).sum()) / den if den else 0.0
