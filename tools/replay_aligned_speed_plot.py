#!/usr/bin/env python3
"""
从 motion dump CSV 读取 x_aligned / y_aligned，计算相邻样本的
sqrt(dx^2+dy^2)/dt，并绘制速度随时间变化，用于排查 replay 不连贯是否来自数据。
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "csv",
        type=Path,
        help="例如 Assets/StreamingAssets/f1_motion_dump_with_time/ALO.csv",
    )
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        default=None,
        help="输出 PNG 路径（默认与 CSV 同目录，文件名加 _aligned_speed.png）",
    )
    parser.add_argument(
        "--max-rows",
        type=int,
        default=None,
        help="仅读前 N 行（调试用）",
    )
    args = parser.parse_args()

    try:
        import matplotlib

        matplotlib.use("Agg")
        import numpy as np
        import pandas as pd
        import matplotlib.pyplot as plt
    except ImportError as e:
        print("需要安装: pip install pandas matplotlib numpy", file=sys.stderr)
        raise SystemExit(1) from e

    csv_path = args.csv.resolve()
    if not csv_path.is_file():
        print(f"文件不存在: {csv_path}", file=sys.stderr)
        return 1

    # 表头里 x_aligned / y_aligned 各出现两次，用列位置取第一组
    usecols = [2, 9, 11]  # Time, x_aligned, y_aligned (first occurrences)
    df = pd.read_csv(
        csv_path,
        usecols=usecols,
        nrows=args.max_rows,
        dtype={usecols[1]: float, usecols[2]: float},
    )
    # 读入后列名可能是 Time, x_aligned, y_aligned（若仍重复则带 .1）
    time_col = df.columns[0]
    x_col = [c for c in df.columns if str(c).startswith("x_aligned")][0]
    y_col = [c for c in df.columns if str(c).startswith("y_aligned")][0]

    t = pd.to_timedelta(df[time_col], errors="coerce")
    if t.isna().all():
        print("无法解析 Time 列为 timedelta，请检查 CSV。", file=sys.stderr)
        return 1

    t_sec = t.dt.total_seconds().to_numpy(dtype=np.float64)
    x = df[x_col].to_numpy(dtype=np.float64)
    y = df[y_col].to_numpy(dtype=np.float64)

    dt = np.diff(t_sec)
    dx = np.diff(x)
    dy = np.diff(y)
    # 避免除零
    eps = 1e-12
    dt_safe = np.where(np.abs(dt) < eps, np.nan, dt)
    speed = np.sqrt(dx * dx + dy * dy) / dt_safe
    t_mid = 0.5 * (t_sec[1:] + t_sec[:-1])

    out = args.output
    if out is None:
        out = csv_path.with_name(csv_path.stem + "_aligned_speed.png")

    fig, axes = plt.subplots(2, 1, figsize=(14, 7), sharex=True, constrained_layout=True)

    axes[0].plot(t_mid, speed, lw=0.6, color="C0", alpha=0.85, label="|d aligned| / dt")
    axes[0].set_ylabel("speed (aligned units / s)")
    axes[0].set_title(f"Replay-aligned speed — {csv_path.name}")
    axes[0].grid(True, alpha=0.3)
    axes[0].legend(loc="upper right")

    # 二阶：速度变化率（抖动/尖峰更明显）
    dspeed = np.diff(speed)
    dt2 = np.diff(t_mid)
    dt2_safe = np.where(np.abs(dt2) < eps, np.nan, dt2)
    accel = dspeed / dt2_safe
    t_acc = 0.5 * (t_mid[1:] + t_mid[:-1])
    axes[1].plot(t_acc, accel, lw=0.5, color="C1", alpha=0.8, label="d(speed)/dt")
    axes[1].set_ylabel("accel (aligned / s²)")
    axes[1].set_xlabel("Time (s, from CSV Time column)")
    axes[1].grid(True, alpha=0.3)
    axes[1].legend(loc="upper right")

    # 在图上标注 dt 异常点
    bad_dt = np.abs(dt) < 1e-9
    if np.any(bad_dt):
        axes[0].text(
            0.02,
            0.98,
            f"警告: {int(np.sum(bad_dt))} 处 dt≈0",
            transform=axes[0].transAxes,
            va="top",
            color="red",
            fontsize=10,
        )

    fig.savefig(out, dpi=160)
    print(f"已保存: {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
