using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using The21stDriver.Replay.Data;

namespace The21stDriver.Replay.Importers
{
    public static class FastF1CsvImporter
    {
        private static readonly Regex PandasTimedeltaRegex = new Regex(
            @"^(?<sign>-?)\s*(?:(?<d>\d+(?:\.\d+)?)\s+days?\s+)?(?<h>\d+):(?<m>\d+):(?<s>\d+(?:\.\d+)?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // --- Used by Race_Controller ---
        public static DriverReplayTrack LoadDriverCsvForRaceController(string filePath, float interval, float yOff, Vector3 globalOffset)
        {
            return LoadNewTelemetryFormat(filePath, yOff, globalOffset);
        }

        // --- Used by F1_Driver_Follower ---
        public static DriverReplayTrack LoadTrackFromFile(string filePath)
        {
            return LoadNewTelemetryFormat(filePath, 0.25f, Vector3.zero);
        }

        public static DriverReplayTrack LoadTrackFromFile(string filePath, Vector3 sharedOffset)
        {
            return LoadNewTelemetryFormat(filePath, 0.25f, sharedOffset);
        }

        // --- The Core Loader ---
        private static DriverReplayTrack LoadNewTelemetryFormat(string filePath, float yOff, Vector3 offset)
        {
            DriverReplayTrack track = new DriverReplayTrack { driverId = Path.GetFileNameWithoutExtension(filePath), sourcePath = filePath };
            if (!File.Exists(filePath)) return track;

            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) return track;

            Dictionary<string, int> col = BuildHeaderColumnIndex(lines[0]);
            
            int iT = col.ContainsKey("SessionTime") ? col["SessionTime"] : -1;
            
            // Prioritize x_ref and y_ref so the cars match the track's coordinate space exactly
            int iX = col.ContainsKey("x_ref") ? col["x_ref"] : (col.ContainsKey("X") ? col["X"] : 0);
            int iZ = col.ContainsKey("y_ref") ? col["y_ref"] : (col.ContainsKey("Y") ? col["Y"] : 1);

            float? t0 = null;
            for (int r = 1; r < lines.Length; r++)
            {
                string[] f = lines[r].Split(',');
                if (f.Length <= Mathf.Max(iX, iZ)) continue;

                // Handle the 0.2s interval correctly
                float tAbs;
                if (iT != -1 && TryParsePandasTime(f[iT], out float parsedTime)) {
                    tAbs = parsedTime;
                } else {
                    tAbs = (r - 1) * 0.2f; // Fallback to 0.2s intervals
                }

                if (!float.TryParse(f[iX], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) continue;
                if (!float.TryParse(f[iZ], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) continue;

                if (!t0.HasValue) t0 = tAbs;

                // Map CSV X to Unity X, and CSV Y to Unity Z
                Vector3 pos = new Vector3(x, yOff, z);
                track.samples.Add(new ReplaySample {
                    sessionTimeSeconds = tAbs - t0.Value,
                    rawPosition = pos,
                    worldPosition = pos + offset
                });
            }

            CalculateMovementData(track);
            return track;
        }

        private static void CalculateMovementData(DriverReplayTrack track)
        {
            for (int i = 0; i < track.samples.Count - 1; i++)
            {
                ReplaySample cur = track.samples[i];
                ReplaySample nxt = track.samples[i + 1];
                float dt = nxt.sessionTimeSeconds - cur.sessionTimeSeconds;
                if (dt > 0) {
                    cur.speedMetersPerSecond = (nxt.worldPosition - cur.worldPosition).magnitude / dt;
                    Vector3 dir = (nxt.worldPosition - cur.worldPosition);
                    if (dir.sqrMagnitude > 0.001f)
                        cur.headingYawDegrees = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)).eulerAngles.y;
                }
            }
        }

        private static Dictionary<string, int> BuildHeaderColumnIndex(string header)
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string[] parts = header.Split(',');
            for (int i = 0; i < parts.Length; i++) {
                string key = parts[i].Trim().Replace("\"", "");
                if (!string.IsNullOrEmpty(key) && !d.ContainsKey(key)) d[key] = i;
            }
            return d;
        }

        private static bool TryParsePandasTime(string val, out float sec)
        {
            sec = 0;
            Match m = PandasTimedeltaRegex.Match(val.Trim().Replace("\"", ""));
            if (!m.Success) return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out sec);
            sec = (float.Parse(m.Groups["d"].Success ? m.Groups["d"].Value : "0") * 86400f +
                   int.Parse(m.Groups["h"].Value) * 3600f + 
                   int.Parse(m.Groups["m"].Value) * 60f + 
                   float.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture));
            return true;
        }
    }
}