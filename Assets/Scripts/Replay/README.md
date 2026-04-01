# Replay Pipeline Notes

This project's replay system is driven by a shared replay data pipeline instead of moving cars directly from raw CSV rows.

## Current flow

1. `FastF1CsvImporter` loads FastF1 position CSV files into a shared `ReplaySession`.
2. Each driver track is stored as a `DriverReplayTrack` containing timed `ReplaySample` points.
3. `TrajectorySampler` evaluates the track using time-based sampling.
4. Cars query the sampler every frame for:
   - position
   - forward direction
5. Track lines are also drawn from resampled points so the visible path matches the driven path.

## Current interpolation method

The replay no longer uses simple linear interpolation between two CSV points.

It now uses:

- time-based sampling
- centripetal Catmull-Rom spline interpolation
- tangent-based forward sampling for vehicle heading

This was chosen because FastF1 position samples are not perfectly uniform, and centripetal Catmull-Rom is more stable than uniform Catmull-Rom for uneven point spacing.

## Why this matters

Compared with direct point-to-point lerp playback, this approach gives:

- smoother cornering
- less overshoot and fewer sudden twitches
- a cleaner foundation for later work such as wheel rotation, steering visuals, and timeline controls
