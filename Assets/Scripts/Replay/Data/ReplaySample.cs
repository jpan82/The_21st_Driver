using System;
using UnityEngine;

namespace The21stDriver.Replay.Data
{
    [Serializable]
    public class ReplaySample
    {
        public float sessionTimeSeconds;
        public Vector3 rawPosition;
        public Vector3 worldPosition;
        public float speedMetersPerSecond;
        public float headingYawDegrees;
        /// <summary>CSV Speed column when present (m/s in current dumps).</summary>
        public float telemetrySpeed;
        public bool hasTelemetrySpeed;
    }
}
