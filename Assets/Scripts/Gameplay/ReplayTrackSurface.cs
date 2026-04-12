using System.Collections.Generic;
using UnityEngine;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Runtime helper attached to generated track mesh.
    /// Stores centerline and width profile so NPC logic can clamp lateral offsets.
    /// </summary>
    public class ReplayTrackSurface : MonoBehaviour
    {
        [SerializeField] private List<Vector3> centerline = new List<Vector3>();
        [SerializeField] private List<float> halfRight = new List<float>();
        [SerializeField] private List<float> halfLeft = new List<float>();
        [SerializeField] private float fallbackHalfWidthMeters = 14f;
        private int lastNearestIndex;

        /// <summary>
        /// Injects track centerline and width profile built from CSV/fallback.
        /// </summary>
        public void ConfigureWidths(List<Vector3> centers, List<float> rightHalfWidths, List<float> leftHalfWidths, float fallbackHalfWidth)
        {
            centerline = centers != null ? new List<Vector3>(centers) : new List<Vector3>();
            halfRight = rightHalfWidths != null ? new List<float>(rightHalfWidths) : new List<float>();
            halfLeft = leftHalfWidths != null ? new List<float>(leftHalfWidths) : new List<float>();
            fallbackHalfWidthMeters = Mathf.Max(0.5f, fallbackHalfWidth);
            lastNearestIndex = 0;
        }

        /// <summary>
        /// Returns nearest-sample half widths at world position.
        /// Useful for debug/inspection helpers.
        /// </summary>
        public bool TryGetHalfWidthsAtWorldPosition(Vector3 worldPosition, out float rightWidth, out float leftWidth)
        {
            rightWidth = Mathf.Max(0.5f, fallbackHalfWidthMeters);
            leftWidth = Mathf.Max(0.5f, fallbackHalfWidthMeters);

            if (centerline == null || centerline.Count == 0)
            {
                return false;
            }

            int idx = FindNearestCenterlineIndex(worldPosition);
            if (idx < 0 || idx >= centerline.Count)
            {
                return false;
            }

            if (halfRight != null && idx < halfRight.Count)
            {
                rightWidth = Mathf.Max(0.25f, halfRight[idx]);
            }

            if (halfLeft != null && idx < halfLeft.Count)
            {
                leftWidth = Mathf.Max(0.25f, halfLeft[idx]);
            }

            return true;
        }

        /// <summary>
        /// Returns allowable additional lateral offset range relative to <paramref name="worldPosition"/>.
        /// Negative = left, positive = right in local track frame at nearest centerline sample.
        /// Caller can clamp desired offset into [minOffset, maxOffset] to stay inside track with clearance.
        /// </summary>
        public bool TryGetAdditionalLateralOffsetBounds(Vector3 worldPosition, float edgeClearanceMeters, out float minOffset, out float maxOffset)
        {
            minOffset = -Mathf.Max(0.5f, fallbackHalfWidthMeters);
            maxOffset = Mathf.Max(0.5f, fallbackHalfWidthMeters);

            if (centerline == null || centerline.Count < 2)
            {
                return false;
            }

            int idx = FindNearestCenterlineIndex(worldPosition);
            if (idx < 0 || idx >= centerline.Count)
            {
                return false;
            }

            // Build local track frame at nearest sample.
            int prevIdx = Mathf.Max(0, idx - 1);
            int nextIdx = Mathf.Min(centerline.Count - 1, idx + 1);
            Vector3 fwd = centerline[nextIdx] - centerline[prevIdx];
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            // Convert current world point to signed lateral offset from centerline.
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 toWorld = worldPosition - centerline[idx];
            float signedFromCenter = Vector3.Dot(toWorld, right);

            float rightWidth = Mathf.Max(0.25f, fallbackHalfWidthMeters);
            float leftWidth = Mathf.Max(0.25f, fallbackHalfWidthMeters);
            if (halfRight != null && idx < halfRight.Count)
            {
                rightWidth = Mathf.Max(0.25f, halfRight[idx]);
            }
            if (halfLeft != null && idx < halfLeft.Count)
            {
                leftWidth = Mathf.Max(0.25f, halfLeft[idx]);
            }

            // Compute remaining left/right room after safety clearance.
            float clearance = Mathf.Max(0f, edgeClearanceMeters);
            maxOffset = (rightWidth - clearance) - signedFromCenter;
            minOffset = (-leftWidth + clearance) - signedFromCenter;
            return true;
        }

        // Half-size of the sliding window used by FindNearestCenterlineIndex.
        // 40 samples covers ~80 m at typical F1 CSV density (≈1 m/sample) — enough
        // headroom even at 300 km/h between inference ticks.
        private const int SEARCH_HALF_WINDOW = 40;

        /// <summary>
        /// Finds nearest centerline index to world position using a sliding window
        /// around the previous result (O(window) instead of O(N)).
        /// Falls back to a full scan once if the nearest point escapes the window,
        /// which re-anchors the warm-start index for subsequent frames.
        /// </summary>
        private int FindNearestCenterlineIndex(Vector3 worldPosition)
        {
            if (centerline == null || centerline.Count == 0)
            {
                return -1;
            }

            int n = centerline.Count;
            int seed = Mathf.Clamp(lastNearestIndex, 0, n - 1);

            int bestIndex = seed;
            float bestSqr = (centerline[seed] - worldPosition).sqrMagnitude;

            int lo = seed - SEARCH_HALF_WINDOW;
            int hi = seed + SEARCH_HALF_WINDOW;

            for (int i = lo; i <= hi; i++)
            {
                // Wrap around for closed-loop tracks.
                int idx = ((i % n) + n) % n;
                float sqr = (centerline[idx] - worldPosition).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestIndex = idx;
                }
            }

            // If the winner sits at a window boundary the car may have jumped
            // outside our window (e.g. first frame, teleport). Do one full scan
            // to re-anchor, then resume windowed search next frame.
            bool atBoundary = bestIndex == ((lo % n + n) % n) || bestIndex == ((hi % n + n) % n);
            if (atBoundary)
            {
                for (int i = 0; i < n; i++)
                {
                    float sqr = (centerline[i] - worldPosition).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        bestIndex = i;
                    }
                }
            }

            lastNearestIndex = bestIndex;
            return bestIndex;
        }
    }
}