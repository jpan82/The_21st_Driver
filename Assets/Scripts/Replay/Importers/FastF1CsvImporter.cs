using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class FastF1CsvImporter
{
    public static ReplaySession LoadSessionFromFolder(string folderPath)
    {
        ReplaySession session = new ReplaySession
        {
            sessionId = Path.GetFileName(folderPath),
            sourceFolderPath = folderPath,
            globalOffset = Vector3.zero
        };

        if (!Directory.Exists(folderPath))
        {
            Debug.LogError("Cannot find replay folder: " + folderPath);
            return session;
        }

        string[] csvFiles = Directory.GetFiles(folderPath, "*.csv");
        if (csvFiles.Length == 0)
        {
            Debug.LogWarning("No replay csv files found in: " + folderPath);
            return session;
        }

        System.Array.Sort(csvFiles);

        Vector3? sharedOffset = null;
        foreach (string filePath in csvFiles)
        {
            DriverReplayTrack track = LoadTrackFromFile(filePath, ref sharedOffset);
            if (track.samples.Count == 0)
            {
                continue;
            }

            session.tracks.Add(track);
        }

        session.globalOffset = sharedOffset ?? Vector3.zero;
        return session;
    }

    public static DriverReplayTrack LoadTrackFromFile(string filePath)
    {
        Vector3? sharedOffset = null;
        return LoadTrackFromFile(filePath, ref sharedOffset);
    }

    public static DriverReplayTrack LoadTrackFromFile(string filePath, Vector3 sharedOffset)
    {
        Vector3? offset = sharedOffset;
        return LoadTrackFromFile(filePath, ref offset);
    }

    private static DriverReplayTrack LoadTrackFromFile(string filePath, ref Vector3? sharedOffset)
    {
        DriverReplayTrack track = new DriverReplayTrack
        {
            driverId = Path.GetFileNameWithoutExtension(filePath),
            sourcePath = filePath
        };

        if (!File.Exists(filePath))
        {
            Debug.LogError("Cannot find replay file: " + filePath);
            return track;
        }

        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
        {
            Debug.LogWarning("Replay file has no sample rows: " + filePath);
            return track;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (!TryParseSample(lines[i], out float timeSeconds, out Vector3 rawPosition))
            {
                continue;
            }

            if (!sharedOffset.HasValue)
            {
                sharedOffset = -rawPosition;
            }

            ReplaySample sample = new ReplaySample
            {
                sessionTimeSeconds = timeSeconds,
                rawPosition = rawPosition,
                worldPosition = rawPosition + sharedOffset.Value
            };

            track.samples.Add(sample);
        }

        PopulateDerivedData(track);
        return track;
    }

    private static bool TryParseSample(string line, out float timeSeconds, out Vector3 rawPosition)
    {
        timeSeconds = 0f;
        rawPosition = Vector3.zero;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string[] cols = line.Split(',');
        if (cols.Length < 4)
        {
            return false;
        }

        if (!TryParseFloat(cols[0], out timeSeconds) ||
            !TryParseFloat(cols[1], out float posX) ||
            !TryParseFloat(cols[2], out float posY) ||
            !TryParseFloat(cols[3], out float posZ))
        {
            return false;
        }

        rawPosition = new Vector3(posX, posZ, posY);
        return true;
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static void PopulateDerivedData(DriverReplayTrack track)
    {
        for (int i = 0; i < track.samples.Count; i++)
        {
            ReplaySample current = track.samples[i];

            if (i >= track.samples.Count - 1)
            {
                current.speedMetersPerSecond = 0f;
                continue;
            }

            ReplaySample next = track.samples[i + 1];
            Vector3 segment = next.worldPosition - current.worldPosition;
            float deltaTime = next.sessionTimeSeconds - current.sessionTimeSeconds;
            Vector3 flatSegment = new Vector3(segment.x, 0f, segment.z);

            current.speedMetersPerSecond = deltaTime > 0f ? flatSegment.magnitude / deltaTime : 0f;

            if (flatSegment.sqrMagnitude > 0.0001f)
            {
                current.headingYawDegrees = Quaternion.LookRotation(flatSegment.normalized, Vector3.up).eulerAngles.y;
            }
            else if (i > 0)
            {
                current.headingYawDegrees = track.samples[i - 1].headingYawDegrees;
            }
        }

        if (track.samples.Count > 1)
        {
            track.samples[track.samples.Count - 1].headingYawDegrees =
                track.samples[track.samples.Count - 2].headingYawDegrees;
        }
    }
}
