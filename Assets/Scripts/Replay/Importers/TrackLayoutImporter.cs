using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

public class TrackLayoutImporter : MonoBehaviour
{
    public string trackFileName = "track_data/Silverstone.csv";
    public Material trackMaterial;
    public float trackHeight = 500f; // 这里的数值应参考赛车 CSV 里的 Z/高度值 (约 500-800)

    private Vector3 globalOffset;

    // 此方法用于生成赛道，并返回一个偏移量供赛车使用
    public Vector3 CreateTrackMesh(Vector3 sharedOffset)
    {
        string path = Path.Combine(Application.streamingAssetsPath, trackFileName);
        if (!File.Exists(path)) return Vector3.zero;

        string[] lines = File.ReadAllLines(path);
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            float x = float.Parse(cols[0], CultureInfo.InvariantCulture);
            float z = float.Parse(cols[1], CultureInfo.InvariantCulture);
            float wRight = float.Parse(cols[2], CultureInfo.InvariantCulture);
            float wLeft = float.Parse(cols[3], CultureInfo.InvariantCulture);

            // 转换为 Unity 坐标 (X, Height, Z)
            Vector3 center = new Vector3(x, trackHeight, z) + sharedOffset;
            Vector3 dir = (i < lines.Length - 1) ? 
                (new Vector3(float.Parse(lines[i+1].Split(',')[0]), trackHeight, float.Parse(lines[i+1].Split(',')[1])) - center).normalized : Vector3.forward;
            
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            // 生成左右边缘顶点
            vertices.Add(center + right * wRight); // 右侧
            vertices.Add(center - right * wLeft);  // 左侧

            if (i > 1)
            {
                int v = vertices.Count - 4;
                triangles.AddRange(new int[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        GameObject trackObj = new GameObject("Silverstone_TrackMesh");
        trackObj.AddComponent<MeshFilter>().mesh = mesh;
        trackObj.AddComponent<MeshRenderer>().material = trackMaterial;

        return sharedOffset;
    }
}