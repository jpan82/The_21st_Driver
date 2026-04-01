using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class F1_RaceManager : MonoBehaviour
{
    public GameObject carPrefab;
    public string folderName = "f1_motion_dump";

    public float speedMultiplier = 0.5f;
    public float trackWidth = 20f;
    public Color trackColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    private Vector3 globalOffset;
    private bool offsetInitialized = false;

    void Start()
    {
        if (carPrefab == null) {
            return;
        }

        string path = Path.Combine(Application.streamingAssetsPath, folderName);
        if (!Directory.Exists(path)) {
            Debug.LogError("Cannot find the file: " + path);
            return;
        }

        string[] csvFiles = Directory.GetFiles(path, "*.csv");
        int carIndex = 0;

        foreach (string file in csvFiles)
        {
            if (!offsetInitialized)
            {
                InitializeGlobalOffset(file);
                offsetInitialized = true;
            }

            GameObject car = Instantiate(carPrefab);
            car.name = Path.GetFileNameWithoutExtension(file);

            F1_Driver_Follower driver = car.GetComponent<F1_Driver_Follower>();
            if (driver == null) driver = car.AddComponent<F1_Driver_Follower>();

            driver.csvPath = file;
            driver.globalOffset = globalOffset; 
            driver.speedMultiplier = speedMultiplier;
            driver.uniqueYOffset = carIndex * 0.01f;
            driver.racingLineWidth = trackWidth;
            driver.pathColor = trackColor;

            carIndex++;
        }
    }

    void InitializeGlobalOffset(string file)
    {
        string[] lines = File.ReadAllLines(file);
        if (lines.Length > 1) {
            string[] cols = lines[1].Split(',');
            globalOffset = Vector3.zero - new Vector3(float.Parse(cols[1]), float.Parse(cols[3]), float.Parse(cols[2]));
        }
    }
}