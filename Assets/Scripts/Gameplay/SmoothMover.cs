using System;
using UnityEngine;
using The21stDriver.Replay.Data;
using The21stDriver.Replay.Playback;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Replays historical trajectory and layers strategy-driven lateral motion on top.
    /// AI decides intent, state machine stabilizes behavior, and track widths clamp safety bounds.
    /// </summary>
    public class SmoothMover : MonoBehaviour
    {
        private TrajectorySampler sampler;
        private Race_Controller ctrl;
        private float verticalVelocity;
        private float rotationDelayTimer = 0f;
        private const float ROTATION_DELAY = 2f;
        
        private Model_Script aiModel;
        private Vector3 lastPosition;
        private float currentSpeed;
        private int lastAction = -1;
        private float lateralOffset;
        private bool strategyRangeLatched;
        private int heldAction = -1;
        private float heldActionUntil;
        private float nextInferenceTime;
        private ReplayTrackSurface replayTrackSurface;
        private float lateralVelocity;
        private float blendedTargetLateral;
        private readonly EvadeStateMachine evadeStateMachine = new EvadeStateMachine();

        [Header("Strategy Lateral Offset (Historical Trajectory)")]
        [Tooltip("Enter strategy influence radius (meters). Inside this radius, lateral offset follows model actions.")]
        public float strategyProximityRadius = 28f;
        [Tooltip("Exit strategy influence radius (meters). Recommended to be larger than enter radius to reduce boundary jitter.")]
        public float strategyProximityExitRadius = 34f;
        [Tooltip("Maximum lateral offset (meters) for avoid actions, along track tangent normal.")]
        public float strategyMaxLateralOffset = 2.2f;
        [Tooltip("Maximum lateral offset approach speed (m/s) while inside player influence range.")]
        public float strategyLateralApproachSpeed = 5f;
        [Tooltip("Maximum return speed to zero offset (m/s) after leaving player influence range; usually lower than approach speed for smoother feel.")]
        public float strategyLateralReturnSpeed = 3.2f;
        [Tooltip("Model inference interval (seconds) while in strategy range. Reduces per-frame inference cost.")]
        public float strategyInferenceInterval = 0.05f;
        [Tooltip("Minimum action hold duration (seconds). Reduces zig-zag caused by action jitter.")]
        public float strategyActionHoldSeconds = 0.25f;
        [Range(0.2f, 1f)]
        [Tooltip("Lateral amplitude scale for block_left / block_right relative to avoid actions.")]
        public float strategyBlockOffsetScale = 0.55f;
        [Tooltip("Safety clearance from track edges (meters), used for dynamic lateral-offset clamping.")]
        public float strategyTrackEdgeClearance = 0.8f;
        [Tooltip("How fast target lateral intent can change (m/s). Higher = more responsive action switching.")]
        public float strategyTargetChangeSpeed = 9f;
        [Tooltip("Smoothing time while actively reacting in strategy range (seconds). Lower = faster response.")]
        public float strategyLateralSmoothTimeInRange = 0.1f;
        [Tooltip("Smoothing time while returning to racing line (seconds). Slightly larger feels steadier.")]
        public float strategyLateralSmoothTimeReturn = 0.18f;
        [Tooltip("Maximum lateral movement speed (m/s) used by SmoothDamp.")]
        public float strategyLateralMaxSpeed = 12f;
        [Range(1, 6)]
        [Tooltip("Consecutive inferred side frames required before switching to an evade side.")]
        public int strategySideConfirmFrames = 2;
        [Tooltip("Minimum commit time (seconds) to keep an evade side before considering return/switch.")]
        public float strategyEvadeCommitSeconds = 0.45f;
        [Tooltip("Offset magnitude threshold (meters) considered as back on racing line.")]
        public float strategyReturnDoneThreshold = 0.15f;

        private readonly float[] defaultFeatures = new float[16] {
            -2.12f, 0.0f, -2.75f, 53.38f, 200.06f, 76.87f, 0.0f, 
            -0.50f, 50.46f, 0.0f, 0.0f, 0.0f, -1.0f, 6.22f, 5.11f, 11.427f
        };

        public void Init(DriverReplayTrack track, Race_Controller controller)
        {
            sampler = new TrajectorySampler(track);
            ctrl = controller;
        }

        void Start()
        {
            aiModel = GetComponentInChildren<Model_Script>();
            if (aiModel == null)
            {
                Debug.LogWarning($"[Model] {gameObject.name} could not find Model_Script; strategy AI will be disabled.");
            }
            replayTrackSurface = FindObjectOfType<ReplayTrackSurface>();
            evadeStateMachine.Reset();
            lastPosition = transform.position;
        }

        void Update()
        {
            if (sampler == null || !sampler.IsValid) return;
            
            // 1) Sample base replay position and local track frame.
            float playbackTime = ctrl != null ? ctrl.GlobalTime : 0f;
            if (playbackTime > 0f)
            {
                rotationDelayTimer += Time.deltaTime;
            }

            float duration = sampler.EndTime - sampler.StartTime;
            float t = sampler.StartTime;
            if (duration > Mathf.Epsilon)
            {
                t += playbackTime % duration;
            }

            Vector3 basePos = sampler.SamplePosition(t);
            float lookT = Mathf.Min(t + 0.05f, sampler.EndTime);
            Vector3 lookPos = sampler.SamplePosition(lookT);
            Vector3 flatFwd = new Vector3(lookPos.x - basePos.x, 0f, lookPos.z - basePos.z);
            if (flatFwd.sqrMagnitude < 1e-6f)
            {
                flatFwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
            }
            flatFwd.Normalize();
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatFwd);

            // 2) Resolve player target for AI features.
            if (aiModel != null && aiModel.playerCar == null)
            {
                PlayerCarController spawnedPlayer = FindObjectOfType<PlayerCarController>();
                if (spawnedPlayer != null)
                {
                    aiModel.playerCar = spawnedPlayer.transform;
                    Debug.Log($"{gameObject.name} successfully locked onto the player target.");
                }
            }

            Transform playerTf = aiModel != null ? aiModel.playerCar : null;
            Vector3 toPlayer = playerTf != null ? playerTf.position - basePos : Vector3.zero;
            toPlayer.y = 0f;

            // 3) Apply enter/exit hysteresis around strategy range.
            bool wasInRange = strategyRangeLatched;
            if (playerTf == null)
            {
                strategyRangeLatched = false;
            }
            else
            {
                float sqrDist = toPlayer.sqrMagnitude;
                float enterSqr = strategyProximityRadius * strategyProximityRadius;
                float exitSqr = strategyProximityExitRadius * strategyProximityExitRadius;
                if (!strategyRangeLatched && sqrDist <= enterSqr)
                {
                    strategyRangeLatched = true;
                }
                else if (strategyRangeLatched && sqrDist >= exitSqr)
                {
                    strategyRangeLatched = false;
                }
            }

            if (!wasInRange && strategyRangeLatched)
            {
                // Run inference immediately on entering range; do not wait for interval.
                nextInferenceTime = 0f;
            }
            if (wasInRange && !strategyRangeLatched)
            {
                heldAction = -1;
                heldActionUntil = 0f;
                blendedTargetLateral = 0f;
            }

            // 4) Query AI at a controlled rate and hold short-lived action output.
            int action = -1;
            // Infer only inside strategy range and hold actions briefly to avoid per-frame jitter.
            if (aiModel != null && playerTf != null && Time.deltaTime > 0)
            {
                currentSpeed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
                if (strategyRangeLatched)
                {
                    if (Time.time >= nextInferenceTime)
                    {
                        float[] features = BuildAIFeatures();
                        int inferred = aiModel.GetAIAction(features);
                        nextInferenceTime = Time.time + Mathf.Max(0.01f, strategyInferenceInterval);

                        if (inferred != lastAction)
                        {
                            LogAIAction(inferred);
                            lastAction = inferred;
                        }

                        heldAction = inferred;
                        heldActionUntil = Time.time + Mathf.Max(0f, strategyActionHoldSeconds);
                    }

                    if (heldAction >= 0 && Time.time <= heldActionUntil)
                    {
                        action = heldAction;
                    }
                }
            }

            // 5) Convert AI action to side intent and let state machine produce stable target offset.
            int desiredSide = StrategyActionToSide(action);
            var smConfig = new EvadeStateMachine.Config
            {
                sideConfirmFrames = strategySideConfirmFrames,
                evadeCommitSeconds = strategyEvadeCommitSeconds,
                returnDoneThreshold = strategyReturnDoneThreshold
            };
            float targetLateral = evadeStateMachine.Step(
                strategyRangeLatched,
                desiredSide,
                action,
                lateralOffset,
                Time.time,
                smConfig,
                StrategyActionToLateralTarget);

            // 6) Blend target offset and apply damping for smooth motion.
            float targetChangeSpeed = Mathf.Max(0.01f, strategyTargetChangeSpeed);
            blendedTargetLateral = Mathf.MoveTowards(blendedTargetLateral, targetLateral, targetChangeSpeed * Time.deltaTime);

            float smoothTime = strategyRangeLatched
                ? Mathf.Max(0.01f, strategyLateralSmoothTimeInRange)
                : Mathf.Max(0.01f, strategyLateralSmoothTimeReturn);
            lateralOffset = Mathf.SmoothDamp(
                lateralOffset,
                blendedTargetLateral,
                ref lateralVelocity,
                smoothTime,
                Mathf.Max(0.01f, strategyLateralMaxSpeed),
                Time.deltaTime);
            // 7) Clamp by global caps and dynamic track-edge bounds.
            float minAllowed = -strategyMaxLateralOffset;
            float maxAllowed = strategyMaxLateralOffset;
            if (replayTrackSurface != null &&
                replayTrackSurface.TryGetAdditionalLateralOffsetBounds(basePos, strategyTrackEdgeClearance, out float dynMin, out float dynMax))
            {
                minAllowed = Mathf.Max(minAllowed, dynMin);
                maxAllowed = Mathf.Min(maxAllowed, dynMax);
                if (minAllowed > maxAllowed)
                {
                    float mid = 0.5f * (minAllowed + maxAllowed);
                    minAllowed = mid;
                    maxAllowed = mid;
                }
            }
            lateralOffset = Mathf.Clamp(lateralOffset, minAllowed, maxAllowed);
            if (Mathf.Approximately(lateralOffset, minAllowed) || Mathf.Approximately(lateralOffset, maxAllowed))
            {
                lateralVelocity = 0f;
            }

            // 8) Write final position, then preserve replay-facing orientation.
            Vector3 currentPos = basePos + flatRight * lateralOffset;
            lastPosition = transform.position;

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

            Vector3 direction = new Vector3(lookPos.x - basePos.x, 0f, lookPos.z - basePos.z).normalized;

            if (direction.sqrMagnitude > 0.001f && rotationDelayTimer > ROTATION_DELAY)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction, Vector3.up);
                Quaternion targetRotation = Quaternion.Euler(ctrl.fixedXRotation, lookRot.eulerAngles.y, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * ctrl.rotationSmoothness);
            }
        }

        // Model input features.
        private float[] BuildAIFeatures()
        {
            float[] features = (float[])defaultFeatures.Clone();
            Transform player = aiModel != null ? aiModel.playerCar : null;
            if (player == null)
            {
                return features;
            }

            Vector3 relativePos = player.position - transform.position;
            float approxDeltaS = Vector3.Dot(relativePos, transform.forward); 
            float approxDeltaD = Vector3.Dot(relativePos, transform.right);   

            features[0] = approxDeltaS;                             
            features[1] = approxDeltaD;                             
            features[3] = relativePos.magnitude;                    
            features[4] = currentSpeed * 3.6f;                      
            features[7] = lateralOffset;

            return features;
        }
        /// <summary>
        /// Positive offset = right side of the track plane (flatRight); avoid_left uses negative offset.
        /// </summary>
        private float StrategyActionToLateralTarget(int action)
        {
            float cap = strategyMaxLateralOffset;
            float blk = cap * strategyBlockOffsetScale;
            switch (action)
            {
                case 0: return -cap;  // avoid_left
                case 1: return cap;   // avoid_right
                case 2: return -blk;  // block_left
                case 3: return blk;   // block_right
                default: return 0f;   // keep
            }
        }

        private int StrategyActionToSide(int action)
        {
            switch (action)
            {
                case 0:
                case 2:
                    return -1;
                case 1:
                case 3:
                    return 1;
                default:
                    return 0;
            }
        }

        void OnValidate()
        {
            strategyProximityRadius = Mathf.Max(0.1f, strategyProximityRadius);
            strategyProximityExitRadius = Mathf.Max(strategyProximityRadius + 0.1f, strategyProximityExitRadius);
            strategyMaxLateralOffset = Mathf.Max(0f, strategyMaxLateralOffset);
            strategyLateralApproachSpeed = Mathf.Max(0.01f, strategyLateralApproachSpeed);
            strategyLateralReturnSpeed = Mathf.Max(0.01f, strategyLateralReturnSpeed);
            strategyInferenceInterval = Mathf.Max(0.01f, strategyInferenceInterval);
            strategyActionHoldSeconds = Mathf.Max(0f, strategyActionHoldSeconds);
            strategyTrackEdgeClearance = Mathf.Max(0f, strategyTrackEdgeClearance);
            strategyTargetChangeSpeed = Mathf.Max(0.01f, strategyTargetChangeSpeed);
            strategyLateralSmoothTimeInRange = Mathf.Max(0.01f, strategyLateralSmoothTimeInRange);
            strategyLateralSmoothTimeReturn = Mathf.Max(0.01f, strategyLateralSmoothTimeReturn);
            strategyLateralMaxSpeed = Mathf.Max(0.01f, strategyLateralMaxSpeed);
            strategySideConfirmFrames = Mathf.Max(1, strategySideConfirmFrames);
            strategyEvadeCommitSeconds = Mathf.Max(0f, strategyEvadeCommitSeconds);
            strategyReturnDoneThreshold = Mathf.Max(0.01f, strategyReturnDoneThreshold);
        }

        // Model feedback logs.
        private void LogAIAction(int action)
        {
            string[] labels = { "avoid_left", "avoid_right", "block_left", "block_right", "keep" };
            if (action >= 0 && action < 5)
            {
                
                Debug.Log($"[Model] {gameObject.name} decision -> {labels[action]}");
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