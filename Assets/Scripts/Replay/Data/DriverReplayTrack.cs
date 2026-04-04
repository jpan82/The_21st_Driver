using System;
using System.Collections.Generic;

namespace The21stDriver.Replay.Data
{
    [Serializable]
    public class DriverReplayTrack
    {
        public string driverId;
        public string sourcePath;
        public List<ReplaySample> samples = new List<ReplaySample>();
    }
}
