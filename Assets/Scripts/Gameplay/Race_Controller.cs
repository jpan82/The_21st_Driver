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

        [Header("Track Visual")]
        public float trackUvMetersPerRepeat = 12f;

        [Header("Trackside Decor")]
        public bool buildTracksideDecor = true;
        public float decorSpacingMeters = 24f;
        public float barrierOffsetMeters = 14f;
        public float fenceOffsetMeters = 18f;
        public float groundPaddingMeters = 80f;
        public float barrierLengthMeters = 10f;
        public float barrierHeightMeters = 1.25f;
        public float barrierThicknessMeters = 0.7f;
        public float fenceVerticalOffset = 2.15f;
        public float tireStackOffsetMeters = 20f;
        [Tooltip("Kenney 围栏沿模型本地 +X 拉长；代码用 LookRotation 让物体 +Z 对准赛道切线。默认 90° 使围栏顺赛道延伸。")]
        [SerializeField] private float fencePrefabYawOffsetDegrees = 90f;
        [Tooltip("fenceCurved 左右不对称；右侧 (sideSign>0) 需相对左侧再绕 Y 旋转，弯钩才会朝向跑道内侧。若左右反了可改成 0 或 -180。")]
        [SerializeField] private float curvedFenceRightSideExtraYawDegrees = 180f;

        private Vector3 globalOffset;
        private float globalTime;
        private Transform tracksideDecorRoot;

        public float GlobalTime => globalTime;

        void Start() {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            string tPath = Path.Combine(Application.streamingAssetsPath, trackFolder, trackFileName);
            string cDirPath = Path.Combine(Application.streamingAssetsPath, carFolder);

            if (!File.Exists(tPath)) return;

            string[] tLines = File.ReadAllLines(tPath);
            globalOffset = -new Vector3(float.Parse(tLines[1].Split(',')[0], CultureInfo.InvariantCulture), 0, float.Parse(tLines[1].Split(',')[1], CultureInfo.InvariantCulture));

            BuildTrack(tLines);

            if (Directory.Exists(cDirPath)) {
                string[] files = Directory.GetFiles(cDirPath, "*.csv");
                int limit = (maxCars > 0) ? Mathf.Min(maxCars, files.Length) : files.Length;
                for (int i = 0; i < limit; i++) {
                    SpawnCar(files[i]);
                }
            }
        }

        void Update() {
            globalTime += Time.deltaTime * speedMultiplier;
        }

        void SpawnCar(string path) {
            DriverReplayTrack trackData = FastF1CsvImporter.LoadDriverCsvForRaceController(
                path, sampleInterval, carYOffset, globalOffset);
            if (trackData.samples.Count < 2) return;

            GameObject car = Instantiate(carPrefab);
            car.name = Path.GetFileNameWithoutExtension(path);
            if (car.TryGetComponent<Rigidbody>(out Rigidbody rb)) rb.isKinematic = true;

            car.AddComponent<SmoothMover>().Init(trackData, this);
        }

        void BuildTrack(string[] lines) {
            const float trackWidthMultiplier = 1.5f;
            Mesh mesh = TrackRibbonMeshFromCsv.BuildRibbonMesh(
                lines,
                globalOffset,
                trackWidthMultiplier,
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

            List<Vector3> centerline = BuildCenterline(lines);
            if (centerline.Count < 2)
            {
                return;
            }

            Texture2D grassAlbedo = Resources.Load<Texture2D>("CC0_Race/Grass/Grass001_1K-JPG_Color");
            Texture2D grassNormal = Resources.Load<Texture2D>("CC0_Race/Grass/Grass001_1K-JPG_NormalGL");
            Texture2D barrierAlbedo = Resources.Load<Texture2D>("CC0_Race/Barrier/concrete_road_barrier_diff_2k");
            Texture2D barrierNormal = Resources.Load<Texture2D>("CC0_Race/Barrier/concrete_road_barrier_nor_gl_2k");
            Texture2D rubberAlbedo = Resources.Load<Texture2D>("CC0_Race/Rubber/Rubber004_2K-JPG_Color");
            Texture2D rubberNormal = Resources.Load<Texture2D>("CC0_Race/Rubber/Rubber004_2K-JPG_NormalGL");
            GameObject straightFencePrefab = Resources.Load<GameObject>("CC0_Race/Kenney/fenceStraight");
            GameObject curvedFencePrefab = Resources.Load<GameObject>("CC0_Race/Kenney/fenceCurved");

            Material grassMaterial = CreateTexturedLitMaterial(new Color(0.38f, 0.49f, 0.23f, 1f), grassAlbedo, grassNormal);
            Material barrierMaterial = CreateTexturedLitMaterial(Color.white, barrierAlbedo, barrierNormal);
            Material tireMaterial = CreateTexturedLitMaterial(new Color(0.12f, 0.12f, 0.12f, 1f), rubberAlbedo, rubberNormal);

            if (grassMaterial != null)
            {
                ApplyTextureTiling(grassMaterial, new Vector2(60f, 60f));
            }

            if (barrierMaterial != null)
            {
                ApplyTextureTiling(barrierMaterial, new Vector2(8f, 1f));
            }

            if (tireMaterial != null)
            {
                ApplyTextureTiling(tireMaterial, new Vector2(3f, 3f));
            }

            Vector3 boundsMin = centerline[0];
            Vector3 boundsMax = centerline[0];
            for (int i = 1; i < centerline.Count; i++)
            {
                Vector3 point = centerline[i];
                boundsMin = Vector3.Min(boundsMin, point);
                boundsMax = Vector3.Max(boundsMax, point);
            }

            CreateGroundPlane(boundsMin, boundsMax, grassMaterial);

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

                bool useCurvedFenceHere = curvedFencePrefab != null && Random.value > 0.75f;
                SpawnBarrierRow(current, rotation, right, -1f, barrierMaterial, straightFencePrefab, curvedFencePrefab, useCurvedFenceHere);
                SpawnBarrierRow(current, rotation, right, 1f, barrierMaterial, straightFencePrefab, curvedFencePrefab, useCurvedFenceHere);

                Vector3 incoming = (current - previous).normalized;
                float cornerSeverity = Vector3.Angle(incoming, forward);
                if (cornerSeverity > 10f)
                {
                    float turnSide = Vector3.Dot(Vector3.up, Vector3.Cross(incoming, forward));
                    SpawnTireStack(current, right, turnSide, tireMaterial);
                }
            }
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

        void CreateGroundPlane(Vector3 boundsMin, Vector3 boundsMax, Material grassMaterial)
        {
            if (grassMaterial == null)
            {
                return;
            }

            Vector3 center = (boundsMin + boundsMax) * 0.5f;
            Vector3 size = boundsMax - boundsMin;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Trackside_Grass";
            ground.transform.SetParent(tracksideDecorRoot, false);
            ground.transform.position = new Vector3(center.x, -0.12f, center.z);
            ground.transform.localScale = new Vector3(
                Mathf.Max(1f, (size.x + groundPaddingMeters) / 10f),
                1f,
                Mathf.Max(1f, (size.z + groundPaddingMeters) / 10f));

            Destroy(ground.GetComponent<Collider>());
            ground.GetComponent<MeshRenderer>().sharedMaterial = grassMaterial;
        }

        void SpawnBarrierRow(Vector3 center, Quaternion rotation, Vector3 right, float sideSign, Material barrierMaterial, GameObject straightFencePrefab, GameObject curvedFencePrefab, bool useCurvedFence)
        {
            float offsetSign = sideSign < 0f ? -1f : 1f;
            Vector3 barrierPosition = center + right * offsetSign * barrierOffsetMeters + Vector3.up * (barrierHeightMeters * 0.5f);

            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = sideSign < 0f ? "Barrier_Left" : "Barrier_Right";
            barrier.transform.SetParent(tracksideDecorRoot, false);
            barrier.transform.position = barrierPosition;
            barrier.transform.rotation = rotation;
            // 与 LookRotation(forward) 一致：长边放在本地 Z（物体前方），否则会横在赛道切向上。
            barrier.transform.localScale = new Vector3(barrierThicknessMeters, barrierHeightMeters, barrierLengthMeters);
            Destroy(barrier.GetComponent<Collider>());

            MeshRenderer barrierRenderer = barrier.GetComponent<MeshRenderer>();
            if (barrierMaterial != null)
            {
                barrierRenderer.sharedMaterial = barrierMaterial;
            }

            bool pickCurved = useCurvedFence && curvedFencePrefab != null;
            GameObject fencePrefab = pickCurved ? curvedFencePrefab : straightFencePrefab;
            if (fencePrefab != null)
            {
                Vector3 fencePosition = center + right * offsetSign * fenceOffsetMeters + Vector3.up * fenceVerticalOffset;
                float sideYaw = pickCurved && offsetSign > 0f ? curvedFenceRightSideExtraYawDegrees : 0f;
                Quaternion fenceRotation = rotation * Quaternion.Euler(0f, fencePrefabYawOffsetDegrees + sideYaw, 0f);
                GameObject fence = Instantiate(fencePrefab, fencePosition, fenceRotation, tracksideDecorRoot);
                fence.transform.localScale = Vector3.one;
            }
        }

        void SpawnTireStack(Vector3 center, Vector3 right, float turnSide, Material tireMaterial)
        {
            if (tireMaterial == null)
            {
                return;
            }

            float outerSide = turnSide >= 0f ? 1f : -1f;
            Vector3 basePosition = center + right * outerSide * tireStackOffsetMeters + Vector3.up * 0.5f;

            GameObject stackRoot = new GameObject(turnSide >= 0f ? "TireStack_Right" : "TireStack_Left");
            stackRoot.transform.SetParent(tracksideDecorRoot, false);
            stackRoot.transform.position = basePosition;

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tire.transform.SetParent(stackRoot.transform, false);
                    tire.transform.localPosition = new Vector3((col - 1) * 0.55f, row * 0.28f, 0f);
                    tire.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    tire.transform.localScale = new Vector3(0.48f, 0.18f, 0.48f);
                    Destroy(tire.GetComponent<Collider>());
                    tire.GetComponent<MeshRenderer>().sharedMaterial = tireMaterial;
                }
            }
        }

        Material CreateTexturedLitMaterial(Color tint, Texture2D albedo, Texture2D normalMap)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader);
            if (albedo != null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", albedo);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", albedo);
                }
            }

            if (normalMap != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }

            return material;
        }

        void ApplyTextureTiling(Material material, Vector2 tiling)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", tiling);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_MainTex", tiling);
            }

            if (material.HasProperty("_BumpMap"))
            {
                material.SetTextureScale("_BumpMap", tiling);
            }
        }
    }
}
