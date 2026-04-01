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
    public float rotationSmoothness = 10f;
    public float lineSampleTimeStep = 0.05f;

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
            Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothness);
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

        sampler = new TrajectorySampler(track);

        LineRenderer lr = gameObject.AddComponent<LineRenderer>();
        lr.startWidth = lr.endWidth = racingLineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = pathColor;
        lr.alignment = LineAlignment.View;
        lr.sortingOrder = -1;

        List<Vector3> points = sampler.BuildSampledPath(lineSampleTimeStep);
        for (int i = 0; i < points.Count; i++)
        {
            points[i] += new Vector3(0f, uniqueYOffset, 0f);
        }
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());

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