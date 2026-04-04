using UnityEngine;
using System.IO;
using System.Globalization;
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
        [Tooltip("Interval between CSV rows in seconds (5ms = 0.005)")]
        public float sampleInterval = 0.05f;
        [Range(0.1f, 50f)]
        public float speedMultiplier = 1f;

        [Header("Car Alignment")]
        public float carYOffset = 1.0f;
        public float rotationSmoothness = 10f;
        public float fixedXRotation = -90f;

        [Header("Track Visual")]
        public float trackUvMetersPerRepeat = 12f;

        private Vector3 globalOffset;
        private float globalTime;

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
            DriverReplayTrack trackData = FastF1CsvImporter.LoadMotionDumpTrack(path, sampleInterval, carYOffset, globalOffset);
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
        }
    }
}
