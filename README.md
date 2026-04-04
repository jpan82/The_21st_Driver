# The 21st Driver

Unity telemetry replay project: cars follow time-sampled trajectories from CSV, with optional track ribbon generation and visual-only wheel/steer/body-roll control.

## Project Structure

- `Assets/Scripts/Replay`: shared replay pipeline
  - `Data`: `ReplaySession`, `DriverReplayTrack`, `ReplaySample`
  - `Importers`: `FastF1CsvImporter`
  - `Playback`: `TrajectorySampler`
  - `Track`: `TrackRibbonMeshFromCsv`
  - `Visuals`: `ReplayVehicleVisualController`
- `Assets/Scripts/Gameplay`: session orchestration (`Race_Controller`, `SmoothMover`)
- `Assets/Scripts/Vehicles`: standalone vehicle playback (`CSVMovementPlayer`, `F1_Driver_Follower`)
- `Assets/Scripts/Camera`: follow/top camera (`F1_TopDownCamera`)
- `Assets/Models`: meshes, prefabs, materials, and other art assets

## Assemblies

- `The21stDriver.Replay`
- `The21stDriver.Gameplay` (references Replay)
- `The21stDriver.Vehicles` (references Replay)
- `The21stDriver.Camera` (references Gameplay + Vehicles)

## Data Flow

1. CSV files under `StreamingAssets` provide track and driver telemetry.
2. Importers build replay tracks (`DriverReplayTrack`).
3. `TrajectorySampler` evaluates position/forward by session time.
4. Movement scripts update transforms each frame.
5. `TrackRibbonMeshFromCsv` generates ribbon mesh when needed.
6. `ReplayVehicleVisualController` adds optional visual motion on child nodes.

## Documentation

- Chinese: [README_CN.md](README_CN.md)
- Replay module notes: [Assets/Scripts/Replay/README.md](Assets/Scripts/Replay/README.md)
