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

## ReplayVehicleVisualController

`ReplayVehicleVisualController` is an optional visual-only layer for replay cars.

It does not move the car. The replay scripts still control world position and heading.

### What to bind

- `visualRoot`
  - The car body visual root.
  - This is the node that receives body roll.
- `frontLeftSteerPivot`
  - The front-left steering pivot node.
- `frontRightSteerPivot`
  - The front-right steering pivot node.
- `frontLeftWheel`
  - The actual front-left wheel mesh or wheel visual node.
- `frontRightWheel`
  - The actual front-right wheel mesh or wheel visual node.
- `rearLeftWheel`
  - The actual rear-left wheel mesh or wheel visual node.
- `rearRightWheel`
  - The actual rear-right wheel mesh or wheel visual node.

### Important note about model structure

This controller assumes the model has:

- a clean body node
- separate front steering pivots
- separate wheel nodes that can rotate independently

If the imported car model mixes body, suspension, and wheel nodes together, enabling steering or wheel spin can rotate the wrong part of the car.

That is why the current defaults are conservative:

- `enableWheelSpin = false`
- `enableSteering = false`
- `enableBodyRoll = false`

### Suggested workflow

1. First confirm the replay car moves correctly without the visual controller features enabled.
2. Then test `enableSteering` only.
3. Then test `enableBodyRoll` only.
4. Only enable `enableWheelSpin` when the model has verified wheel-only nodes.
