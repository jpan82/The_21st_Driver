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
    private Vector3 csvOffset;
    private LineRenderer trackLine;

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
        if (!File.Exists(filePath)) yield break;
        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) yield break;

        // Calculate Offset
        string[] firstRow = lines[1].Split(',');
        float fX = float.Parse(firstRow[1]);
        float fY = float.Parse(firstRow[3]); 
        float fZ = float.Parse(firstRow[2]); 
        csvOffset = Vector3.zero - new Vector3(fX, fY, fZ);

        // Draw the track exactly on the data points
        DrawFullTrack(lines);

        // Teleport car to start + offset
        transform.position = Vector3.up * carVerticalOffset;
        yield return new WaitForSeconds(1.5f); 

        yield return StartCoroutine(MoveCar(lines));
    }

    void DrawFullTrack(string[] lines)
    {
        List<Vector3> allPoints = new List<Vector3>();
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            // Mapping: X, Altitude, Z
            Vector3 pos = new Vector3(float.Parse(cols[1]), float.Parse(cols[3]), float.Parse(cols[2])) + csvOffset;
            allPoints.Add(pos);
        }
        trackLine.positionCount = allPoints.Count;
        trackLine.SetPositions(allPoints.ToArray());
    }

    IEnumerator MoveCar(string[] lines)
    {
        for (int i = 1; i < lines.Length - 1; i++)
        {
            string[] curLine = lines[i].Split(',');
            string[] nextLine = lines[i + 1].Split(',');

            // Car position includes the Vertical Offset
            Vector3 startPos = new Vector3(float.Parse(curLine[1]), float.Parse(curLine[3]) + carVerticalOffset, float.Parse(curLine[2])) + csvOffset;
            Vector3 endPos = new Vector3(float.Parse(nextLine[1]), float.Parse(nextLine[3]) + carVerticalOffset, float.Parse(nextLine[2])) + csvOffset;

            float duration = (float.Parse(nextLine[0]) - float.Parse(curLine[0])) / speedMultiplier;

            // Rotation
            Vector3 moveDir = (endPos - startPos).normalized;
            if (moveDir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Euler(fixedXRotation, lookRot.eulerAngles.y, 0f);
            }

            // Movement
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