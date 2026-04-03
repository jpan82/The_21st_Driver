using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

public class Race_Controller : MonoBehaviour
{
    [Header("Assets")]
    public GameObject carPrefab;

    [Header("Settings")]
    public float playbackSpeed = 90f; 
    public string trackFolder = "track_data";
    public string trackFileName = "Silverstone.csv";
    public string carFolder = "f1_motion_dump";
    
    [Header("Car Alignment")]
    [Tooltip("Lifts the car vertically. Set to 1.0 as requested.")]
    public float carYOffset = 1.0f; 

    [Header("Camera Settings")]
    public float followHeight = 5f;
    public float followDistance = 15f;
    public float cameraFOV = 80f;

    [Header("Track Visual")]
    [Tooltip("Material with Base Map; if null, use grey Unlit shader.")]
    public Material trackMaterial;
    public float trackUvMetersPerRepeat = 12f;

    private Vector3 globalOffset;
    private List<Transform> spawnedCars = new List<Transform>();
    private int currentFollowIndex = 0;
    private bool isFollowing = false;

    // DATA
    [System.Serializable]
    public class CarPathData {
        public List<Vector3> positions = new List<Vector3>();
        public static CarPathData Load(string path, Vector3 offset, float yLift) {
            CarPathData data = new CarPathData();
            if (!File.Exists(path)) return data;
            string[] lines = File.ReadAllLines(path);
            
            for (int i = 1; i < lines.Length; i++) {
                string[] cols = lines[i].Split(',');
                if (cols.Length < 7) continue;

                float x = float.Parse(cols[5], CultureInfo.InvariantCulture);
                float z = float.Parse(cols[6], CultureInfo.InvariantCulture);
                
                float altitude = 0f;
                if (cols.Length > 7) {
                    altitude = float.Parse(cols[7], CultureInfo.InvariantCulture);
                }

                // Unity Position: [X, Altitude + 1.0, Z]
                data.positions.Add(new Vector3(x, altitude + yLift, z) + offset);
            }
            return data;
        }
    }

    // Mover
    public class ReferenceCarMover : MonoBehaviour {
        public List<Vector3> path; public float speed; private int index = 0;
        void Update() {
            if (path == null || index >= path.Count - 1) return;
            transform.position = Vector3.MoveTowards(transform.position, path[index + 1], speed * Time.deltaTime);
            
            Vector3 dir = path[index + 1] - transform.position;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
            if (Vector3.Distance(transform.position, path[index + 1]) < 0.1f) index++;
        }
    }

    void Start() {
		carYOffset = 1f;
        string tPath = Path.Combine(Application.streamingAssetsPath, trackFolder, trackFileName);
        string cDirPath = Path.Combine(Application.streamingAssetsPath, carFolder);

        if (!File.Exists(tPath)) return;

        string[] lines = File.ReadAllLines(tPath);
        globalOffset = -new Vector3(float.Parse(lines[1].Split(',')[0], CultureInfo.InvariantCulture), 0, float.Parse(lines[1].Split(',')[1], CultureInfo.InvariantCulture));

        BuildTrack(lines);
        SetupInitialCamera();

        if (Directory.Exists(cDirPath)) {
            string[] carFiles = Directory.GetFiles(cDirPath, "*.csv");
            foreach (string file in carFiles) {
                SpawnCar(file);
            }
        }
    }

    void Update() {
        // Toggle following with 'C'
        if (Input.GetKeyDown(KeyCode.C)) {
            if (!isFollowing) isFollowing = true;
            else {
                currentFollowIndex++;
                if (currentFollowIndex >= spawnedCars.Count) {
                    isFollowing = false;
                    currentFollowIndex = 0;
                    SetupInitialCamera();
                }
            }
        }

        if (isFollowing && spawnedCars.Count > 0) {
            Transform target = spawnedCars[currentFollowIndex];
            Camera.main.fieldOfView = cameraFOV;
            Vector3 targetPos = target.position + target.TransformDirection(new Vector3(0, followHeight, -followDistance));
            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, targetPos, Time.deltaTime * 10f);
            Camera.main.transform.LookAt(target.position + Vector3.up * 1.5f);
        }
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

            verts.Add(center + right * float.Parse(c[2], CultureInfo.InvariantCulture)); 
            verts.Add(center - right * float.Parse(c[3], CultureInfo.InvariantCulture)); 

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
            if (fallback == null) {
                fallback = Shader.Find("Unlit/Color");
            }
            Material m = new Material(fallback);
            if (fallback.name.Contains("Universal")) {
                m.SetColor("_BaseColor", Color.grey);
            } else {
                m.color = Color.grey;
            }
            mr.material = m;
        }
    }

    void SetupInitialCamera() {
        Camera.main.farClipPlane = 10000f;
        Camera.main.transform.position = new Vector3(0, 100, -50);
        Camera.main.transform.rotation = Quaternion.Euler(60, 0, 0);
    }

    void SpawnCar(string path) {
        CarPathData data = CarPathData.Load(path, globalOffset, carYOffset);
        if (data.positions.Count == 0 || carPrefab == null) return;
		
        GameObject car = Instantiate(carPrefab);
        car.name = Path.GetFileNameWithoutExtension(path);
        spawnedCars.Add(car.transform);
        
        var mover = car.AddComponent<ReferenceCarMover>();
        mover.path = data.positions;
        mover.speed = playbackSpeed;
        car.transform.position = data.positions[0];
    }
}