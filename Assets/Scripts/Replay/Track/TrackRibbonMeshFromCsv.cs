using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Builds a ribbon mesh from track CSV rows: center XZ, half-widths left/right in columns 2–3.
/// </summary>
namespace The21stDriver.Replay.Track
{
    public static class TrackRibbonMeshFromCsv
    {
        public static Mesh BuildRibbonMesh(
            string[] lines,
            Vector3 centerOffset,
            float widthMultiplier,
            bool generateUvs,
            float uvMetersPerRepeat,
            bool useUInt32Indices,
            bool legacyRb20TriangleWinding)
        {
            Mesh mesh = new Mesh();
            if (useUInt32Indices)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = generateUvs ? new List<Vector2>() : null;
            List<int> tris = new List<int>();

            float uAlong = 0f;
            Vector3 prevCenter = Vector3.zero;
            bool hasPrevCenter = false;
            float uScale = 1f / Mathf.Max(0.01f, uvMetersPerRepeat);

            for (int i = 1; i < lines.Length; i++)
            {
                string[] c = lines[i].Split(',');
                if (c.Length < 4)
                {
                    continue;
                }

                if (!TryParse(c[0], out float cx) || !TryParse(c[1], out float cz) ||
                    !TryParse(c[2], out float wRight) || !TryParse(c[3], out float wLeft))
                {
                    continue;
                }

                Vector3 center = new Vector3(cx, 0f, cz) + centerOffset;

                Vector3 forward = Vector3.forward;
                if (i < lines.Length - 1)
                {
                    string[] n = lines[i + 1].Split(',');
                    if (n.Length >= 2 &&
                        TryParse(n[0], out float nx) &&
                        TryParse(n[1], out float nz))
                    {
                        Vector3 next = new Vector3(nx, 0f, nz) + centerOffset;
                        forward = (next - center).normalized;
                    }
                }

                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                if (hasPrevCenter)
                {
                    uAlong += Vector3.Distance(prevCenter, center);
                }

                prevCenter = center;
                hasPrevCenter = true;

                if (generateUvs && uvs != null)
                {
                    float u = uAlong * uScale;
                    uvs.Add(new Vector2(u, 1f));
                    uvs.Add(new Vector2(u, 0f));
                }

                wRight *= widthMultiplier;
                wLeft *= widthMultiplier;

                verts.Add(center + right * wRight);
                verts.Add(center - right * wLeft);

                if (verts.Count >= 4)
                {
                    int v = verts.Count - 4;
                    if (legacyRb20TriangleWinding)
                    {
                        tris.Add(v);
                        tris.Add(v + 2);
                        tris.Add(v + 1);
                        tris.Add(v + 1);
                        tris.Add(v + 2);
                        tris.Add(v + 3);
                    }
                    else
                    {
                        tris.Add(v);
                        tris.Add(v + 1);
                        tris.Add(v + 2);
                        tris.Add(v + 1);
                        tris.Add(v + 3);
                        tris.Add(v + 2);
                    }
                }
            }

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            if (generateUvs && uvs != null && uvs.Count == verts.Count)
            {
                mesh.uv = uvs.ToArray();
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool TryParse(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
