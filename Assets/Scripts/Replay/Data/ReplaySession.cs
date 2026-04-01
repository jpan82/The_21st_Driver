using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ReplaySession
{
    public string sessionId;
    public string sourceFolderPath;
    public Vector3 globalOffset;
    public List<DriverReplayTrack> tracks = new List<DriverReplayTrack>();
}
