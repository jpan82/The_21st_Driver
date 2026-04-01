using UnityEngine;

public class TrajectorySampler
{
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
        return CatmullRom(p0, p1, p2, p3, t);
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
        Vector3 direction = CatmullRomTangent(p0, p1, p2, p3, t);
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        Quaternion fallbackRotation = Quaternion.Euler(0f, current.headingYawDegrees, 0f);
        return fallbackRotation * Vector3.forward;
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
            segmentIndex = 0;
            current = track.samples[0];
            next = track.samples[1];
            return true;
        }

        if (sessionTimeSeconds >= EndTime)
        {
            int lastIndex = track.samples.Count - 1;
            segmentIndex = lastIndex - 1;
            current = track.samples[lastIndex - 1];
            next = track.samples[lastIndex];
            t = 1f;
            return true;
        }

        segmentIndex = Mathf.Clamp(lastSegmentIndex, 0, track.samples.Count - 2);
        if (!IsTimeWithinSegment(sessionTimeSeconds, segmentIndex))
        {
            segmentIndex = FindSegmentIndex(sessionTimeSeconds);
        }

        lastSegmentIndex = segmentIndex;
        current = track.samples[segmentIndex];
        next = track.samples[segmentIndex + 1];

        float duration = next.sessionTimeSeconds - current.sessionTimeSeconds;
        if (duration <= Mathf.Epsilon)
        {
            t = 0f;
            return true;
        }

        t = Mathf.Clamp01((sessionTimeSeconds - current.sessionTimeSeconds) / duration);
        return true;
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

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;

        return 0.5f * (
            (-p0 + p2) +
            2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
            3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
        );
    }
}
