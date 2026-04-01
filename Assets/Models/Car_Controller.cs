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

    private TrajectorySampler sampler;
    private float replayTimeSeconds;
    private float replayEndTimeSeconds;
    private bool isPlaying;

    void Start()
    {
        if (GetComponent<Rigidbody>()) GetComponent<Rigidbody>().isKinematic = true;
        InitializeReplay();
    }

    void Update()
    {
        if (!isPlaying || sampler == null || !sampler.IsValid)
        {
            return;
        }

        replayTimeSeconds += Time.deltaTime * speedMultiplier;
        float clampedReplayTime = Mathf.Min(replayTimeSeconds, replayEndTimeSeconds);

        Vector3 sampledPosition = sampler.SamplePosition(clampedReplayTime);
        sampledPosition.y += carVerticalOffset + uniqueYOffset;
        transform.position = sampledPosition;

        Vector3 forward = sampler.SampleForward(clampedReplayTime);
        if (forward.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        if (replayTimeSeconds >= replayEndTimeSeconds)
        {
            isPlaying = false;
        }
    }

    void InitializeReplay()
    {
        DriverReplayTrack track = LoadTrackIfNeeded();
        if (track == null || track.samples.Count < 2)
        {
            return;
        }

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

        sampler = new TrajectorySampler(track);
        replayTimeSeconds = sampler.StartTime;
        replayEndTimeSeconds = sampler.EndTime;
        isPlaying = true;

        Vector3 startPosition = sampler.SamplePosition(replayTimeSeconds);
        startPosition.y += carVerticalOffset + uniqueYOffset;
        transform.position = startPosition;

        Vector3 startForward = sampler.SampleForward(replayTimeSeconds);
        if (startForward.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(startForward, Vector3.up);
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