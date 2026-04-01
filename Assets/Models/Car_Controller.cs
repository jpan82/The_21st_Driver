using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class F1_Driver_Follower : MonoBehaviour
{
    [HideInInspector] public string csvPath;
    [HideInInspector] public Vector3 globalOffset;
    [HideInInspector] public float uniqueYOffset;
    [HideInInspector] public Color pathColor;
    [HideInInspector] public float racingLineWidth;

    public float speedMultiplier = 0.5f;
    public float carVerticalOffset = 0.2f;

    void Start()
    {
        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;
        StartCoroutine(DriveSequence());
    }

    IEnumerator DriveSequence()
    {
        if (!File.Exists(csvPath)) yield break;
        string[] lines = File.ReadAllLines(csvPath);
        if (lines.Length < 3) yield break;

        LineRenderer lr = gameObject.AddComponent<LineRenderer>();
        lr.startWidth = lr.endWidth = racingLineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = pathColor;
        lr.alignment = LineAlignment.View;
        lr.sortingOrder = -1;

        List<Vector3> points = new List<Vector3>();
        for (int j = 1; j < lines.Length; j++)
        {
            string[] c = lines[j].Split(',');
            Vector3 p = new Vector3(float.Parse(c[1]), float.Parse(c[3]) + uniqueYOffset, float.Parse(c[2])) + globalOffset;
            points.Add(p);
        }
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());

        for (int i = 1; i < lines.Length - 1; i++)
        {
            string[] cur = lines[i].Split(',');
            string[] nxt = lines[i+1].Split(',');

            Vector3 startPos = new Vector3(float.Parse(cur[1]), float.Parse(cur[3]) + carVerticalOffset + uniqueYOffset, float.Parse(cur[2])) + globalOffset;
            Vector3 endPos = new Vector3(float.Parse(nxt[1]), float.Parse(nxt[3]) + carVerticalOffset + uniqueYOffset, float.Parse(nxt[2])) + globalOffset;
            float duration = (float.Parse(nxt[0]) - float.Parse(cur[0])) / speedMultiplier;

            if (duration > 0)
            {
                Vector3 dir = endPos - startPos;
                dir.y = 0;
                if (dir.magnitude > 0.05f) transform.rotation = Quaternion.LookRotation(dir.normalized);

                float elapsed = 0;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
                    yield return null;
                }
            }
        }
    }
}