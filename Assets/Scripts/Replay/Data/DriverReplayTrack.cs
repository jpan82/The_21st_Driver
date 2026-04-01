using System;
using System.Collections.Generic;

[Serializable]
public class DriverReplayTrack
{
    public string driverId;
    public string sourcePath;
    public List<ReplaySample> samples = new List<ReplaySample>();
}
