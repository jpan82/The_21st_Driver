using UnityEngine;

namespace The21stDriver.Gameplay
{
    /// <summary>
    /// Arcade-kinematic player F1 car. WASD input, matched to the race-start gate
    /// that NPC SmoothMovers observe (first 5 seconds of level load).
    /// Spawned at runtime by Race_Controller.SpawnPlayerCar().
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerCarController : MonoBehaviour
    {
        [Header("Driving")]
        public float maxSpeed          = 70f;   // m/s (~252 km/h)
        public float acceleration      = 25f;   // m/s² forward
        public float brakeDecel        = 45f;   // m/s² when braking while moving forward
        public float coastDecel        = 6f;    // m/s² passive coast-to-stop
        public float reverseMaxSpeed   = 12f;   // m/s reverse cap
        public float steerDegPerSec    = 90f;   // yaw rate at low speed
        [Range(0f, 1f)]
        public float highSpeedSteerScale = 0.35f; // fraction of steerDegPerSec at maxSpeed

        [Header("Track Boundary")]
        public float outOfBoundsGraceMeters = 1.0f; // must exceed edge by this much to trigger game over

        Rigidbody  rb;
        Race_Controller ctrl;
        float      currentSpeed; // signed; + forward
        ReplayTrackSurface trackSurface;
        bool isGameOver;
        public bool IsOutOfBounds { get; private set; }
        public float CurrentSpeedMs => currentSpeed;

        /// <summary>Called by Race_Controller immediately after AddComponent.</summary>
        public void Init(Race_Controller controller)
        {
            ctrl = controller;
            trackSurface = FindObjectOfType<ReplayTrackSurface>();
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic  = true;
            rb.useGravity   = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            if (isGameOver) return;

            // Mirror the 5-second race-start gate that SmoothMover NPCs observe
            if (ctrl != null && !ctrl.RaceStarted)
            {
                currentSpeed = 0f;
                return;
            }

            float throttle = Input.GetAxisRaw("Vertical");    // W/S or Up/Down
            float steer    = Input.GetAxisRaw("Horizontal");  // A/D or Left/Right

            // --- Longitudinal ---
            if (throttle > 0.01f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed,
                    acceleration * Time.fixedDeltaTime);
            }
            else if (throttle < -0.01f)
            {
                float decel = (currentSpeed > 0f) ? brakeDecel : acceleration;
                currentSpeed = Mathf.MoveTowards(currentSpeed, -reverseMaxSpeed,
                    decel * Time.fixedDeltaTime);
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f,
                    coastDecel * Time.fixedDeltaTime);
            }

            // --- Lateral (speed-scaled steering) ---
            // Steering is less responsive at high speed (F1-like, avoids spinny Mario-Kart feel)
            float speedFactor = Mathf.Lerp(1f, highSpeedSteerScale,
                Mathf.InverseLerp(0f, maxSpeed, Mathf.Abs(currentSpeed)));
            // Reverse steering inverts when going backward
            float reverseSign = (currentSpeed < 0f) ? -1f : 1f;
            float yawDelta = steer * steerDegPerSec * speedFactor * reverseSign
                             * Time.fixedDeltaTime;

            // --- Kinematic move ---
            Quaternion newRot = rb.rotation * Quaternion.Euler(0f, yawDelta, 0f);
            Vector3    newPos = rb.position + newRot * Vector3.forward * currentSpeed * Time.fixedDeltaTime;

            // --- Out-of-bounds check ---
            Vector3 carForward = newRot * Vector3.forward;
            if (trackSurface != null &&
                trackSurface.TryGetAdditionalLateralOffsetBounds(newPos, 0f, out float minOff, out float maxOff, carForward))
            {
                // maxOff < 0 means car is already past the right edge; minOff > 0 means past the left edge
                IsOutOfBounds = maxOff < -outOfBoundsGraceMeters || minOff > outOfBoundsGraceMeters;
            }
            else
            {
                IsOutOfBounds = false;
            }

            rb.MoveRotation(newRot);
            rb.MovePosition(newPos);
        }

        void TriggerGameOver()
        {
            isGameOver = true;
            currentSpeed = 0f;
            if (ctrl != null) ctrl.OnPlayerGameOver();
        }
    }
}
