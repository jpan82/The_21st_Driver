using UnityEngine;
using System.Collections.Generic;
using System.IO;
using The21stDriver.Replay.Data;
using The21stDriver.Replay.Importers;
using The21stDriver.Replay.Playback;
using The21stDriver.Replay.Track;

namespace The21stDriver.Vehicles
{
    public class CSVMovementPlayer : MonoBehaviour
    {
        [Header("File Settings")]
        public string fileName = "f1_motion_dump/ALO_data.csv";
        public string trackFileName = "track_data/Silverstone.csv"; // Path to your track CSV
        [Range(0.1f, 5f)]
        public float speedMultiplier = 0.5f;

        [Header("Track Visuals")]
        public Material trackSurfaceMaterial; // Drag a simple material here
        public Color trackColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);
        public float trackWidth = 15f; // Used for the LineRenderer path
        public float lineSampleTimeStep = 0.05f;

        [Header("Overlap Fix")]
        public float carVerticalOffset = 0.2f;
        public float fixedXRotation = -90f;
        public float rotationSmoothness = 10f;

        private string carFilePath;
        private string trackFilePath;
        private LineRenderer trackLine; // This remains as the "Racing Line"
        private DriverReplayTrack replayTrack;
        private TrajectorySampler sampler;
        private float replayTimeSeconds;
        private float replayEndTimeSeconds;
        private bool isPlaying;

        void Start()
        {
        carFilePath = Path.Combine(Application.streamingAssetsPath, fileName);
        trackFilePath = Path.Combine(Application.streamingAssetsPath, trackFileName);

        // Setup Racing Line (Original Code)
        trackLine = gameObject.AddComponent<LineRenderer>();
        trackLine.startWidth = trackWidth;
        trackLine.endWidth = trackWidth;
        trackLine.material = new Material(Shader.Find("Sprites/Default"));
        trackLine.startColor = trackColor;
        trackLine.endColor = trackColor;
        trackLine.alignment = LineAlignment.TransformZ;

        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;

        InitializeReplay();
        }

        void Update()
        {
        if (!isPlaying || sampler == null || !sampler.IsValid) return;

        replayTimeSeconds += Time.deltaTime * speedMultiplier;
        float clampedReplayTime = Mathf.Min(replayTimeSeconds, replayEndTimeSeconds);

        Vector3 sampledPosition = sampler.SamplePosition(clampedReplayTime);
        sampledPosition.y += carVerticalOffset;
        transform.position = sampledPosition;

        Vector3 forward = sampler.SampleForward(clampedReplayTime);
        if (forward.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(forward, Vector3.up);
            Quaternion targetRotation = Quaternion.Euler(fixedXRotation, lookRot.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothness);
        }

        if (replayTimeSeconds >= replayEndTimeSeconds)
        {
            isPlaying = false;
        }
        }

        void InitializeReplay()
        {
        // 1. Load Car Data (Determines the shared coordinate offset)
        replayTrack = FastF1CsvImporter.LoadTrackFromFile(carFilePath);
        if (replayTrack.samples.Count < 2) return;

        // 2. Generate the 3D Track Surface using the SAME offset from the car
        // We use Vector3.zero for the track offset if the track CSV is already centered,
        // but it must align with the car's global reference.
        GenerateTrackMesh();

        // 3. Setup Replay Logic (Original Code)
        sampler = new TrajectorySampler(replayTrack);
        DrawFullTrack(replayTrack); // Draws the racing line
        replayTimeSeconds = sampler.StartTime;
        replayEndTimeSeconds = sampler.EndTime;
        isPlaying = true;

        Vector3 startPosition = sampler.SamplePosition(replayTimeSeconds);
        startPosition.y += carVerticalOffset;
        transform.position = startPosition;

        Vector3 startForward = sampler.SampleForward(replayTimeSeconds);
        if (startForward.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(startForward, Vector3.up);
            transform.rotation = Quaternion.Euler(fixedXRotation, lookRot.eulerAngles.y, 0f);
        }
        }

        void GenerateTrackMesh()
        {
        if (!File.Exists(trackFilePath))
        {
            Debug.LogError("Track CSV not found at: " + trackFilePath);
            return;
        }

        string[] lines = File.ReadAllLines(trackFilePath);
        Mesh mesh = TrackRibbonMeshFromCsv.BuildRibbonMesh(
            lines,
            Vector3.zero,
            1f,
            false,
            12f,
            false,
            true);

        GameObject trackObj = new GameObject("Procedural_Track_Surface");
        trackObj.AddComponent<MeshFilter>().mesh = mesh;
        trackObj.AddComponent<MeshRenderer>().material = trackSurfaceMaterial != null ? trackSurfaceMaterial : new Material(Shader.Find("Standard"));

        if (trackSurfaceMaterial == null)
        {
            trackObj.GetComponent<MeshRenderer>().material.color = Color.gray;
        }
        }

        void DrawFullTrack(DriverReplayTrack track)
        {
        // This still draws the thin line representing the driver's actual path
        List<Vector3> allPoints = sampler != null && sampler.IsValid
            ? sampler.BuildSampledPath(lineSampleTimeStep)
            : new List<Vector3>();

        if (allPoints.Count == 0)
        {
            foreach (ReplaySample sample in track.samples)
            {
                allPoints.Add(sample.worldPosition);
            }
        }

        trackLine.positionCount = allPoints.Count;
        trackLine.SetPositions(allPoints.ToArray());
        }
    }
}
