using System;
using System.Collections.Generic;
using UnityEngine;

namespace The21stDriver.Replay.Data
{
    [Serializable]
    public class ReplaySession
    {
        public string sessionId;
        public string sourceFolderPath;
        public Vector3 globalOffset;
        public List<DriverReplayTrack> tracks = new List<DriverReplayTrack>();
    }
}
