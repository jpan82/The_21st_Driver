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
        private const float TimeDedupeEpsilon = 1e-4f;
        private const float MinDtForSpeedCompare = 1e-5f;

        private static readonly Regex PandasTimedeltaRegex = new Regex(
            @"^(?<sign>-?)\s*(?:(?<d>\d+(?:\.\d+)?)\s+days?\s+)?(?<h>\d+):(?<m>\d+):(?<s>\d+(?:\.\d+)?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // --- Used by Race_Controller ---
        public static DriverReplayTrack LoadDriverCsvForRaceController(
            string filePath,
            float interval,
            float yOff,
            Vector3 globalOffset,
            int replayXzSmoothPasses,
            bool replaySpeedOutlierRepair)
        {
            return LoadNewTelemetryFormat(filePath, yOff, globalOffset, replayXzSmoothPasses, replaySpeedOutlierRepair);
        }

        // --- Used by F1_Driver_Follower ---
        public static DriverReplayTrack LoadTrackFromFile(string filePath)
        {
            return LoadNewTelemetryFormat(filePath, 0.25f, Vector3.zero, 0, false);
        }

        public static DriverReplayTrack LoadTrackFromFile(string filePath, Vector3 sharedOffset)
        {
            return LoadNewTelemetryFormat(filePath, 0.25f, sharedOffset, 0, false);
        }

        // --- The Core Loader ---
        private static DriverReplayTrack LoadNewTelemetryFormat(
            string filePath,
            float yOff,
            Vector3 offset,
            int xzSmoothPasses,
            bool speedOutlierRepair)
        {
            DriverReplayTrack track = new DriverReplayTrack { driverId = Path.GetFileNameWithoutExtension(filePath), sourcePath = filePath };
            if (!File.Exists(filePath)) return track;

            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) return track;

            Dictionary<string, int> col = BuildHeaderColumnIndex(lines[0]);
            
            int iT = col.ContainsKey("SessionTime") ? col["SessionTime"] : -1;

            if (!col.ContainsKey("x_ref") || !col.ContainsKey("y_ref"))
            {
                Debug.LogError($"[FastF1CsvImporter] {filePath}: expected x_ref and y_ref in header (missing column).");
                return track;
            }

            int iX = col["x_ref"];
            int iZ = col["y_ref"];
            int iSpeed = col.ContainsKey("Speed") ? col["Speed"] : -1;

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

                bool hasSpd = false;
                float spd = 0f;
                if (iSpeed >= 0 && iSpeed < f.Length &&
                    float.TryParse(f[iSpeed], NumberStyles.Float, CultureInfo.InvariantCulture, out spd))
                {
                    hasSpd = true;
                }

                // Map CSV X to Unity X, and CSV Y to Unity Z
                Vector3 pos = new Vector3(x, yOff, z);
                track.samples.Add(new ReplaySample {
                    sessionTimeSeconds = tAbs - t0.Value,
                    rawPosition = pos,
                    worldPosition = pos + offset,
                    telemetrySpeed = spd,
                    hasTelemetrySpeed = hasSpd
                });
            }

            PostProcessSamples(track, offset, xzSmoothPasses, speedOutlierRepair);
            return track;
        }

        private static void PostProcessSamples(
            DriverReplayTrack track,
            Vector3 offset,
            int xzSmoothPasses,
            bool speedOutlierRepair)
        {
            if (track.samples.Count == 0)
            {
                return;
            }

            track.samples.Sort((a, b) => a.sessionTimeSeconds.CompareTo(b.sessionTimeSeconds));

            List<ReplaySample> deduped = new List<ReplaySample>(track.samples.Count);
            for (int i = 0; i < track.samples.Count; i++)
            {
                ReplaySample s = track.samples[i];
                if (deduped.Count == 0)
                {
                    deduped.Add(s);
                    continue;
                }

                ReplaySample prev = deduped[deduped.Count - 1];
                if (s.sessionTimeSeconds <= prev.sessionTimeSeconds + TimeDedupeEpsilon)
                {
                    prev.worldPosition = s.worldPosition;
                    prev.rawPosition = s.rawPosition;
                    if (s.hasTelemetrySpeed)
                    {
                        prev.telemetrySpeed = s.telemetrySpeed;
                        prev.hasTelemetrySpeed = true;
                    }
                    continue;
                }

                deduped.Add(s);
            }

            track.samples = deduped;

            int xzPasses = Mathf.Max(0, xzSmoothPasses);
            if (xzPasses > 0)
            {
                ApplyXzBinomialSmooth(track, offset, xzPasses);
            }

            if (speedOutlierRepair)
            {
                ApplySpeedGatedOutlierRepair(track, offset);
            }

            CalculateMovementData(track);
        }

        /// <summary>Light [1/4, 1/2, 1/4] blur on horizontal path to cut high-frequency telemetry wobble.</summary>
        private static void ApplyXzBinomialSmooth(DriverReplayTrack track, Vector3 offset, int passes)
        {
            int n = track.samples.Count;
            if (n < 3)
            {
                return;
            }

            for (int p = 0; p < passes; p++)
            {
                var buf = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    buf[i] = track.samples[i].worldPosition;
                }

                for (int i = 1; i < n - 1; i++)
                {
                    Vector3 a = buf[i - 1];
                    Vector3 b = buf[i];
                    Vector3 c = buf[i + 1];
                    float x = 0.25f * a.x + 0.5f * b.x + 0.25f * c.x;
                    float z = 0.25f * a.z + 0.5f * b.z + 0.25f * c.z;
                    Vector3 w = new Vector3(x, b.y, z);
                    track.samples[i].worldPosition = w;
                    track.samples[i].rawPosition = w - offset;
                }
            }
        }

        /// <summary>A 档：几何段速度远大于两端 telemetry Speed 时，沿弦方向缩短位移。</summary>
        private static void ApplySpeedGatedOutlierRepair(DriverReplayTrack track, Vector3 offset)
        {
            for (int i = 0; i < track.samples.Count - 1; i++)
            {
                ReplaySample cur = track.samples[i];
                ReplaySample nxt = track.samples[i + 1];
                float dt = nxt.sessionTimeSeconds - cur.sessionTimeSeconds;
                if (dt < MinDtForSpeedCompare || !cur.hasTelemetrySpeed || !nxt.hasTelemetrySpeed)
                {
                    continue;
                }

                Vector3 delta = nxt.worldPosition - cur.worldPosition;
                float dist = delta.magnitude;
                float vGeom = dist / dt;
                float vRef = 0.5f * (Mathf.Max(cur.telemetrySpeed, 0.5f) + Mathf.Max(nxt.telemetrySpeed, 0.5f));
                float threshold = Mathf.Max(vRef * 2.75f, vRef + 12f);
                if (vGeom <= threshold || dist < 1e-6f)
                {
                    continue;
                }

                float capDist = Mathf.Max(vRef * dt * 1.12f, 0.05f);
                Vector3 dir = delta / dist;
                Vector3 newWorld = cur.worldPosition + dir * Mathf.Min(dist, capDist);
                nxt.worldPosition = newWorld;
                nxt.rawPosition = newWorld - offset;
            }
        }

        private static void CalculateMovementData(DriverReplayTrack track)
        {
            for (int i = 0; i < track.samples.Count - 1; i++)
            {
                ReplaySample cur = track.samples[i];
                ReplaySample nxt = track.samples[i + 1];
                float dt = nxt.sessionTimeSeconds - cur.sessionTimeSeconds;
                if (dt > MinDtForSpeedCompare)
                {
                    cur.speedMetersPerSecond = (nxt.worldPosition - cur.worldPosition).magnitude / dt;
                    Vector3 dir = nxt.worldPosition - cur.worldPosition;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        cur.headingYawDegrees = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)).eulerAngles.y;
                    }
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