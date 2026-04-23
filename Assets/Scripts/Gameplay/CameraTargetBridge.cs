using UnityEngine;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Lightweight bridge so the Camera assembly (which references Gameplay) can publish
    /// the currently-viewed car, and Gameplay UI can read it without a reverse dependency.
    /// </summary>
    public static class CameraTargetBridge
    {
        public static Transform CurrentTarget { get; set; }
    }
}
