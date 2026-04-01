using UnityEngine;

public class F1_Playback_Actor : MonoBehaviour
{
    private TrajectorySampler sampler;
    private float playbackTime;
    private bool isReady = false;
    private float speedFactor = 1.0f; // Internal storage for speed

    // FIXED: Now takes 2 arguments to match your Race_Controller call
    public void Initialize(DriverReplayTrack data, float speed)
    {
        sampler = new TrajectorySampler(data);
        speedFactor = speed;
        
        if (sampler.IsValid)
        {
            playbackTime = sampler.StartTime;
            isReady = true;
        }
    }

    void Update()
    {
        if (!isReady) return;

        // Advance time using the speedFactor passed from the Manager
        playbackTime += Time.deltaTime * speedFactor;

        // Loop playback
        if (playbackTime > sampler.EndTime)
        {
            playbackTime = sampler.StartTime;
        }

        // Apply Position and Rotation from the Sampler
        transform.position = sampler.SamplePosition(playbackTime);

        Vector3 forward = sampler.SampleForward(playbackTime);
        if (forward != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}