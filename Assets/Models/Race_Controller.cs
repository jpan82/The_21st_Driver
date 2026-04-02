using System.IO;
using UnityEngine;

public class F1_RaceManager : MonoBehaviour
{
    public GameObject carPrefab;
    public string folderName = "f1_motion_dump";

    public float speedMultiplier = 0.5f;
    public float trackWidth = 20f;
    public Color trackColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    void Start()
    {
        if (carPrefab == null) {
            return;
        }

        string path = Path.Combine(Application.streamingAssetsPath, folderName);
        ReplaySession session = FastF1CsvImporter.LoadSessionFromFolder(path);
        if (session.tracks.Count == 0)
        {
            return;
        }

        for (int carIndex = 0; carIndex < session.tracks.Count; carIndex++)
        {
            // int carIndex = 0;
            DriverReplayTrack track = session.tracks[carIndex];

            GameObject car = Instantiate(carPrefab);
            car.name = track.driverId;

            F1_Driver_Follower driver = car.GetComponent<F1_Driver_Follower>();
            if (driver == null) driver = car.AddComponent<F1_Driver_Follower>();

            driver.replayTrack = track;
            driver.speedMultiplier = speedMultiplier;
            driver.uniqueYOffset = carIndex * 0.01f;
            driver.racingLineWidth = trackWidth;
            driver.pathColor = trackColor;
        }
    }
}
