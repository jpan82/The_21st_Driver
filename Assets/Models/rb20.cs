using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class CSVMovementPlayer : MonoBehaviour
{
    [Header("File Settings")]
    public string fileName = "f1_motion_dump/ALB_data.csv";
    [Range(0.1f, 5f)]
    public float speedMultiplier = 0.5f; 

    [Header("Track Visuals")]
    public Color trackColor = new Color(0.2f, 0.2f, 0.2f, 1.0f); 
    public float trackWidth = 15f; 
    public float lineSampleTimeStep = 0.05f;
    
    [Header("Overlap Fix")]
    [Tooltip("Raise the car slightly so it doesn't clip through the track.")]
    public float carVerticalOffset = 0.2f; 
    public float fixedXRotation = -90f;
    public float rotationSmoothness = 10f;

    private string filePath;
    private LineRenderer trackLine;
    private DriverReplayTrack replayTrack;
    private TrajectorySampler sampler;
    private float replayTimeSeconds;
    private float replayEndTimeSeconds;
    private bool isPlaying;

    void Start()
    {
        filePath = Path.Combine(Application.streamingAssetsPath, fileName);
        
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
        if (!isPlaying || sampler == null || !sampler.IsValid)
        {
            return;
        }

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
        replayTrack = FastF1CsvImporter.LoadTrackFromFile(filePath);
        if (replayTrack.samples.Count < 2)
        {
            return;
        }

        sampler = new TrajectorySampler(replayTrack);
        DrawFullTrack(replayTrack);
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

    void DrawFullTrack(DriverReplayTrack track)
    {
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