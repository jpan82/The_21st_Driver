using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class F1_Driver_Follower : MonoBehaviour
{
    [HideInInspector] public DriverReplayTrack replayTrack;
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
        DriverReplayTrack track = LoadTrackIfNeeded();
        if (track == null || track.samples.Count < 2) yield break;

        LineRenderer lr = gameObject.AddComponent<LineRenderer>();
        lr.startWidth = lr.endWidth = racingLineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = pathColor;
        lr.alignment = LineAlignment.View;
        lr.sortingOrder = -1;

        List<Vector3> points = new List<Vector3>();
        foreach (ReplaySample sample in track.samples)
        {
            Vector3 p = sample.worldPosition + new Vector3(0f, uniqueYOffset, 0f);
            points.Add(p);
        }
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());

        for (int i = 0; i < track.samples.Count - 1; i++)
        {
            ReplaySample cur = track.samples[i];
            ReplaySample nxt = track.samples[i + 1];

            Vector3 startPos = cur.worldPosition + new Vector3(0f, carVerticalOffset + uniqueYOffset, 0f);
            Vector3 endPos = nxt.worldPosition + new Vector3(0f, carVerticalOffset + uniqueYOffset, 0f);
            float duration = (nxt.sessionTimeSeconds - cur.sessionTimeSeconds) / speedMultiplier;

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

    DriverReplayTrack LoadTrackIfNeeded()
    {
        if (replayTrack != null && replayTrack.samples.Count > 0)
        {
            return replayTrack;
        }

        if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
        {
            return null;
        }

        replayTrack = FastF1CsvImporter.LoadTrackFromFile(csvPath, globalOffset);
        return replayTrack;
    }
}