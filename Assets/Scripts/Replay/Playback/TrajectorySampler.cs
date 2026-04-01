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
        if (!TryGetInterpolatedSample(sessionTimeSeconds, out ReplaySample current, out ReplaySample next, out float t))
        {
            return Vector3.zero;
        }

        return Vector3.Lerp(current.worldPosition, next.worldPosition, t);
    }

    public Vector3 SampleForward(float sessionTimeSeconds)
    {
        if (!TryGetInterpolatedSample(sessionTimeSeconds, out ReplaySample current, out ReplaySample next, out _))
        {
            return Vector3.forward;
        }

        Vector3 direction = next.worldPosition - current.worldPosition;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        Quaternion fallbackRotation = Quaternion.Euler(0f, current.headingYawDegrees, 0f);
        return fallbackRotation * Vector3.forward;
    }

    private bool TryGetInterpolatedSample(float sessionTimeSeconds, out ReplaySample current, out ReplaySample next, out float t)
    {
        current = null;
        next = null;
        t = 0f;

        if (!IsValid)
        {
            return false;
        }

        if (sessionTimeSeconds <= StartTime)
        {
            current = track.samples[0];
            next = track.samples[1];
            return true;
        }

        if (sessionTimeSeconds >= EndTime)
        {
            int lastIndex = track.samples.Count - 1;
            current = track.samples[lastIndex - 1];
            next = track.samples[lastIndex];
            t = 1f;
            return true;
        }

        int segmentIndex = Mathf.Clamp(lastSegmentIndex, 0, track.samples.Count - 2);
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
}
