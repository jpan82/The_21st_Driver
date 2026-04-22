using UnityEngine;
using System.Collections.Generic;
using The21stDriver.Replay.Data;

namespace The21stDriver.Replay.Playback
{
    public class TrajectorySampler
    {
        private const float CentripetalAlpha = 0.5f;
        private const float MinParameterStep = 0.0001f;
        private const float MinSegmentDuration = 1e-5f;

        private readonly DriverReplayTrack track;
        private int lastSegmentIndex;

        public TrajectorySampler(DriverReplayTrack sourceTrack)
        {
            track = sourceTrack;
            lastSegmentIndex = 0;
        }

        public bool IsValid => track != null && track.samples != null && track.samples.Count >= 2;

        public float StartTime => IsValid ? track.samples[0].sessionTimeSeconds : 0f;

        public float EndTime => IsValid ? track.samples[track.samples.Count - 1].sessionTimeSeconds : 0f;

        public Vector3 SamplePosition(float sessionTimeSeconds)
        {
            if (!TryGetInterpolatedSample(sessionTimeSeconds, out int segmentIndex, out ReplaySample current, out ReplaySample next, out float t))
            {
                return Vector3.zero;
            }

            Vector3 p0 = GetSamplePosition(segmentIndex - 1);
            Vector3 p1 = current.worldPosition;
            Vector3 p2 = next.worldPosition;
            Vector3 p3 = GetSamplePosition(segmentIndex + 2);
            return CentripetalCatmullRom(p0, p1, p2, p3, t);
        }

        public Vector3 SampleForward(float sessionTimeSeconds)
        {
            if (!TryGetInterpolatedSample(sessionTimeSeconds, out int segmentIndex, out ReplaySample current, out ReplaySample next, out float t))
            {
                return Vector3.forward;
            }

            Vector3 p0 = GetSamplePosition(segmentIndex - 1);
            Vector3 p1 = current.worldPosition;
            Vector3 p2 = next.worldPosition;
            Vector3 p3 = GetSamplePosition(segmentIndex + 2);
            Vector3 direction = CentripetalCatmullRomTangent(p0, p1, p2, p3, t);
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            Quaternion fallbackRotation = Quaternion.Euler(0f, current.headingYawDegrees, 0f);
            return fallbackRotation * Vector3.forward;
        }

        public List<Vector3> BuildSampledPath(float timeStepSeconds)
        {
            List<Vector3> points = new List<Vector3>();
            if (!IsValid)
            {
                return points;
            }

            float safeTimeStep = Mathf.Max(0.01f, timeStepSeconds);
            float currentTime = StartTime;

            while (currentTime < EndTime)
            {
                points.Add(SamplePosition(currentTime));
                currentTime += safeTimeStep;
            }

            points.Add(SamplePosition(EndTime));
            return points;
        }

        private bool TryGetInterpolatedSample(float sessionTimeSeconds, out int segmentIndex, out ReplaySample current, out ReplaySample next, out float t)
        {
        segmentIndex = 0;
        current = null;
        next = null;
        t = 0f;

        if (!IsValid)
        {
            return false;
        }

        if (sessionTimeSeconds <= StartTime)
        {
            segmentIndex = AdvanceToPositiveDurationSegment(0);
            current = track.samples[segmentIndex];
            next = track.samples[segmentIndex + 1];
            return true;
        }

        if (sessionTimeSeconds >= EndTime)
        {
            int lastIndex = track.samples.Count - 1;
            segmentIndex = FindLastPositiveDurationSegmentStart(lastIndex);
            current = track.samples[segmentIndex];
            next = track.samples[segmentIndex + 1];
            t = 1f;
            return true;
        }

        segmentIndex = Mathf.Clamp(lastSegmentIndex, 0, track.samples.Count - 2);
        if (!IsTimeWithinSegment(sessionTimeSeconds, segmentIndex))
        {
            segmentIndex = FindSegmentIndex(sessionTimeSeconds);
        }

        segmentIndex = AdvanceToPositiveDurationSegment(segmentIndex);
        lastSegmentIndex = segmentIndex;
        current = track.samples[segmentIndex];
        next = track.samples[segmentIndex + 1];

        float duration = next.sessionTimeSeconds - current.sessionTimeSeconds;
        if (duration <= MinSegmentDuration)
        {
            t = 0f;
            return true;
        }

        t = Mathf.Clamp01((sessionTimeSeconds - current.sessionTimeSeconds) / duration);
        return true;
        }

        private int AdvanceToPositiveDurationSegment(int startIndex)
        {
            int i = Mathf.Clamp(startIndex, 0, track.samples.Count - 2);
            while (i < track.samples.Count - 2)
            {
                float duration = track.samples[i + 1].sessionTimeSeconds - track.samples[i].sessionTimeSeconds;
                if (duration > MinSegmentDuration)
                {
                    return i;
                }

                i++;
            }

            return Mathf.Max(0, track.samples.Count - 2);
        }

        private int FindLastPositiveDurationSegmentStart(int lastSampleIndex)
        {
            int i = Mathf.Clamp(lastSampleIndex - 1, 0, track.samples.Count - 2);
            while (i > 0)
            {
                float duration = track.samples[i + 1].sessionTimeSeconds - track.samples[i].sessionTimeSeconds;
                if (duration > MinSegmentDuration)
                {
                    return i;
                }

                i--;
            }

            return 0;
        }

        private bool IsTimeWithinSegment(float sessionTimeSeconds, int segmentIndex)
        {
            ReplaySample current = track.samples[segmentIndex];
            ReplaySample next = track.samples[segmentIndex + 1];
            return sessionTimeSeconds >= current.sessionTimeSeconds && sessionTimeSeconds <= next.sessionTimeSeconds;
        }

        private int FindSegmentIndex(float sessionTimeSeconds)
        {
        int low = 0;
        int high = track.samples.Count - 2;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            ReplaySample current = track.samples[mid];
            ReplaySample next = track.samples[mid + 1];

            if (sessionTimeSeconds < current.sessionTimeSeconds)
            {
                high = mid - 1;
            }
            else if (sessionTimeSeconds > next.sessionTimeSeconds)
            {
                low = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return Mathf.Clamp(low, 0, track.samples.Count - 2);
        }

        private Vector3 GetSamplePosition(int index)
        {
            int clampedIndex = Mathf.Clamp(index, 0, track.samples.Count - 1);
            return track.samples[clampedIndex].worldPosition;
        }

        private static Vector3 CentripetalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
        GetCurveParameters(p0, p1, p2, p3, out float t0, out float t1, out float t2, out float t3);

        float curveTime = Mathf.Lerp(t1, t2, t);
        Vector3 a1 = InterpolateSafe(p0, p1, t0, t1, curveTime);
        Vector3 a2 = InterpolateSafe(p1, p2, t1, t2, curveTime);
        Vector3 a3 = InterpolateSafe(p2, p3, t2, t3, curveTime);

        Vector3 b1 = InterpolateSafe(a1, a2, t0, t2, curveTime);
        Vector3 b2 = InterpolateSafe(a2, a3, t1, t3, curveTime);

        return InterpolateSafe(b1, b2, t1, t2, curveTime);
        }

        private static Vector3 CentripetalCatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
        float sampleBefore = Mathf.Clamp01(t - 0.01f);
        float sampleAfter = Mathf.Clamp01(t + 0.01f);
        Vector3 before = CentripetalCatmullRom(p0, p1, p2, p3, sampleBefore);
        Vector3 after = CentripetalCatmullRom(p0, p1, p2, p3, sampleAfter);
        return after - before;
        }

        private static void GetCurveParameters(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, out float t0, out float t1, out float t2, out float t3)
        {
        t0 = 0f;
        t1 = GetNextParameter(t0, p0, p1);
        t2 = GetNextParameter(t1, p1, p2);
        t3 = GetNextParameter(t2, p2, p3);
        }

        private static float GetNextParameter(float currentParameter, Vector3 from, Vector3 to)
        {
        float distance = Vector3.Distance(from, to);
        float step = Mathf.Pow(distance, CentripetalAlpha);
        return currentParameter + Mathf.Max(step, MinParameterStep);
        }

        private static Vector3 InterpolateSafe(Vector3 a, Vector3 b, float ta, float tb, float t)
        {
        float denominator = tb - ta;
        if (denominator <= Mathf.Epsilon)
        {
            return b;
        }

        float lerpT = Mathf.Clamp01((t - ta) / denominator);
        return Vector3.Lerp(a, b, lerpT);
        }
    }
}
