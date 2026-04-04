# The 21st Driver

这是一个 Unity 遥测回放项目：车辆根据 CSV 时间采样轨迹回放，可选生成赛道带状网格，并可叠加轮速/转向/侧倾等纯视觉表现。

## 项目结构

- `Assets/Scripts/Replay`：共享回放管线
  - `Data`：`ReplaySession`、`DriverReplayTrack`、`ReplaySample`
  - `Importers`：`FastF1CsvImporter`
  - `Playback`：`TrajectorySampler`
  - `Track`：`TrackRibbonMeshFromCsv`
  - `Visuals`：`ReplayVehicleVisualController`
- `Assets/Scripts/Gameplay`：会话编排（`Race_Controller`、`SmoothMover`）
- `Assets/Scripts/Vehicles`：单车回放行为（`CSVMovementPlayer`、`F1_Driver_Follower`）
- `Assets/Scripts/Camera`：俯视/跟车相机（`F1_TopDownCamera`）
- `Assets/Models`：模型、Prefab、材质等资源

## 程序集拆分

- `The21stDriver.Replay`
- `The21stDriver.Gameplay`（引用 Replay）
- `The21stDriver.Vehicles`（引用 Replay）
- `The21stDriver.Camera`（引用 Gameplay + Vehicles）

## 数据流

1. `StreamingAssets` 下的 CSV 提供赛道与车手轨迹数据。
2. Importer 构建 `DriverReplayTrack`。
3. `TrajectorySampler` 按时间求位置与前向。
4. 运动脚本每帧更新 Transform。
5. 需要时用 `TrackRibbonMeshFromCsv` 生成赛道带状网格。
6. `ReplayVehicleVisualController` 在子节点上叠加可选视觉效果。

## 文档索引

- English: [README.md](README.md)
- Replay 模块说明: [Assets/Scripts/Replay/README_CN.md](Assets/Scripts/Replay/README_CN.md)
