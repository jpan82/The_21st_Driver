#!/usr/bin/env python3
"""打印与 Unity 导入一致的几何速度 vs CSV Speed 的简单 QA 指标（无图）。"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("csv", type=Path)
    p.add_argument("--max-rows", type=int, default=None)
    args = p.parse_args()

    try:
        import numpy as np
        import pandas as pd
    except ImportError:
        print("pip install pandas numpy", file=sys.stderr)
        return 1

    path = args.csv.resolve()
    if not path.is_file():
        print(f"not found: {path}", file=sys.stderr)
        return 1

    df = pd.read_csv(
        path,
        usecols=lambda c: c in ("SessionTime", "x_ref", "y_ref", "Speed"),
        nrows=args.max_rows,
    )
    if df.shape[1] < 3:
        print("missing SessionTime / x_ref / y_ref columns", file=sys.stderr)
        return 1

    t = pd.to_timedelta(df["SessionTime"], errors="coerce").dt.total_seconds().to_numpy()
    t = t - t[0]
    x = df["x_ref"].to_numpy(dtype=np.float64)
    y = df["y_ref"].to_numpy(dtype=np.float64)
    dt = np.diff(t)
    mask = np.abs(dt) > 1e-9
    v_g = np.full(len(dt), np.nan)
    v_g[mask] = np.sqrt(np.diff(x)[mask] ** 2 + np.diff(y)[mask] ** 2) / dt[mask]
    t_mid = 0.5 * (t[1:] + t[:-1])

    has_speed = "Speed" in df.columns
    if has_speed:
        sp = df["Speed"].to_numpy(dtype=np.float64)
        v_s = 0.5 * (sp[:-1] + sp[1:])
        valid = mask & np.isfinite(v_g) & np.isfinite(v_s) & (v_s > 0.5)
        rel = np.abs(v_g[valid] - v_s[valid]) / np.maximum(v_s[valid], 0.5)
        print(f"rows={len(df)} segments={int(np.sum(mask))}")
        print(f"v_geom max={np.nanmax(v_g):.3f} p99={np.nanpercentile(v_g[mask], 99):.3f}")
        if np.any(valid):
            print(
                f"|v_geom-Speed|/Speed median={np.median(rel):.3f} "
                f"p95={np.percentile(rel, 95):.3f} max_ratio={np.max(rel):.3f}"
            )
    else:
        print(f"rows={len(df)} v_geom max={np.nanmax(v_g):.3f}")

    bad_dt = int(np.sum(np.abs(dt) <= 1e-9))
    if bad_dt:
        print(f"warn: near-zero dt segments={bad_dt}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
