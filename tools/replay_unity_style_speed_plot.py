#!/usr/bin/env python3
"""
与 Unity FastF1CsvImporter 一致：SessionTime + x_ref/y_ref 的几何速度，
并叠画 CSV Speed 列（阶段 0 诊断）。
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("csv", type=Path)
    parser.add_argument("-o", "--output", type=Path, default=None)
    parser.add_argument("--max-rows", type=int, default=None)
    args = parser.parse_args()

    try:
        import matplotlib

        matplotlib.use("Agg")
        import numpy as np
        import pandas as pd
        import matplotlib.pyplot as plt
    except ImportError:
        print("pip install pandas matplotlib numpy", file=sys.stderr)
        return 1

    csv_path = args.csv.resolve()
    if not csv_path.is_file():
        print(f"文件不存在: {csv_path}", file=sys.stderr)
        return 1

    df = pd.read_csv(
        csv_path,
        usecols=lambda c: c in ("SessionTime", "x_ref", "y_ref", "Speed"),
        nrows=args.max_rows,
    )
    t = pd.to_timedelta(df["SessionTime"], errors="coerce")
    if t.isna().all():
        print("无法解析 SessionTime", file=sys.stderr)
        return 1

    t_sec = t.dt.total_seconds().to_numpy(dtype=np.float64)
    t_sec = t_sec - t_sec[0]
    x = df["x_ref"].to_numpy(dtype=np.float64)
    y = df["y_ref"].to_numpy(dtype=np.float64)

    dt = np.diff(t_sec)
    eps = 1e-12
    dt_safe = np.where(np.abs(dt) < eps, np.nan, dt)
    v_geom = np.sqrt(np.diff(x) ** 2 + np.diff(y) ** 2) / dt_safe
    t_mid = 0.5 * (t_sec[1:] + t_sec[:-1])

    out = args.output
    if out is None:
        out = csv_path.with_name(csv_path.stem + "_unity_ref_speed.png")

    fig, ax = plt.subplots(figsize=(14, 4.5), constrained_layout=True)
    ax.plot(t_mid, v_geom, lw=0.55, color="C0", alpha=0.85, label="|d ref| / dt (Unity xz)")
    if "Speed" in df.columns:
        sp = df["Speed"].to_numpy(dtype=np.float64)
        t_sp = t_sec
        ax.plot(t_sp, sp, lw=0.45, color="C2", alpha=0.75, label="CSV Speed")
    ax.set_ylabel("m/s (approx)")
    ax.set_xlabel("Session time (s, zeroed)")
    ax.set_title(f"Unity-style path + Speed — {csv_path.name}")
    ax.grid(True, alpha=0.3)
    ax.legend(loc="upper right")
    fig.savefig(out, dpi=160)
    print(f"已保存: {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
