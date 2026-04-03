using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

public class SmoothMover : MonoBehaviour
{
    private TrajectorySampler sampler;
    private Race_Controller ctrl;

    public void Init(DriverReplayTrack track, Race_Controller controller) {
        sampler = new TrajectorySampler(track);
        ctrl = controller;
    }

    void Update() {
        if (sampler == null || !sampler.IsValid) return;

        float duration = sampler.EndTime - sampler.StartTime;
        float t = sampler.StartTime + (ctrl.GlobalTime % duration);

        Vector3 currentPos = sampler.SamplePosition(t);
        transform.position = currentPos;

        float lookT = Mathf.Min(t + 0.05f, sampler.EndTime);
        Vector3 targetPos = sampler.SamplePosition(lookT);
        Vector3 direction = (targetPos - currentPos).normalized;

        if (direction.sqrMagnitude > 0.001f) {
            Quaternion lookRot = Quaternion.LookRotation(direction, Vector3.up);
            Quaternion targetRotation = Quaternion.Euler(ctrl.fixedXRotation, lookRot.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * ctrl.rotationSmoothness);
        }
    }
}

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
        DriverReplayTrack trackData = new DriverReplayTrack();
        string[] lines = File.ReadAllLines(path);

        for (int i = 1; i < lines.Length; i++) {
            string[] cols = lines[i].Split(',');
            if (cols.Length < 7) continue;

            float timestamp = (i - 1) * sampleInterval;
            float x = float.Parse(cols[5], CultureInfo.InvariantCulture);
            float z = float.Parse(cols[6], CultureInfo.InvariantCulture);

            ReplaySample s = new ReplaySample {
                sessionTimeSeconds = timestamp,
                worldPosition = new Vector3(x, carYOffset, z) + globalOffset
            };
            trackData.samples.Add(s);
        }

        if (trackData.samples.Count < 2) return;

        GameObject car = Instantiate(carPrefab);
        car.name = Path.GetFileNameWithoutExtension(path);
        if (car.TryGetComponent<Rigidbody>(out Rigidbody rb)) rb.isKinematic = true;

        car.AddComponent<SmoothMover>().Init(trackData, this);
    }

    void BuildTrack(string[] lines) {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        float uAlong = 0f;
        Vector3 prevCenter = Vector3.zero;
        bool hasPrevCenter = false;
        float uScale = 1f / Mathf.Max(0.01f, trackUvMetersPerRepeat);

        for (int i = 1; i < lines.Length; i++) {
            string[] c = lines[i].Split(',');
            if (c.Length < 4) continue;
            Vector3 center = new Vector3(float.Parse(c[0], CultureInfo.InvariantCulture), 0, float.Parse(c[1], CultureInfo.InvariantCulture)) + globalOffset;

            Vector3 forward = Vector3.forward;
            if (i < lines.Length - 1) {
                Vector3 next = new Vector3(float.Parse(lines[i+1].Split(',')[0], CultureInfo.InvariantCulture), 0, float.Parse(lines[i+1].Split(',')[1], CultureInfo.InvariantCulture)) + globalOffset;
                forward = (next - center).normalized;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            if (hasPrevCenter) {
                uAlong += Vector3.Distance(prevCenter, center);
            }
            prevCenter = center;
            hasPrevCenter = true;

            float u = uAlong * uScale;
            uvs.Add(new Vector2(u, 1f));
            uvs.Add(new Vector2(u, 0f));

            // 直接在这里硬编码宽度。
            // 现在的 1.5f 代表比真实宽度宽 1.5 倍。你可以改成 1.0f (真实宽度) 或是 2.0f (两倍宽)
            float wRight = float.Parse(c[2], CultureInfo.InvariantCulture) * 1.5f;
            float wLeft  = float.Parse(c[3], CultureInfo.InvariantCulture) * 1.5f;

            verts.Add(center + right * wRight);
            verts.Add(center - right * wLeft);

            if (i > 1) {
                int v = verts.Count - 4;
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                tris.Add(v + 1); tris.Add(v + 3); tris.Add(v + 2);
            }
        }
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

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
