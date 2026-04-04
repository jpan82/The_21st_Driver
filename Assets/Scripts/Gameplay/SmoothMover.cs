using System;
using UnityEngine;
using The21stDriver.Replay.Data;
using The21stDriver.Replay.Playback;

namespace The21stDriver.Gameplay
{
    public class SmoothMover : MonoBehaviour
    {
        private TrajectorySampler sampler;
        private Race_Controller ctrl;
        private float verticalVelocity;

        public void Init(DriverReplayTrack track, Race_Controller controller)
        {
            sampler = new TrajectorySampler(track);
            ctrl = controller;

            if (sampler != null && sampler.IsValid)
            {
                Vector3 startPos = sampler.SamplePosition(sampler.StartTime);
                if (ctrl != null && ctrl.useGroundSpring)
                {
                    startPos.y += ctrl.spawnHeightOffset;
                }

                transform.position = startPos;
            }
        }

        void Update()
        {
            if (sampler == null || !sampler.IsValid) return;

            float duration = sampler.EndTime - sampler.StartTime;
            float t = sampler.StartTime;
            float playbackTime = ctrl != null ? ctrl.GlobalTime : 0f;
            if (duration > Mathf.Epsilon)
            {
                t += playbackTime % duration;
            }

            Vector3 currentPos = sampler.SamplePosition(t);
            if (ctrl != null && ctrl.useGroundSpring && TryGetGroundY(currentPos, out float groundY))
            {
                float targetY = groundY + ctrl.carYOffset;
                float dt = Time.deltaTime;
                float displacement = targetY - transform.position.y;

                verticalVelocity += displacement * ctrl.groundSpringStrength * dt;
                verticalVelocity -= verticalVelocity * ctrl.groundSpringDamping * dt;

                float newY = transform.position.y + verticalVelocity * dt;
                if (Mathf.Abs(displacement) < ctrl.groundSnapEpsilon && Mathf.Abs(verticalVelocity) < ctrl.groundSnapVelocity)
                {
                    newY = targetY;
                    verticalVelocity = 0f;
                }

                currentPos.y = newY;
            }

            transform.position = currentPos;

            float lookT = Mathf.Min(t + 0.05f, sampler.EndTime);
            Vector3 targetPos = sampler.SamplePosition(lookT);
            // Horizontal only: replay Y can differ from spring-set Y and would skew yaw.
            Vector3 direction = new Vector3(targetPos.x - currentPos.x, 0f, targetPos.z - currentPos.z).normalized;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction, Vector3.up);
                Quaternion targetRotation = Quaternion.Euler(ctrl.fixedXRotation, lookRot.eulerAngles.y, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * ctrl.rotationSmoothness);
            }
        }

        private bool TryGetGroundY(Vector3 sourcePosition, out float groundY)
        {
            groundY = 0f;
            if (ctrl == null)
            {
                return false;
            }

            Vector3 origin = sourcePosition + Vector3.up * ctrl.groundRaycastStartHeight;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                ctrl.groundRaycastMaxDistance + ctrl.groundRaycastStartHeight,
                ~0,
                QueryTriggerInteraction.Ignore);

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hitCollider.GetComponentInParent<ReplayTrackSurface>() == null)
                {
                    continue;
                }

                groundY = hits[i].point.y;
                return true;
            }

            return false;
        }
    }
}
