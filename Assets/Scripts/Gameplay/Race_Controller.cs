using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using The21stDriver.Replay.Data;
using The21stDriver.Replay.Importers;
using The21stDriver.Replay.Track;

namespace The21stDriver.Gameplay
{
    public class Race_Controller : MonoBehaviour
    {
        [Header("Assets")]
        public GameObject carPrefab;
        public Material trackMaterial;

        [Header("Player Car")]
        [Tooltip("Prefab for the player-controlled car. Falls back to carPrefab if empty.")]
        public GameObject playerCarPrefab;
        [Tooltip("Spawn a player-controlled car at the front of the grid.")]
        public bool spawnPlayer = true;
        [Tooltip("Grid slot reserved for the player. 0 = pole position.")]
        public int playerGridIndex = 0;
        [Tooltip("Extra Y offset applied only to the player car spawn. Negative moves it down.")]
        public float playerSpawnHeightOffset = 0f;

        [Header("Files")]
        public string trackFolder = "track_data";
        public string trackFileName = "Silverstone.csv";
        public string carFolder = "f1_motion_dump";
        [Tooltip("Max number of cars to spawn. Set to 0 to spawn all.")]
        public int maxCars = 0;

        [Header("Playback")]
        [Tooltip("Only for narrow 7-column reference CSV. Full-race CSV uses SessionTime column.")]
        public float sampleInterval = 0.05f;
        [Range(0.1f, 50f)]
        public float speedMultiplier = 1f;

        [Header("Car Alignment")]
        public float carYOffset = 0.25f;
        public float rotationSmoothness = 10f;
        public float fixedXRotation = -90f;
        [Tooltip("Spawn replay cars slightly above the track so they settle visually instead of snapping.")]
        public float spawnHeightOffset = 0.8f;

        [Header("Half-Physics Y")]
        [Tooltip("Use ground raycast + spring settling for NPC cars instead of hard Y snapping.")]
        public bool useGroundSpring = true;
        public float groundRaycastStartHeight = 20f;
        public float groundRaycastMaxDistance = 60f;
        public float groundSpringStrength = 32f;
        public float groundSpringDamping = 8f;
        public float groundSnapEpsilon = 0.01f;
        public float groundSnapVelocity = 0.02f;

        // 赛道丝带网格与半宽倍率：BuildTrack、TrackRibbonMeshFromCsv；UV 见 trackUvMetersPerRepeat。
        [Header("Track Visual")]
        public float trackUvMetersPerRepeat = 12f;
        public float trackRibbonWidthMultiplier = 1.5f;

        // 草地平面、混凝土护栏、Kenney 围栏、轮胎堆：BuildTracksideDecor、SpawnBarrierRow、SpawnTireStack。
        [Header("Trackside Decor")]
        public bool buildTracksideDecor = true;
        public float decorSpacingMeters = 24f;
        public float barrierOffsetMeters = 14f;
        public float fenceOffsetMeters = 18f;
        public float decorBarrierClearanceBeyondAsphaltMeters = 2.5f;
        public float decorFenceClearanceBeyondAsphaltMeters = 8f;
        public float decorTireClearanceBeyondAsphaltMeters = 3f;
        public float decorSponsorClearanceBeyondAsphaltMeters = 9f;
        public float groundPaddingMeters = 80f;
        public float barrierLengthMeters = 10f;
        public float barrierHeightMeters = 1.25f;
        public float barrierThicknessMeters = 0.7f;
        public float fenceVerticalOffset = 2.15f;
        public float tireStackOffsetMeters = 20f;
        [SerializeField] private float fencePrefabYawOffsetDegrees = 90f;
        [SerializeField] private float curvedFenceRightSideExtraYawDegrees = 180f;
        public bool enableCurvedFencePrefabs = false;
        public float curvedFenceMaxCornerDeg = 8f;
        public float cornerFenceExtraOutwardMeters = 2f;

        // 起终点线、发车格、龙门架、维修墙、赞助牌：BuildF1EventLandmarks 及子方法。
        [Header("F1 Event Dressing")]
        public bool buildF1EventLandmarks = true;
        public bool anchorStartFromCarReplay = true;
        public int gridRows = 12;
        public float gridSpacingMeters = 9f;
        public float gridBoxLengthMeters = 5.5f;
        public float gridBoxWidthMeters = 2.4f;
        public float pitWallLengthMeters = 220f;
        public float pitWallOffsetMeters = 11.5f;
        public float pitBuildingOffsetMeters = 26f;
        public float pitWallClearanceBeyondAsphaltMeters = 3f;
        public float pitPaddockBeyondWallMeters = 12f;
        public int sponsorBoardCount = 24;

        // 弯道刹车牌、马修塔、赛段横幅门：BuildRaceOperationsProps。
        [Header("F1 Operations Props")]
        public bool buildRaceOperationsProps = true;
        public bool buildBrakingBoards = false;
        public float cornerDetectionAngleDeg = 14f;
        public float cornerPropsMinSpacingMeters = 45f;
        public float brakingBoardOffsetMeters = 8f;
        public float marshalPostOffsetMeters = 10f;
        public float operationsClearanceBeyondAsphaltMeters = 3f;
        public float bannerGateClearanceBeyondAsphaltMeters = 2.5f;
        public int bannerGateCount = 4;
        public Color marshalFlagColor = new Color(0.95f, 0.78f, 0.12f, 1f);

        // 生成后整体外推，避免低位物体压赛道：EnforceTrackClearanceForAllProps、TryKeepBlockOutsideTrack、CreateBlock。
        [Header("Track Clearance")]
        public bool enforceNoPropsOnTrack = true;
        public float enforceTrackClearanceExtraMeters = 0.6f;
        public float fallbackTrackHalfWidthMeters = 14f;

        // Kenney 围栏网格/pivot 相对赛道的额外外移量：SpawnBarrierRow。
        [Header("Fence / Barrier geometry")]
        public float fencePrefabAdditionalOutwardMeters = 1.35f;

        private Vector3 globalOffset;
        private float globalTime;
        private Transform tracksideDecorRoot;
        private RacePropFactory propFactory;
        private Vector3 gridAnchor;
        private Vector3 gridForward;
        private Vector3 gridRight;

        public float GlobalTime  => globalTime;
        public bool  RaceStarted => Time.timeSinceLevelLoad >= 5f;

        void Start() {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            string tPath = Path.Combine(Application.streamingAssetsPath, trackFolder, trackFileName);
            string cDirPath = Path.Combine(Application.streamingAssetsPath, carFolder);

            if (!File.Exists(tPath)) return;

            string[] tLines = File.ReadAllLines(tPath);
            if (!TryParseTrackGlobalOffset(tLines, tPath, out globalOffset))
            {
                return;
            }

            BuildTrack(tLines);

            if (spawnPlayer)
                SpawnPlayerCar(playerGridIndex);

            if (Directory.Exists(cDirPath)) {
                string[] files = Directory.GetFiles(cDirPath, "*.csv");
                System.Array.Sort(files, System.StringComparer.Ordinal);
                int limit = (maxCars > 0) ? Mathf.Min(maxCars, files.Length) : files.Length;
                // Offset NPC grid indices so they never land on the player's reserved slot.
                int npcGridOffset = 0;
                for (int i = 0; i < limit; i++) {
                    int gridIdx = i + npcGridOffset;
                    if (spawnPlayer && gridIdx == playerGridIndex)
                    {
                        npcGridOffset++;
                        gridIdx++;
                    }
                    SpawnCar(files[i], gridIdx);
                }
            }
        }

        void Update() {
            // Wait 5 seconds to run cars so we can see start of game clearly
            if (Time.timeSinceLevelLoad < 5f) return; 
            
            globalTime += Time.deltaTime * speedMultiplier;
        }

        /// <summary>
        /// Shared grid-position formula (F1 staggered two-column layout).
        /// Returns world position; also provides the grid facing rotation.
        /// </summary>
        Vector3 ComputeGridPosition(int gridIndex, out Quaternion gridRotation)
        {
            int   row          = gridIndex / 2;
            float f1Stagger    = (gridIndex % 2 == 0) ? 0f : gridSpacingMeters * 0.5f;
            float longitudinal = 8f + (row * gridSpacingMeters) + f1Stagger;
            float staggerSide  = (gridIndex % 2 == 0) ? -3.5f : 3.5f;
            float carBodyLength = 5.2f;
            Vector3 pos = gridAnchor
                - gridForward * longitudinal
                + gridRight   * staggerSide
                - gridForward * carBodyLength;
            pos.y += spawnHeightOffset;
            gridRotation = Quaternion.LookRotation(gridForward, Vector3.up);
            return pos;
        }

        // Overload without rotation output for callers that only need the position.
        Vector3 ComputeGridPosition(int gridIndex)
        {
            return ComputeGridPosition(gridIndex, out _);
        }

        /// <summary>Spawn the player-controlled car at the given grid slot.</summary>
        void SpawnPlayerCar(int gridIndex)
        {
            GameObject prefab = (playerCarPrefab != null) ? playerCarPrefab : carPrefab;
            if (prefab == null) return;

            Vector3    gridPos = ComputeGridPosition(gridIndex, out Quaternion gridRot);
            gridPos.y += playerSpawnHeightOffset;
            GameObject car     = Instantiate(prefab, gridPos, gridRot);
            car.name = "PlayerCar";

            if (car.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.isKinematic = true;
                rb.useGravity  = false;
            }

            car.AddComponent<PlayerCarController>().Init(this);

            // White paint strip matching the NPC start-line visuals
            Shader    lineShader   = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material  whitePaintMat = new Material(lineShader) { color = Color.white };
            GameObject linesRoot   = GameObject.Find("CarStartLinesContainer") ?? new GameObject("CarStartLinesContainer");
            GameObject paintStrip  = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paintStrip.transform.SetParent(linesRoot.transform);
            paintStrip.transform.position   = gridPos + gridForward * 3.0f + Vector3.up * 0.05f;
            paintStrip.transform.rotation   = Quaternion.LookRotation(gridRight, Vector3.up);
            paintStrip.transform.localScale = new Vector3(0.1f, 0.01f, 3.5f);
            Destroy(paintStrip.GetComponent<Collider>());
            paintStrip.GetComponent<MeshRenderer>().sharedMaterial = whitePaintMat;
        }

        void SpawnCar(string path, int gridIndex) {
			if (carPrefab == null) return;
		
			DriverReplayTrack trackData = FastF1CsvImporter.LoadDriverCsvForRaceController(
				path, sampleInterval, carYOffset, globalOffset);
			if (trackData.samples.Count < 2) return;
		
			// Material and hierarchy setup
			Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
			Material whitePaintMat = new Material(lineShader) { color = Color.white };
			GameObject linesRoot = GameObject.Find("CarStartLinesContainer") ?? new GameObject("CarStartLinesContainer");
		
			// Grid position math with F1 STAGGER added back
			Vector3 gridPos = ComputeGridPosition(gridIndex);
		
			// Visual paint strip for start lines
			GameObject paintStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
			paintStrip.transform.SetParent(linesRoot.transform);
			paintStrip.transform.position = gridPos + gridForward * 3.0f + Vector3.up * 0.05f;
			paintStrip.transform.rotation = Quaternion.LookRotation(gridRight, Vector3.up);
			paintStrip.transform.localScale = new Vector3(0.1f, 0.01f, 3.5f);
			Destroy(paintStrip.GetComponent<Collider>());
			paintStrip.GetComponent<MeshRenderer>().sharedMaterial = whitePaintMat;
		
			// Path normalization with a "Straight Start" delay
			Vector3 recordedStartPos = trackData.samples[0].worldPosition;
			Vector3 totalOffset = gridPos - recordedStartPos;
			
			// Make cars moving from start positions to recorded racing line
			float mergeDuration = 1.0f; 
			float stayStraightDelay = 1.0f;
		
			for(int s_idx = 0; s_idx < trackData.samples.Count; s_idx++) {
				var sample = trackData.samples[s_idx];
				float currentTime = s_idx * sampleInterval;
				
				float mergeTime = Mathf.Max(0, currentTime - stayStraightDelay);
				float mergeFactor = Mathf.Clamp01(1f - (mergeTime / mergeDuration));
				
				sample.worldPosition += (totalOffset * mergeFactor);
				trackData.samples[s_idx] = sample;
			}
		
			// Spawn car and place it on the grid
			GameObject car = Instantiate(carPrefab);
			car.name = Path.GetFileNameWithoutExtension(path);
			if (car.TryGetComponent<Rigidbody>(out Rigidbody rb)) rb.isKinematic = true;
			car.AddComponent<SmoothMover>().Init(trackData, this);
			
			// Set position and rotation after Init so they are not overridden
			car.transform.position = gridPos;
			car.transform.rotation = Quaternion.LookRotation(gridForward, Vector3.up);
		}

        void BuildTrack(string[] lines) {
            // 丝带路面网格与可选 MeshCollider；材质来自 trackMaterial 或着色器兜底。
            Mesh mesh = TrackRibbonMeshFromCsv.BuildRibbonMesh(
                lines,
                globalOffset,
                trackRibbonWidthMultiplier,
                true,
                trackUvMetersPerRepeat,
                true,
                false);

            GameObject obj = new GameObject("Silverstone_Track");
            obj.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();

            if (trackMaterial != null) {
                mr.material = Instantiate(trackMaterial);
            } else {
                Shader fallback = Shader.Find("Universal Render Pipeline/Unlit");
                if (fallback == null) fallback = Shader.Find("Unlit/Color");
                Material m = new Material(fallback);
                if (fallback.name.Contains("Universal")) m.SetColor("_BaseColor", Color.grey);
                else m.color = Color.grey;
                mr.material = m;
            }

            if (useGroundSpring)
            {
                MeshCollider col = obj.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = false;
                obj.AddComponent<ReplayTrackSurface>();
            }

            BuildTracksideDecor(lines);
        }

        void BuildTracksideDecor(string[] lines)
        {
            if (!buildTracksideDecor)
            {
                return;
            }

            if (tracksideDecorRoot != null)
            {
                Destroy(tracksideDecorRoot.gameObject);
            }

            tracksideDecorRoot = new GameObject("Trackside_Decor").transform;

            List<Vector3> centerline;
            List<float> halfRight;
            List<float> halfLeft;
            bool useRibbonWidths = TryBuildRibbonCentersAndHalfWidths(lines, out centerline, out halfRight, out halfLeft);
            if (!useRibbonWidths)
            {
                centerline = BuildCenterline(lines);
                halfRight = null;
                halfLeft = null;
            }

            if (centerline.Count < 2)
            {
                return;
            }

            List<float> clearanceHalfRight;
            List<float> clearanceHalfLeft;
            if (useRibbonWidths && halfRight != null && halfLeft != null)
            {
                clearanceHalfRight = halfRight;
                clearanceHalfLeft = halfLeft;
            }
            else
            {
                clearanceHalfRight = new List<float>(centerline.Count);
                clearanceHalfLeft = new List<float>(centerline.Count);
                float fh = Mathf.Max(0.5f, fallbackTrackHalfWidthMeters);
                for (int k = 0; k < centerline.Count; k++)
                {
                    clearanceHalfRight.Add(fh);
                    clearanceHalfLeft.Add(fh);
                }
            }

            propFactory = new RacePropFactory(
                tracksideDecorRoot,
                centerline,
                clearanceHalfRight,
                clearanceHalfLeft,
                enforceTrackClearanceExtraMeters);

            // CC0_Race 素材加载与材质构造统一从素材目录类读取。
            RaceAssetCatalog.TracksideAssets assets = RaceAssetCatalog.LoadTracksideAssets();
            RaceAssetCatalog.TracksideMaterials tracksideMats = RaceAssetCatalog.BuildTracksideMaterials(assets);
            GameObject straightFencePrefab = assets.straightFencePrefab;
            GameObject curvedFencePrefab = assets.curvedFencePrefab;

            Material grassMaterial = tracksideMats.grass;
            Material barrierMaterial = tracksideMats.barrier;
            Material tireMaterial = tracksideMats.tire;

            Vector3 boundsMin = centerline[0];
            Vector3 boundsMax = centerline[0];
            for (int i = 1; i < centerline.Count; i++)
            {
                Vector3 point = centerline[i];
                boundsMin = Vector3.Min(boundsMin, point);
                boundsMax = Vector3.Max(boundsMax, point);
            }

            propFactory.CreateGroundPlane(boundsMin, boundsMax, grassMaterial, groundPaddingMeters);

            float distanceSinceLastDecor = 0f;
            for (int i = 1; i < centerline.Count - 1; i++)
            {
                Vector3 previous = centerline[i - 1];
                Vector3 current = centerline[i];
                Vector3 next = centerline[i + 1];

                float segmentLength = Vector3.Distance(previous, current);
                if (segmentLength < 0.01f)
                {
                    continue;
                }

                distanceSinceLastDecor += segmentLength;
                if (distanceSinceLastDecor < decorSpacingMeters)
                {
                    continue;
                }

                distanceSinceLastDecor = 0f;

                Vector3 forward = (next - current).normalized;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = (current - previous).normalized;
                }

                if (forward.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

                Vector3 incoming = (current - previous).normalized;
                float cornerSeverity = Vector3.Angle(incoming, forward);
                bool useCurvedFenceHere = enableCurvedFencePrefabs
                    && curvedFencePrefab != null
                    && cornerSeverity <= curvedFenceMaxCornerDeg
                    && UnityEngine.Random.value > 0.75f;

                float hl = useRibbonWidths ? halfLeft[i] : 0f;
                float hr = useRibbonWidths ? halfRight[i] : 0f;
                propFactory.SpawnBarrierRow(
                    current,
                    rotation,
                    right,
                    -1f,
                    barrierMaterial,
                    straightFencePrefab,
                    curvedFencePrefab,
                    useCurvedFenceHere,
                    useRibbonWidths,
                    hl,
                    hr,
                    cornerSeverity,
                    barrierOffsetMeters,
                    fenceOffsetMeters,
                    decorBarrierClearanceBeyondAsphaltMeters,
                    decorFenceClearanceBeyondAsphaltMeters,
                    curvedFenceMaxCornerDeg,
                    cornerFenceExtraOutwardMeters,
                    barrierThicknessMeters,
                    barrierHeightMeters,
                    barrierLengthMeters,
                    fencePrefabAdditionalOutwardMeters,
                    fenceVerticalOffset,
                    fencePrefabYawOffsetDegrees,
                    curvedFenceRightSideExtraYawDegrees);
                propFactory.SpawnBarrierRow(
                    current,
                    rotation,
                    right,
                    1f,
                    barrierMaterial,
                    straightFencePrefab,
                    curvedFencePrefab,
                    useCurvedFenceHere,
                    useRibbonWidths,
                    hl,
                    hr,
                    cornerSeverity,
                    barrierOffsetMeters,
                    fenceOffsetMeters,
                    decorBarrierClearanceBeyondAsphaltMeters,
                    decorFenceClearanceBeyondAsphaltMeters,
                    curvedFenceMaxCornerDeg,
                    cornerFenceExtraOutwardMeters,
                    barrierThicknessMeters,
                    barrierHeightMeters,
                    barrierLengthMeters,
                    fencePrefabAdditionalOutwardMeters,
                    fenceVerticalOffset,
                    fencePrefabYawOffsetDegrees,
                    curvedFenceRightSideExtraYawDegrees);

                if (cornerSeverity > 10f)
                {
                    float turnSide = Vector3.Dot(Vector3.up, Vector3.Cross(incoming, forward));
                    propFactory.SpawnTireStack(current, right, turnSide, tireMaterial, useRibbonWidths, hl, hr, tireStackOffsetMeters, decorTireClearanceBeyondAsphaltMeters);
                }
            }

            BuildF1EventLandmarks(centerline, halfRight, halfLeft, useRibbonWidths, barrierMaterial);
            BuildRaceOperationsProps(centerline, halfRight, halfLeft, useRibbonWidths, barrierMaterial);

            if (enforceNoPropsOnTrack)
            {
                propFactory.EnforceTrackClearanceForAllProps();
            }
        }



        void BuildRaceOperationsProps(List<Vector3> centerline, List<float> halfRight, List<float> halfLeft, bool useRibbonWidths, Material frameMaterial)
        {
            if (!buildRaceOperationsProps || centerline == null || centerline.Count < 8)
            {
                return;
            }

            RaceAssetCatalog.OperationsMaterials opsMats = RaceAssetCatalog.BuildOperationsMaterials(frameMaterial, marshalFlagColor);
            Material boardMaterial = opsMats.board;
            Material postMaterial = opsMats.post;
            Material flagMaterial = opsMats.flag;
            Material bannerMaterial = opsMats.banner;

            float spacingAccum = cornerPropsMinSpacingMeters;
            for (int i = 2; i < centerline.Count - 2; i++)
            {
                Vector3 prev = centerline[i - 1];
                Vector3 curr = centerline[i];
                Vector3 next = centerline[i + 1];

                spacingAccum += Vector3.Distance(prev, curr);
                Vector3 incoming = (curr - prev).normalized;
                Vector3 outgoing = (next - curr).normalized;
                if (incoming.sqrMagnitude < 0.0001f || outgoing.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                float cornerAngle = Vector3.Angle(incoming, outgoing);
                if (cornerAngle < cornerDetectionAngleDeg || spacingAccum < cornerPropsMinSpacingMeters)
                {
                    continue;
                }

                spacingAccum = 0f;
                float turnSign = Vector3.Dot(Vector3.up, Vector3.Cross(incoming, outgoing));
                Vector3 right = Vector3.Cross(Vector3.up, outgoing).normalized;
                float outerSide = turnSign >= 0f ? 1f : -1f;
                float hr = useRibbonWidths ? halfRight[i] : 0f;
                float hl = useRibbonWidths ? halfLeft[i] : 0f;

                if (buildBrakingBoards)
                {
                    BuildBrakingBoards(curr, incoming, right, outerSide, boardMaterial, postMaterial, useRibbonWidths, hr, hl);
                }
                BuildMarshalPost(curr, outgoing, right, outerSide, postMaterial, flagMaterial, useRibbonWidths, hr, hl);
            }

            BuildBannerGates(centerline, halfRight, halfLeft, useRibbonWidths, bannerMaterial, postMaterial);
        }

        void BuildBrakingBoards(Vector3 cornerPoint, Vector3 incoming, Vector3 right, float sideSign, Material boardMaterial, Material postMaterial, bool useRibbonWidths, float halfRightHere, float halfLeftHere)
        {
            if (boardMaterial == null)
            {
                return;
            }

            float lateral = brakingBoardOffsetMeters;
            if (useRibbonWidths)
            {
                float halfOut = sideSign > 0f ? halfRightHere : halfLeftHere;
                lateral = halfOut + operationsClearanceBeyondAsphaltMeters;
            }

            int[] markers = { 150, 100, 50 };
            for (int idx = 0; idx < markers.Length; idx++)
            {
                float longitudinal = markers[idx] * 0.2f;
                Vector3 basePos = cornerPoint - incoming * longitudinal + right * sideSign * lateral;

                Vector3 postPos = basePos + Vector3.up * 1.05f;
                propFactory.CreateBlock(
                    "BrakePost_" + markers[idx],
                    postPos,
                    Quaternion.identity,
                    new Vector3(0.16f, 2.1f, 0.16f),
                    postMaterial);

                Vector3 boardPos = basePos + Vector3.up * 2.05f;
                propFactory.CreateBlock(
                    "BrakeBoard_" + markers[idx],
                    boardPos,
                    Quaternion.LookRotation(-right * sideSign, Vector3.up),
                    new Vector3(0.12f, 1.0f, 0.85f),
                    boardMaterial);
            }
        }

        void BuildMarshalPost(Vector3 cornerPoint, Vector3 forward, Vector3 right, float sideSign, Material postMaterial, Material flagMaterial, bool useRibbonWidths, float halfRightHere, float halfLeftHere)
        {
            float lateral = marshalPostOffsetMeters;
            if (useRibbonWidths)
            {
                float halfOut = sideSign > 0f ? halfRightHere : halfLeftHere;
                lateral = halfOut + operationsClearanceBeyondAsphaltMeters;
            }

            Vector3 postBase = cornerPoint + right * sideSign * lateral;

            propFactory.CreateBlock(
                "MarshalPost_Base",
                postBase + Vector3.up * 1.4f,
                Quaternion.identity,
                new Vector3(1.2f, 2.8f, 1.2f),
                postMaterial);

            propFactory.CreateBlock(
                "MarshalPost_Pole",
                postBase + Vector3.up * 4.3f,
                Quaternion.identity,
                new Vector3(0.15f, 2.8f, 0.15f),
                postMaterial);

            propFactory.CreateBlock(
                "MarshalFlagPanel",
                postBase + Vector3.up * 5.4f + right * sideSign * 0.35f,
                Quaternion.LookRotation(forward, Vector3.up),
                new Vector3(0.04f, 1.2f, 1.8f),
                flagMaterial);
        }

        void BuildBannerGates(List<Vector3> centerline, List<float> halfRight, List<float> halfLeft, bool useRibbonWidths, Material bannerMaterial, Material frameMaterial)
        {
            if (bannerGateCount <= 0 || centerline.Count < 6)
            {
                return;
            }

            int stride = Mathf.Max(18, centerline.Count / (bannerGateCount + 1));
            int built = 0;
            for (int i = stride; i < centerline.Count - stride && built < bannerGateCount; i += stride)
            {
                Vector3 prev = centerline[i - 1];
                Vector3 curr = centerline[i];
                Vector3 next = centerline[i + 1];
                Vector3 incoming = (curr - prev).normalized;
                Vector3 outgoing = (next - curr).normalized;
                float curvature = Vector3.Angle(incoming, outgoing);

                if (curvature > 6f)
                {
                    continue;
                }

                Vector3 forward = (next - prev).normalized;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float leftDist;
                float rightDist;
                if (useRibbonWidths && halfRight != null && halfLeft != null && i < halfRight.Count)
                {
                    leftDist = halfLeft[i] + bannerGateClearanceBeyondAsphaltMeters;
                    rightDist = halfRight[i] + bannerGateClearanceBeyondAsphaltMeters;
                }
                else
                {
                    leftDist = 7.5f;
                    rightDist = 7.5f;
                }

                Vector3 leftPole = curr - right * leftDist;
                Vector3 rightPole = curr + right * rightDist;
                float crossHalfZ = (leftDist + rightDist) * 0.5f + 0.4f;

                propFactory.CreateBlock("BannerPole_Left_" + built, leftPole + Vector3.up * 3.9f, Quaternion.identity, new Vector3(0.28f, 7.8f, 0.28f), frameMaterial);
                propFactory.CreateBlock("BannerPole_Right_" + built, rightPole + Vector3.up * 3.9f, Quaternion.identity, new Vector3(0.28f, 7.8f, 0.28f), frameMaterial);
                // 横幅横梁：LookRotation(forward) 后 scale X 为沿赛道横向跨度。
                propFactory.CreateBlock("BannerCross_" + built, curr + Vector3.up * 7.6f, Quaternion.LookRotation(forward, Vector3.up), new Vector3(crossHalfZ * 2f + 0.8f, 0.28f, 0.35f), frameMaterial);
                float panelWidth = Mathf.Max(9.5f, leftDist + rightDist + 1f);
                // 横幅面板：X=横跨宽度，Y=高度，Z=薄片厚度。
                propFactory.CreateBlock(
                    "BannerPanel_" + built,
                    curr + Vector3.up * 6.45f,
                    // Fix: Ensure we use private variable here as well if needed
                    Quaternion.LookRotation(forward, Vector3.up),
                    new Vector3(panelWidth, 1.5f, 0.08f),
                    bannerMaterial);

                built++;
            }
        }

        bool TryGetReplayStartAnchor(float groundY, out Vector3 start, out Vector3 forwardHorizontal)
        {
            start = Vector3.zero;
            forwardHorizontal = Vector3.zero;
            string dir = Path.Combine(Application.streamingAssetsPath, carFolder);
            if (!Directory.Exists(dir))
            {
                return false;
            }

            string[] files = Directory.GetFiles(dir, "*.csv");
            if (files.Length == 0)
            {
                return false;
            }

            System.Array.Sort(files, System.StringComparer.Ordinal);
            int limit = maxCars > 0 ? Mathf.Min(maxCars, files.Length) : files.Length;
            for (int i = 0; i < limit; i++)
            {
                DriverReplayTrack t = FastF1CsvImporter.LoadDriverCsvForRaceController(
                    files[i], sampleInterval, carYOffset, globalOffset);
                if (t.samples.Count < 2)
                {
                    continue;
                }

                start = t.samples[0].worldPosition;
                start.y = groundY;

                for (int s = 1; s < t.samples.Count; s++)
                {
                    Vector3 delta = t.samples[s].worldPosition - t.samples[0].worldPosition;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > 0.04f)
                    {
                        forwardHorizontal = delta.normalized;
                        return true;
                    }
                }
            }

            return false;
        }

        void BuildF1EventLandmarks(List<Vector3> centerline, List<float> halfRight, List<float> halfLeft, bool useRibbonWidths, Material barrierMaterial)
        {
            if (!buildF1EventLandmarks || centerline == null || centerline.Count < 2)
            {
                return;
            }

            float groundY = centerline[0].y;
            Vector3 start = Vector3.zero;
            Vector3 startForward = Vector3.zero;
            bool fromReplay = false;
            if (anchorStartFromCarReplay)
            {
                fromReplay = TryGetReplayStartAnchor(groundY, out start, out startForward);
            }

            if (!fromReplay)
            {
                if (centerline.Count < 24)
                {
                    return;
                }

                int startIndex = Mathf.Clamp(8, 1, centerline.Count - 3);
                start = centerline[startIndex];
                startForward = (centerline[startIndex + 1] - centerline[startIndex - 1]).normalized;
            }

            startForward.y = 0f;
            if (startForward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            startForward.Normalize();
            start = new Vector3(start.x, groundY, start.z);

            Vector3 startRight = Vector3.Cross(Vector3.up, startForward).normalized;
            if (startRight.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // Save values for SpawnCar access
            gridAnchor = start;
            gridForward = startForward;
            gridRight = startRight;

            RaceAssetCatalog.LandmarkMaterials landmarkMats = RaceAssetCatalog.BuildLandmarkMaterials();
            Material paintMaterial = landmarkMats.paint;
            Material gantryMaterial = landmarkMats.gantry;

            BuildStartFinishMarkings(start, startForward, startRight, paintMaterial);
            BuildStartLightGantry(start + startForward * 8f, startForward, startRight, gantryMaterial, paintMaterial, landmarkMats.lightOn, centerline, halfRight, halfLeft, useRibbonWidths);
            BuildPitWallAndPaddock(start + startForward * 18f, startForward, startRight, barrierMaterial, landmarkMats.paddock, centerline, halfRight, halfLeft, useRibbonWidths);
            BuildSponsorBoards(centerline, halfRight, halfLeft, useRibbonWidths, barrierMaterial);
        }

        void BuildStartFinishMarkings(Vector3 start, Vector3 forward, Vector3 right, Material paintMaterial)
        {
            if (paintMaterial == null)
            {
                return;
            }

            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "StartFinish_Line";
            line.transform.SetParent(tracksideDecorRoot, false);
            line.transform.position = start + Vector3.up * 0.02f;
            line.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            line.transform.localScale = new Vector3(13.8f, 0.02f, 1.0f);
            Destroy(line.GetComponent<Collider>());
            line.GetComponent<MeshRenderer>().sharedMaterial = paintMaterial;

            int rows = Mathf.Clamp(gridRows, 6, 24);
            for (int i = 0; i < rows; i++)
            {
                float longitudinal = 8f + i * gridSpacingMeters;
                float stagger = (i % 2 == 0) ? -3.5f : 3.5f; // Updated for realistic car width
                Vector3 center = start - forward * longitudinal + right * stagger + Vector3.up * 0.02f;
                CreateGridBox(center, forward, paintMaterial);
            }
        }

        void CreateGridBox(Vector3 center, Vector3 forward, Material paintMaterial)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            float halfLen = gridBoxLengthMeters * 0.5f;
            float halfWid = gridBoxWidthMeters * 0.5f;
            float lineWidth = 0.08f;

            CreateMarkingStrip(center + forward * halfLen, forward, right, gridBoxWidthMeters, lineWidth, paintMaterial);
            CreateMarkingStrip(center - forward * halfLen, forward, right, gridBoxWidthMeters, lineWidth, paintMaterial);
            CreateMarkingStrip(center - right * halfWid, right, forward, gridBoxLengthMeters, lineWidth, paintMaterial);
            CreateMarkingStrip(center + right * halfWid, right, forward, gridBoxLengthMeters, lineWidth, paintMaterial);
        }

        void CreateMarkingStrip(Vector3 center, Vector3 forwardAxis, Vector3 sideAxis, float length, float width, Material material)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.transform.SetParent(tracksideDecorRoot, false);
            strip.transform.position = center;
            strip.transform.rotation = Quaternion.LookRotation(forwardAxis, Vector3.up);
            strip.transform.localScale = new Vector3(width, 0.02f, length);
            Destroy(strip.GetComponent<Collider>());
            strip.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        void BuildStartLightGantry(Vector3 anchor, Vector3 forward, Vector3 right, Material gantryMaterial, Material lampMaterial, Material lightOnMaterial, List<Vector3> trackCenters, List<float> halfRight, List<float> halfLeft, bool useRibbonWidths)
        {
            if (gantryMaterial == null)
            {
                return;
            }

            float halfSpan = 8f;
            if (useRibbonWidths && trackCenters != null && halfRight != null && halfLeft != null && trackCenters.Count > 0)
            {
                int idx = NearestCenterlineIndex(trackCenters, anchor);
                halfSpan = Mathf.Max(8f, Mathf.Max(halfRight[idx], halfLeft[idx]) + 2f);
            }

            Vector3 leftPole = anchor - right * halfSpan;
            Vector3 rightPole = anchor + right * halfSpan;

            propFactory.CreateBlock("Gantry_LeftPole", leftPole + Vector3.up * 4.6f, Quaternion.identity, new Vector3(0.55f, 9.2f, 0.55f), gantryMaterial);
            propFactory.CreateBlock("Gantry_RightPole", rightPole + Vector3.up * 4.6f, Quaternion.identity, new Vector3(0.55f, 9.2f, 0.55f), gantryMaterial);
            propFactory.CreateBlock("Gantry_Beam", anchor + Vector3.up * 9.0f, Quaternion.LookRotation(forward, Vector3.up), new Vector3(0.7f, 0.8f, halfSpan * 2f + 1.5f), gantryMaterial);

            Material lightOffMaterial = lampMaterial ?? gantryMaterial;
            for (int i = 0; i < 5; i++)
            {
                float normalized = (i - 2f) * 1.35f;
                Vector3 lightPos = anchor + right * normalized + Vector3.up * 8.35f - forward * 0.5f;
                Material lightMat = (i == 2) ? lightOnMaterial : lightOffMaterial;
                propFactory.CreateBlock("Gantry_Light_" + i, lightPos, Quaternion.identity, new Vector3(0.35f, 0.35f, 0.35f), lightMat);
            }
        }

        void BuildPitWallAndPaddock(Vector3 pitAnchor, Vector3 forward, Vector3 pitSideRight, Material barrierMaterial, Material paddockMaterial, List<Vector3> trackCenters, List<float> halfRight, List<float> halfLeft, bool useRibbonWidths)
        {
            if (barrierMaterial == null)
            {
                return;
            }

            float sectionLength = Mathf.Max(6f, barrierLengthMeters);
            int sections = Mathf.Max(8, Mathf.RoundToInt(pitWallLengthMeters / sectionLength));
            for (int i = 0; i < sections; i++)
            {
                float longitudinal = i * sectionLength;
                Vector3 basePos = pitAnchor + forward * longitudinal;
                float wallLateral = pitWallOffsetMeters;
                float buildingLateral = pitBuildingOffsetMeters;
                if (useRibbonWidths && trackCenters != null && halfRight != null && halfLeft != null && trackCenters.Count > 0)
                {
                    int idx = NearestCenterlineIndex(trackCenters, basePos);
                    Vector3 tr = GetTrackRightAtIndex(trackCenters, idx);
                    float halfTowardPits = Vector3.Dot(pitSideRight, tr) >= 0f ? halfRight[idx] : halfLeft[idx];
                    wallLateral = halfTowardPits + pitWallClearanceBeyondAsphaltMeters;
                    buildingLateral = wallLateral + pitPaddockBeyondWallMeters;
                }

                Vector3 wallPos = basePos + pitSideRight * wallLateral + Vector3.up * (barrierHeightMeters * 0.5f + 0.1f);

                propFactory.CreateBlock(
                    "PitWall_" + i,
                    wallPos,
                    Quaternion.LookRotation(forward, Vector3.up),
                    new Vector3(barrierThicknessMeters * 1.2f, barrierHeightMeters * 1.15f, sectionLength),
                    barrierMaterial);

                if (i % 3 == 0)
                {
                    Vector3 boxPos = basePos + pitSideRight * buildingLateral + Vector3.up * 4.2f;
                    float zScale = Mathf.Clamp(sectionLength * 1.05f, 8f, 18f);
                    propFactory.CreateBlock(
                        "PaddockBox_" + i,
                        boxPos,
                        Quaternion.LookRotation(forward, Vector3.up),
                        new Vector3(10f, 8.4f, zScale),
                        paddockMaterial);
                }
            }
        }

        void BuildSponsorBoards(List<Vector3> centerline, List<float> halfRight, List<float> halfLeft, bool useRibbonWidths, Material frameMaterial)
        {
            if (centerline == null || centerline.Count < 4 || sponsorBoardCount <= 0)
            {
                return;
            }

            int stride = Mathf.Max(6, centerline.Count / sponsorBoardCount);
            int created = 0;

            for (int i = 2; i < centerline.Count - 2 && created < sponsorBoardCount; i += stride)
            {
                Vector3 current = centerline[i];
                Vector3 forward = (centerline[i + 1] - centerline[i - 1]).normalized;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                float sideSign = (created % 2 == 0) ? 1f : -1f;
                float lateral = fenceOffsetMeters + 3f;
                if (useRibbonWidths && halfRight != null && halfLeft != null && i < halfRight.Count)
                {
                    float halfSide = sideSign > 0f ? halfRight[i] : halfLeft[i];
                    lateral = halfSide + decorSponsorClearanceBeyondAsphaltMeters;
                }

                Vector3 boardPos = current + right * sideSign * lateral + Vector3.up * 2.2f;

                Material boardMaterial = RaceAssetCatalog.CreateSponsorBoardMaterial(created);
                propFactory.CreateBlock(
                    "SponsorBoard_" + created,
                    boardPos,
                    Quaternion.LookRotation(forward * -sideSign, Vector3.up),
                    new Vector3(0.25f, 2.2f, 6.8f),
                    boardMaterial);

                if (frameMaterial != null)
                {
                    Vector3 framePos = boardPos - right * sideSign * 0.2f;
                    propFactory.CreateBlock(
                        "SponsorFrame_" + created,
                        framePos,
                        Quaternion.LookRotation(forward, Vector3.up),
                        new Vector3(0.2f, 2.5f, 7.1f),
                        frameMaterial);
                }

                created++;
            }
        }



        bool TryParseTrackGlobalOffset(string[] lines, string trackPathForLog, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (lines == null || lines.Length < 2)
            {
                Debug.LogWarning("Race_Controller: track CSV has fewer than 2 lines: " + trackPathForLog);
                return false;
            }

            string[] cols = lines[1].Split(',');
            if (cols.Length < 2 ||
                !float.TryParse(cols[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float ox) ||
                !float.TryParse(cols[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float oz))
            {
                Debug.LogWarning("Race_Controller: invalid global offset row (line 2) in track CSV: " + trackPathForLog);
                return false;
            }

            offset = -new Vector3(ox, 0f, oz);
            return true;
        }

        List<Vector3> BuildCenterline(string[] lines)
        {
            List<Vector3> points = new List<Vector3>();
            for (int i = 1; i < lines.Length; i++)
            {
                string[] columns = lines[i].Split(',');
                if (columns.Length < 2)
                {
                    continue;
                }

                if (!float.TryParse(columns[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(columns[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    continue;
                }

                points.Add(new Vector3(x, 0f, z) + globalOffset);
            }

            return points;
        }

        bool TryBuildRibbonCentersAndHalfWidths(string[] lines, out List<Vector3> centers, out List<float> halfRight, out List<float> halfLeft)
        {
            centers = new List<Vector3>();
            halfRight = new List<float>();
            halfLeft = new List<float>();
            float mult = trackRibbonWidthMultiplier;

            for (int i = 1; i < lines.Length; i++)
            {
                string[] c = lines[i].Split(',');
                if (c.Length < 4)
                {
                    continue;
                }

                if (!float.TryParse(c[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) ||
                    !float.TryParse(c[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cz) ||
                    !float.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float wR) ||
                    !float.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float wL))
                {
                    continue;
                }

                centers.Add(new Vector3(cx, 0f, cz) + globalOffset);
                halfRight.Add(wR * mult);
                halfLeft.Add(wL * mult);
            }

            return centers.Count >= 2;
        }

        static int NearestCenterlineIndex(List<Vector3> centers, Vector3 world)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            Vector3 flat = world;
            flat.y = 0f;
            for (int i = 0; i < centers.Count; i++)
            {
                Vector3 p = centers[i];
                p.y = 0f;
                float sqr = (p - flat).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        static Vector3 GetTrackRightAtIndex(List<Vector3> centers, int i)
        {
            if (centers == null || centers.Count < 2)
            {
                return Vector3.right;
            }

            i = Mathf.Clamp(i, 0, centers.Count - 1);
            Vector3 forward;
            if (i < centers.Count - 1)
            {
                forward = centers[i + 1] - centers[i];
            }
            else
            {
                forward = centers[i] - centers[i - 1];
            }

            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f)
            {
                return Vector3.right;
            }

            forward.Normalize();
            return Vector3.Cross(Vector3.up, forward).normalized;
        }



    }
}
