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

        /// <summary>
        /// Narrow 7-column Unity reference CSV (progress…x_ref,y_ref): fixed column indices, index-based time.
        /// </summary>
        public static DriverReplayTrack LoadMotionDumpTrack(
            string filePath,
            float sampleIntervalSeconds,
            float carYOffset,
            Vector3 trackGlobalOffset)
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
                string[] cols = lines[i].Split(',');
                if (cols.Length < 7)
                {
                    continue;
                }

                if (!TryParseFloat(cols[5], out float x) || !TryParseFloat(cols[6], out float z))
                {
                    continue;
                }

                float timestamp = (i - 1) * sampleIntervalSeconds;
                Vector3 raw = new Vector3(x, carYOffset, z);
                track.samples.Add(new ReplaySample
                {
                    sessionTimeSeconds = timestamp,
                    rawPosition = raw,
                    worldPosition = raw + trackGlobalOffset
                });
            }

            PopulateDerivedData(track);
            return track;
        }

        /// <summary>
        /// Full-race export from Frenet / Colab pipeline: header row with SessionTime (pandas timedelta string)
        /// and x_ref, y_ref. Time is normalized to start at 0 from the first valid row.
        /// </summary>
        public static DriverReplayTrack LoadFrenetRaceReferenceCsv(
            string filePath,
            float carYOffset,
            Vector3 trackGlobalOffset)
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

            Dictionary<string, int> col = BuildHeaderColumnIndex(lines[0]);
            if (!col.ContainsKey("SessionTime") || !col.ContainsKey("x_ref") || !col.ContainsKey("y_ref"))
            {
                Debug.LogError("Race CSV missing SessionTime, x_ref, or y_ref columns: " + filePath);
                return track;
            }

            int iSession = col["SessionTime"];
            int iXref = col["x_ref"];
            int iYref = col["y_ref"];
            int minLen = Mathf.Max(iSession, iXref, iYref) + 1;

            float? t0 = null;

            for (int r = 1; r < lines.Length; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r]))
                {
                    continue;
                }

                string[] fields = lines[r].Split(',');
                if (fields.Length < minLen)
                {
                    continue;
                }

                string ts = fields[iSession].Trim();
                if (!TryParsePandasTimedeltaTotalSeconds(ts, out float tAbs))
                {
                    continue;
                }

                if (!TryParseFloat(fields[iXref].Trim(), out float xr) ||
                    !TryParseFloat(fields[iYref].Trim(), out float yr))
                {
                    continue;
                }

                if (!t0.HasValue)
                {
                    t0 = tAbs;
                }

                float tRel = tAbs - t0.Value;
                Vector3 raw = new Vector3(xr, carYOffset, yr);
                track.samples.Add(new ReplaySample
                {
                    sessionTimeSeconds = tRel,
                    rawPosition = raw,
                    worldPosition = raw + trackGlobalOffset
                });
            }

            PopulateDerivedData(track);
            return track;
        }

        /// <summary>
        /// Chooses loader by header: SessionTime + x_ref = full race; otherwise narrow motion-dump.
        /// </summary>
        public static DriverReplayTrack LoadDriverCsvForRaceController(
            string filePath,
            float sampleIntervalSeconds,
            float carYOffset,
            Vector3 trackGlobalOffset)
        {
            DriverReplayTrack empty = new DriverReplayTrack
            {
                driverId = Path.GetFileNameWithoutExtension(filePath),
                sourcePath = filePath
            };

            if (!File.Exists(filePath))
            {
                Debug.LogError("Cannot find replay file: " + filePath);
                return empty;
            }

            string header;
            using (StreamReader sr = new StreamReader(filePath))
            {
                header = sr.ReadLine();
            }

            if (header != null &&
                header.IndexOf("SessionTime", StringComparison.OrdinalIgnoreCase) >= 0 &&
                header.IndexOf("x_ref", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LoadFrenetRaceReferenceCsv(filePath, carYOffset, trackGlobalOffset);
            }

            return LoadMotionDumpTrack(filePath, sampleIntervalSeconds, carYOffset, trackGlobalOffset);
        }

        private static Dictionary<string, int> BuildHeaderColumnIndex(string headerLine)
        {
            Dictionary<string, int> d = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            string[] parts = headerLine.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string key = parts[i].Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                if (!d.ContainsKey(key))
                {
                    d[key] = i;
                }
            }

            return d;
        }

        /// <summary>Parses strings like "0 days 00:56:09.187000" (pandas timedelta text export).</summary>
        private static bool TryParsePandasTimedeltaTotalSeconds(string value, out float totalSeconds)
        {
            totalSeconds = 0f;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();
            Match m = PandasTimedeltaRegex.Match(value);
            if (!m.Success)
            {
                return TryParseFloat(value, out totalSeconds);
            }

            float sign = m.Groups["sign"].Value == "-" ? -1f : 1f;
            float days = m.Groups["d"].Success
                ? float.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture)
                : 0f;
            int hh = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
            int mm = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
            float ss = float.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture);

            totalSeconds = sign * (days * 86400f + hh * 3600f + mm * 60f + ss);
            return true;
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
}
