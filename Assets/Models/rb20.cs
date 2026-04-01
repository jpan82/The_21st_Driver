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
    
    [Header("Overlap Fix")]
    [Tooltip("Raise the car slightly so it doesn't clip through the track.")]
    public float carVerticalOffset = 0.2f; 
    public float fixedXRotation = -90f;

    private string filePath;
    private LineRenderer trackLine;
    private DriverReplayTrack replayTrack;

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

        StartCoroutine(ExecuteRaceSequence());
    }

    IEnumerator ExecuteRaceSequence()
    {
        replayTrack = FastF1CsvImporter.LoadTrackFromFile(filePath);
        if (replayTrack.samples.Count < 2) yield break;

        DrawFullTrack(replayTrack);

        transform.position = Vector3.up * carVerticalOffset;
        yield return new WaitForSeconds(1.5f); 

        yield return StartCoroutine(MoveCar(replayTrack));
    }

    void DrawFullTrack(DriverReplayTrack track)
    {
        List<Vector3> allPoints = new List<Vector3>();
        foreach (ReplaySample sample in track.samples)
        {
            allPoints.Add(sample.worldPosition);
        }
        trackLine.positionCount = allPoints.Count;
        trackLine.SetPositions(allPoints.ToArray());
    }

    IEnumerator MoveCar(DriverReplayTrack track)
    {
        for (int i = 0; i < track.samples.Count - 1; i++)
        {
            ReplaySample cur = track.samples[i];
            ReplaySample next = track.samples[i + 1];

            Vector3 startPos = cur.worldPosition + Vector3.up * carVerticalOffset;
            Vector3 endPos = next.worldPosition + Vector3.up * carVerticalOffset;

            float duration = (next.sessionTimeSeconds - cur.sessionTimeSeconds) / speedMultiplier;

            Vector3 moveDir = (endPos - startPos).normalized;
            if (moveDir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Euler(fixedXRotation, lookRot.eulerAngles.y, 0f);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
                yield return null;
            }
        }
    }
}