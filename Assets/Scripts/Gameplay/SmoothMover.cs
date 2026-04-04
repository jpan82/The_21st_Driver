using UnityEngine;
using The21stDriver.Replay.Data;
using The21stDriver.Replay.Playback;

namespace The21stDriver.Gameplay
{
    public class SmoothMover : MonoBehaviour
    {
        private TrajectorySampler sampler;
        private Race_Controller ctrl;

        public void Init(DriverReplayTrack track, Race_Controller controller)
        {
            sampler = new TrajectorySampler(track);
            ctrl = controller;
        }

        void Update()
        {
            if (sampler == null || !sampler.IsValid) return;

            float duration = sampler.EndTime - sampler.StartTime;
            float t = sampler.StartTime + (ctrl.GlobalTime % duration);

            Vector3 currentPos = sampler.SamplePosition(t);
            transform.position = currentPos;

            float lookT = Mathf.Min(t + 0.05f, sampler.EndTime);
            Vector3 targetPos = sampler.SamplePosition(lookT);
            Vector3 direction = (targetPos - currentPos).normalized;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction, Vector3.up);
                Quaternion targetRotation = Quaternion.Euler(ctrl.fixedXRotation, lookRot.eulerAngles.y, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * ctrl.rotationSmoothness);
            }
        }
    }
}
