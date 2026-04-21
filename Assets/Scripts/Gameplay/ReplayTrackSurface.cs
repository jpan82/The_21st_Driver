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

            int idx = FindNearestCenterlineIndex(worldPosition, Vector3.zero);
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
        public bool TryGetAdditionalLateralOffsetBounds(Vector3 worldPosition, float edgeClearanceMeters, out float minOffset, out float maxOffset, Vector3 carForward = default)
        {
            minOffset = -Mathf.Max(0.5f, fallbackHalfWidthMeters);
            maxOffset = Mathf.Max(0.5f, fallbackHalfWidthMeters);

            if (centerline == null || centerline.Count < 2)
            {
                return false;
            }

            int idx = FindNearestCenterlineIndex(worldPosition, carForward);
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

        // How strongly to penalize a candidate segment whose direction disagrees with
        // the car's heading. In metres — a candidate 5 m closer but facing the wrong
        // way costs this many extra "metres" per unit of (1 - dot). A value of ~20 m
        // means an opposite-facing parallel straight (dot ≈ -1, penalty = 40 m) will
        // only win if it is more than 40 m closer, which almost never happens on track.
        private const float DIRECTION_PENALTY_SCALE = 20f;

        /// <summary>
        /// Finds nearest centerline index to world position using a sliding window
        /// around the previous result (O(window) instead of O(N)).
        /// Falls back to a full scan once if the nearest point escapes the window,
        /// which re-anchors the warm-start index for subsequent frames.
        /// When <paramref name="carForward"/> is non-zero, candidates whose segment
        /// direction disagrees with the car heading are penalised, preventing the
        /// search from snapping to a parallel segment on the opposite side of the track.
        /// </summary>
        private int FindNearestCenterlineIndex(Vector3 worldPosition, Vector3 carForward)
        {
            if (centerline == null || centerline.Count == 0)
            {
                return -1;
            }

            bool useDirection = carForward.sqrMagnitude > 0.01f;
            Vector3 carDir = useDirection ? carForward.normalized : Vector3.zero;
            carDir.y = 0f;
            if (carDir.sqrMagnitude > 0.01f) carDir.Normalize(); else useDirection = false;

            int n = centerline.Count;
            int seed = Mathf.Clamp(lastNearestIndex, 0, n - 1);

            int bestIndex = seed;
            float bestScore = ScoreCandidate(seed, worldPosition, carDir, useDirection, n);

            int lo = seed - SEARCH_HALF_WINDOW;
            int hi = seed + SEARCH_HALF_WINDOW;

            for (int i = lo; i <= hi; i++)
            {
                int idx = ((i % n) + n) % n;
                float score = ScoreCandidate(idx, worldPosition, carDir, useDirection, n);
                if (score < bestScore)
                {
                    bestScore = score;
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
                    float score = ScoreCandidate(i, worldPosition, carDir, useDirection, n);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }
            }

            lastNearestIndex = bestIndex;
            return bestIndex;
        }

        /// <summary>
        /// Scoring function for a centerline candidate. Returns distance + optional
        /// directional penalty so segments facing the wrong way are deprioritised.
        /// </summary>
        private float ScoreCandidate(int idx, Vector3 worldPosition, Vector3 carDir, bool useDirection, int n)
        {
            float dist = (centerline[idx] - worldPosition).magnitude;
            if (!useDirection) return dist;

            // Compute the segment forward at this sample.
            int prev = Mathf.Max(0, idx - 1);
            int next = Mathf.Min(n - 1, idx + 1);
            Vector3 segFwd = centerline[next] - centerline[prev];
            segFwd.y = 0f;
            if (segFwd.sqrMagnitude < 1e-6f) return dist;
            segFwd.Normalize();

            // dot in [-1, 1]; facing same direction → dot ≈ 1, penalty ≈ 0.
            // Facing opposite → dot ≈ -1, penalty ≈ 2 * DIRECTION_PENALTY_SCALE.
            float dot = Vector3.Dot(carDir, segFwd);
            float penalty = (1f - dot) * DIRECTION_PENALTY_SCALE;
            return dist + penalty;
        }
    }
}