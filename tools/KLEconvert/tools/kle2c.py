#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""kle2c.py —— 把 keyboard-layout-editor.com 的 Raw data 批量转成固件用的布局数组。

默认批量模式：
    读取  ./layout_json/*.json
    输出  ./output/<同名>.h  （每键一行，可直接粘进固件的 LayoutManager.h）
    以及  ./output/summary.txt

单位与协议一致：0.25u 为一个整数单位（4 = 1u），左上角为 (0,0)。
输出顺序为「从上到下、从左到右」，必须与固件 key_map 的按键编号顺序一致。

用法：
    python kle2c.py                      批量：layout_json -> output
    python kle2c.py --dir other --out-dir other_out
    python kle2c.py layout.json           单文件：打印到 stdout
    python kle2c.py layout.json -o x.h    单文件：写入指定文件

    python kle2c.py layout.json --expect 14   单文件模式下校验按键数量
    python kle2c.py --emit-rotation           额外输出旋转组表（二期）

支持：
    · 行数组 + 键对象（w/h/x/y/r/rx/ry）
    · 顶层元数据对象（例如 {"r":15,"rx":2,"ry":1}），旋转会延续到后续按键
    · 旋转键自动置 flags 最高两位为 1
    · 坐标不是 0.25u 整数倍时四舍五入并警告
"""

import argparse
import json
import os
import re
import sys
import time

UNITS_PER_U = 4          # 0.25u 一个单位
FLAG_ROTATE = 0xC0       # 最高两位 = 11 表示该键需要旋转

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_IN = os.path.join(HERE, "layout_json")
DEFAULT_OUT = os.path.join(HERE, "output")


class Key:
    __slots__ = ("index", "label", "x", "y", "w", "h", "r", "rx", "ry", "flags")

    def __init__(self, label, x, y, w, h, r, rx, ry):
        self.index = -1
        self.label = label
        self.x, self.y, self.w, self.h = x, y, w, h
        self.r, self.rx, self.ry = r, rx, ry
        self.flags = 0


def parse_kle(raw):
    """把 KLE raw JSON 解析成按顺序排列的按键列表。"""
    keys = []
    meta = {"r": 0.0, "rx": 0.0, "ry": 0.0}

    if isinstance(raw, dict):
        raw = raw.get("keys") or raw.get("layout") or [raw]

    for row_index, row in enumerate(raw):
        if isinstance(row, dict):
            for k in ("r", "rx", "ry"):
                if k in row:
                    meta[k] = float(row[k])
            continue
        if not isinstance(row, (list, tuple)):
            continue

        x_cursor = 0.0
        pending = {}
        for item in row:
            if isinstance(item, dict):
                for k in ("x", "y", "w", "h", "r", "rx", "ry", "name", "c", "t", "a"):
                    if k in item:
                        pending[k] = item[k]
                # 旋转与旋转中心在 KLE 里是"延续"的，直到被下一个对象改写
                for k in ("r", "rx", "ry"):
                    if k in item:
                        meta[k] = float(item[k])
                continue

            label = "" if item is None else str(item)
            w = float(pending.get("w", 1.0))
            h = float(pending.get("h", 1.0))
            x = x_cursor + float(pending.get("x", 0.0))
            y = float(row_index) + float(pending.get("y", 0.0))
            r = float(pending.get("r", meta["r"]))
            rx = float(pending.get("rx", meta["rx"]))
            ry = float(pending.get("ry", meta["ry"]))

            keys.append(Key(label, x, y, w, h, r, rx, ry))
            x_cursor = x + w
            pending = {}

    for i, k in enumerate(keys):
        k.index = i
        if abs(k.r) > 1e-9:
            k.flags |= FLAG_ROTATE
    return keys


def to_units(value, name, warnings, label):
    u = int(round(value * UNITS_PER_U))
    if abs(value * UNITS_PER_U - u) > 1e-6:
        warnings.append("%s: %s=%.3fu 不是 0.25u 的整数倍，已四舍五入为 %.2fu"
                        % (label, name, value, u / float(UNITS_PER_U)))
    return u


IDX_RE = re.compile(r"(?:^|\n)\s*#\s*(\d+)")


def label_index_count(keys):
    return sum(1 for k in keys if IDX_RE.search(k.label or ""))


def apply_label_index(keys):
    """用标签里的 #N 作为按键编号（移动键位后编号不变）。

    要求：每个键的标签都带 #N，且编号正好覆盖 0..N-1 不重复。
    """
    idxs = []
    for k in keys:
        m = IDX_RE.search(k.label or "")
        idxs.append(int(m.group(1)) if m else None)

    missing = [k.index for k, i in zip(keys, idxs) if i is None]
    if missing:
        raise ValueError("--index-from-label: 这些键的标签里没有 #N（位置 %s）" % missing[:10])

    seen = {}
    for i in idxs:
        seen[i] = seen.get(i, 0) + 1
    dup = sorted(i for i, c in seen.items() if c > 1)
    if dup:
        raise ValueError("--index-from-label: 编号重复：%s" % dup[:10])

    n = len(keys)
    if set(idxs) != set(range(n)):
        raise ValueError("--index-from-label: 编号必须是 0..%d 的连续整数，实际为 %s"
                         % (n - 1, sorted(set(idxs))[:12]))

    for k, i in zip(keys, idxs):
        k.index = i
    keys.sort(key=lambda k: k.index)


def c_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")


def build(keys, array_name, source_name, emit_rotation, index_note=None):
    warnings = []
    rows = []
    max_x = max_y = 0.0
    rot_groups = {}

    for k in keys:
        x = to_units(k.x, "x", warnings, k.label)
        y = to_units(k.y, "y", warnings, k.label)
        w = to_units(k.w, "w", warnings, k.label)
        h = to_units(k.h, "h", warnings, k.label)
        if w < 1 or h < 1:
            warnings.append("%s: w/h 过小（%d/%d），按 1 处理" % (k.label, w, h))
            w, h = max(w, 1), max(h, 1)
        if x < 0 or y < 0:
            warnings.append("%s: 坐标为负（%d/%d），请检查 KLE 数据" % (k.label, x, y))
        max_x = max(max_x, x + w)
        max_y = max(max_y, y + h)
        rows.append((k, x, y, w, h))

        if k.r:
            group = (round(k.rx, 4), round(k.ry, 4), round(k.r, 4))
            rot_groups.setdefault(group, []).append(k.index)

    bbox_w = max_x / float(UNITS_PER_U)
    bbox_h = max_y / float(UNITS_PER_U)

    out = []
    out.append("/* Generated by tools/kle2c.py from %s on %s."
               % (os.path.basename(source_name), time.strftime("%Y-%m-%d")))
    out.append(" * %d keys, bounding box %.2fu x %.2fu (%d x %d units of 0.25u)."
               % (len(keys), bbox_w, bbox_h, max_x, max_y))
    if index_note:
        out.append(" * Key index: %s." % index_note)
    else:
        out.append(" * Order: top to bottom, left to right. Keep key_map indices identical.")
    out.append(" * Paste the array below into LayoutManager.h (replace layout_custom).")
    out.append(" */")
    out.append("const KeyLayoutItem %s[] PROGMEM = {" % array_name)
    for k, x, y, w, h in rows:
        out.append("    { %3d, %3d, %3d, %3d, 0x%02X },  /* %3d: %-10s x=%.2fu y=%.2fu w=%.2fu h=%.2fu%s */"
                   % (x, y, w, h, k.flags, k.index, c_escape(k.label)[:10],
                      x / 4.0, y / 4.0, w / 4.0, h / 4.0,
                      ("  ROT %.1fdeg" % k.r) if k.r else ""))
    out.append("};")
    out.append("#define %s_COUNT (sizeof(%s) / sizeof(%s[0]))"
               % (array_name.upper(), array_name, array_name))

    if emit_rotation and rot_groups:
        out.append("")
        out.append("/* Rotation groups (phase 2): keys whose flags are 0xC0. */")
        out.append("const LayoutRotation %s_rotation[] PROGMEM = {" % array_name)
        for (rx, ry, r), members in rot_groups.items():
            out.append("    { %3d, %3d, %3d, %2d, %3d, %3d },  /* r=%.1f, rx=%.2fu, ry=%.2fu, %d keys */"
                       % (members[0], int(round(rx * 4)), int(round(ry * 4)),
                          int(round(r * 2)), 0, len(members), r, rx, ry, len(members)))
        out.append("};")

    return "\n".join(out) + "\n", warnings, bbox_w, bbox_h, len(rot_groups)


def convert_one(path, array_name, emit_rotation, expect=None, index_mode="auto"):
    """转换单个文件，返回 (text, warnings, bbox_w, bbox_h, key_count, rotation_groups)。"""
    with open(path, "r", encoding="utf-8-sig") as f:
        raw = json.load(f)
    keys = parse_kle(raw)
    if not keys:
        raise ValueError("no keys parsed")
    labeled = label_index_count(keys)
    note = None
    if index_mode == "label" or (index_mode == "auto" and labeled == len(keys)):
        apply_label_index(keys)
        note = "from #N in key labels"
    elif index_mode == "auto" and 0 < labeled < len(keys):
        raise ValueError("部分按键标签带 #N、部分没有（%d/%d）；请全部加上或全部去掉"
                         % (labeled, len(keys)))
    if expect is not None and len(keys) != expect:
        raise ValueError("parsed %d keys, expected %d" % (len(keys), expect))
    text, warnings, bw, bh, rot = build(keys, array_name, path, emit_rotation, note)
    return text, warnings, bw, bh, len(keys), rot


def run_batch(indir, outdir, array_name, emit_rotation, quiet, index_mode="auto"):
    if not os.path.isdir(indir):
        print("error: input folder not found: %s" % indir, file=sys.stderr)
        return 2

    files = sorted(f for f in os.listdir(indir) if f.lower().endswith(".json"))
    if not files:
        print("error: no .json files in %s" % indir, file=sys.stderr)
        return 2

    os.makedirs(outdir, exist_ok=True)

    results = []
    failures = 0
    for name in files:
        src = os.path.join(indir, name)
        stem = os.path.splitext(name)[0]
        dst = os.path.join(outdir, stem + ".h")
        try:
            text, warnings, bw, bh, count, rot = convert_one(src, array_name, emit_rotation, index_mode=index_mode)
            with open(dst, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
            results.append((name, count, bw, bh, count * 5, rot, warnings))
            if not quiet:
                print("OK   %-28s %3d keys  %5.2fu x %4.2fu  -> %s"
                      % (name, count, bw, bh, os.path.basename(dst)))
                for w in warnings:
                    print("     warning: " + w)
        except Exception as ex:
            failures += 1
            results.append((name, None, 0, 0, 0, 0, [str(ex)]))
            print("FAIL %-28s %s" % (name, ex), file=sys.stderr)

    summary = os.path.join(outdir, "summary.txt")
    with open(summary, "w", encoding="utf-8", newline="\n") as f:
        f.write("# kle2c batch summary  %s\n" % time.strftime("%Y-%m-%d %H:%M"))
        f.write("# source: %s\n\n" % indir)
        f.write("%-28s %5s %16s %8s %5s %s\n" % ("file", "keys", "bbox (u)", "bytes", "rot", "notes"))
        for name, count, bw, bh, nbytes, rot, warnings in results:
            if count is None:
                f.write("%-28s  ERR  %s\n" % (name, warnings[0] if warnings else ""))
            else:
                f.write("%-28s %5d %6.2f x %5.2f %8d %5d %s\n"
                        % (name, count, bw, bh, nbytes, rot,
                           ("; ".join(warnings))[:80]))

    print("\n%d file(s): %d ok, %d failed" % (len(files), len(files) - failures, failures))
    print("output: %s" % outdir)
    print("summary: %s" % summary)
    return 0 if failures == 0 else 1


def main(argv=None):
    ap = argparse.ArgumentParser(
        description="Convert KLE raw JSON to firmware layout tables (batch by default).")
    ap.add_argument("input", nargs="?", help="single JSON file (omit for batch mode)")
    ap.add_argument("-o", "--output", help="single-file mode: write here instead of stdout")
    ap.add_argument("--dir", default=DEFAULT_IN, help="batch input folder (default: ./layout_json)")
    ap.add_argument("--out-dir", default=DEFAULT_OUT, help="batch output folder (default: ./output)")
    ap.add_argument("--array-name", default="layout_custom", help="C array name (default: layout_custom)")
    ap.add_argument("--expect", type=int, default=None, help="single-file mode: require this key count")
    ap.add_argument("--emit-rotation", action="store_true", help="also emit rotation group table")
    ap.add_argument("--index-mode", choices=["auto", "label", "position"], default="auto",
                    help="key index source: auto (default), #N labels, or position order")
    ap.add_argument("--quiet", action="store_true", help="suppress per-file output")
    args = ap.parse_args(argv)

    if args.input is None:
        return run_batch(args.dir, args.out_dir, args.array_name, args.emit_rotation, args.quiet,
                         args.index_mode)

    try:
        text, warnings, bw, bh, count, rot = convert_one(
            args.input, args.array_name, args.emit_rotation, args.expect, args.index_mode)
    except Exception as ex:
        print("error: %s" % ex, file=sys.stderr)
        return 2

    if args.output:
        with open(args.output, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    else:
        sys.stdout.write(text)

    if not args.quiet:
        print("keys: %d   bbox: %.2fu x %.2fu   bytes: %d   rotation groups: %d"
              % (count, bw, bh, count * 5, rot), file=sys.stderr)
        for w in warnings:
            print("warning: " + w, file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
