using UnityEngine;
using System;

namespace The21stDriver.Replay.Visuals
{
    public class ReplayVehicleVisualController : MonoBehaviour
    {
        [Header("References")]
        public Transform visualRoot;
        public Transform frontLeftSteerPivot;
        public Transform frontRightSteerPivot;
        public Transform frontLeftWheel;
        public Transform frontRightWheel;
        public Transform rearLeftWheel;
        public Transform rearRightWheel;

        [Header("Wheel Motion")]
        public bool enableWheelSpin = false;
        public float wheelRadius = 0.33f;
        public Vector3 wheelSpinAxis = Vector3.right;
        public float wheelSpinMultiplier = 1f;

        [Header("Steering")]
        public bool enableSteering = false;
        public float steerSensitivity = 0.35f;
        public float maxSteerAngle = 24f;
        public float steerSmoothness = 8f;
        [Tooltip("Local axis the steer pivots rotate around. Use Y for RB20, Z for f1_2022_free (GLB Z-up import).")]
        public Vector3 steerAxis = Vector3.up;

        [Header("Body Roll")]
        public bool enableBodyRoll = false;
        public float maxBodyRoll = 4f;
        public float bodyRollSmoothness = 6f;
        public bool invertBodyRoll = true;

        private Vector3 lastPosition;
        private Vector3 lastForward;
        private bool hasState;
        private float accumulatedWheelSpinDegrees;
        private float currentSteerAngle;
        private float currentBodyRoll;
        private Quaternion visualRootBaseRotation;
        private Quaternion frontLeftSteerBaseRotation;
        private Quaternion frontRightSteerBaseRotation;
        private Quaternion frontLeftWheelBaseRotation;
        private Quaternion frontRightWheelBaseRotation;
        private Quaternion rearLeftWheelBaseRotation;
        private Quaternion rearRightWheelBaseRotation;

        void Start()
        {
            AutoAssignIfNeeded();
            CacheBaseRotations();
            ResetMotionState();
        }

        void OnEnable()
        {
            ResetMotionState();
        }

        [ContextMenu("Auto Assign RB20 References")]
        private void AutoAssignRb20References()
        {
        visualRoot = FindChildRecursive("model_0_0");
        frontLeftSteerPivot = FindChildRecursive("model_1_1");
        frontRightSteerPivot = FindChildRecursive("model_2_2");
        frontLeftWheel = FindBestWheelTarget("Object_6", frontLeftSteerPivot);
        frontRightWheel = FindBestWheelTarget("Object_9", frontRightSteerPivot);
        rearLeftWheel = FindBestWheelTarget("Object_13", FindChildRecursive("model_4_4"));
        rearRightWheel = FindBestWheelTarget("Object_15", FindChildRecursive("model_5_5"));

        CacheBaseRotations();
        ResetMotionState();
        LogAssignedReferences();
        }

        [ContextMenu("Auto Assign F1_2022_Free References")]
        private void AutoAssignF1_2022_FreeReferences()
        {
        visualRoot = FindChildRecursive("GLTF_SceneRootNode");
        // Cylinder nodes serve as both steer pivot and wheel — frontLeftWheel/frontRightWheel
        // are left null so ApplyVisuals combines steer + spin into the pivot rotation
        frontLeftSteerPivot = FindChildRecursive("Cylinder_4");
        frontRightSteerPivot = FindChildRecursive("Cylinder.007_36");
        frontLeftWheel = null;
        frontRightWheel = null;
        // GLB import is Z-up: Cylinder local Y points along world Z, so steer on Z axis
        steerAxis = Vector3.forward;
        rearLeftWheel = FindChildRecursive("Cylinder.006_35");
        rearRightWheel = FindChildRecursive("Cylinder.001_14");

        CacheBaseRotations();
        ResetMotionState();
        LogAssignedReferences();
        }

        public void AutoAssignIfNeeded()
        {
        if (visualRoot != null &&
            frontLeftWheel != null &&
            frontRightWheel != null &&
            rearLeftWheel != null &&
            rearRightWheel != null)
        {
            return;
        }

        // Detect f1_2022_free by its unique wheel node name
        Transform cylinder4 = FindChildRecursive("Cylinder_4");
        Debug.Log($"[AutoAssign] GameObject='{gameObject.name}' Cylinder_4={(cylinder4 != null ? GetPath(cylinder4) : "NOT FOUND")}", this);
        if (cylinder4 != null)
        {
            AutoAssignF1_2022_FreeReferences();
        }
        else
        {
            AutoAssignRb20References();
        }
        }

        void LateUpdate()
        {
        Vector3 currentPosition = transform.position;

        if (!hasState)
        {
            lastPosition = currentPosition;
            lastForward = Vector3.forward;
            hasState = true;
            ApplyVisuals();
            return;
        }

        Vector3 delta = currentPosition - lastPosition;
        delta.y = 0f;
        float distanceMoved = delta.magnitude;

        // Derive forward from movement direction — transform.forward is unreliable
        // when the model is imported with a fixed X rotation (e.g. -90)
        Vector3 currentForward = distanceMoved > 0.001f ? delta.normalized : lastForward;
        float signedYawDelta = Vector3.SignedAngle(lastForward, currentForward, Vector3.up);
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float yawRate = signedYawDelta / deltaTime;

        if (enableWheelSpin)
        {
            float wheelCircumference = Mathf.Max(0.001f, 2f * Mathf.PI * wheelRadius);
            accumulatedWheelSpinDegrees += (distanceMoved / wheelCircumference) * 360f * wheelSpinMultiplier;
        }

        if (enableSteering)
        {
            float targetSteerAngle = Mathf.Clamp(yawRate * steerSensitivity, -maxSteerAngle, maxSteerAngle);
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, deltaTime * steerSmoothness);
        }
        else
        {
            currentSteerAngle = 0f;
        }

        if (enableBodyRoll)
        {
            float targetBodyRoll = maxSteerAngle <= Mathf.Epsilon
                ? 0f
                : (currentSteerAngle / maxSteerAngle) * maxBodyRoll;
            if (invertBodyRoll)
            {
                targetBodyRoll *= -1f;
            }

            currentBodyRoll = Mathf.Lerp(currentBodyRoll, targetBodyRoll, deltaTime * bodyRollSmoothness);
        }
        else
        {
            currentBodyRoll = 0f;
        }
        ApplyVisuals();

        lastPosition = currentPosition;
        lastForward = currentForward;
        }

        private void CacheBaseRotations()
        {
        visualRootBaseRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
        frontLeftSteerBaseRotation = frontLeftSteerPivot != null ? frontLeftSteerPivot.localRotation : Quaternion.identity;
        frontRightSteerBaseRotation = frontRightSteerPivot != null ? frontRightSteerPivot.localRotation : Quaternion.identity;
        frontLeftWheelBaseRotation = frontLeftWheel != null ? frontLeftWheel.localRotation : Quaternion.identity;
        frontRightWheelBaseRotation = frontRightWheel != null ? frontRightWheel.localRotation : Quaternion.identity;
        rearLeftWheelBaseRotation = rearLeftWheel != null ? rearLeftWheel.localRotation : Quaternion.identity;
        rearRightWheelBaseRotation = rearRightWheel != null ? rearRightWheel.localRotation : Quaternion.identity;
        }

        private void ResetMotionState()
        {
        hasState = false;
        accumulatedWheelSpinDegrees = 0f;
        currentSteerAngle = 0f;
        currentBodyRoll = 0f;
        }

        private void ApplyVisuals()
        {
        if (visualRoot != null)
        {
            visualRoot.localRotation = visualRootBaseRotation * Quaternion.Euler(0f, 0f, currentBodyRoll);
        }

        Quaternion wheelSpinRotation = Quaternion.AngleAxis(accumulatedWheelSpinDegrees, wheelSpinAxis.normalized);
        Quaternion steerRotation = Quaternion.AngleAxis(currentSteerAngle, steerAxis.normalized);

        if (frontLeftSteerPivot != null)
        {
            // If no separate front wheel node, combine steer + spin into the pivot
            Quaternion fl = frontLeftWheel == null ? steerRotation * wheelSpinRotation : steerRotation;
            frontLeftSteerPivot.localRotation = frontLeftSteerBaseRotation * fl;
        }

        if (frontRightSteerPivot != null)
        {
            Quaternion fr = frontRightWheel == null ? steerRotation * wheelSpinRotation : steerRotation;
            frontRightSteerPivot.localRotation = frontRightSteerBaseRotation * fr;
        }

        ApplyWheelRotation(frontLeftWheel, frontLeftWheelBaseRotation, wheelSpinRotation);
        ApplyWheelRotation(frontRightWheel, frontRightWheelBaseRotation, wheelSpinRotation);
        ApplyWheelRotation(rearLeftWheel, rearLeftWheelBaseRotation, wheelSpinRotation);
        ApplyWheelRotation(rearRightWheel, rearRightWheelBaseRotation, wheelSpinRotation);
        }

        private static void ApplyWheelRotation(Transform wheel, Quaternion baseRotation, Quaternion spinRotation)
        {
        if (wheel == null)
        {
            return;
        }

        wheel.localRotation = baseRotation * spinRotation;
        }

        private Transform FindChildRecursive(string targetName)
        {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        return FindChildRecursive(transform, targetName);
        }

        private Transform FindBestWheelTarget(string preferredName, Transform fallbackRoot)
        {
        Transform preferred = FindChildRecursive(preferredName);
        if (preferred != null)
        {
            Transform rendererChild = FindFirstRendererChild(preferred);
            return rendererChild != null ? rendererChild : preferred;
        }

        if (fallbackRoot == null)
        {
            return null;
        }

        Transform fallbackRendererChild = FindFirstRendererChild(fallbackRoot);
        return fallbackRendererChild != null ? fallbackRendererChild : fallbackRoot;
        }

        private static Transform FindFirstRendererChild(Transform parent)
        {
        if (parent == null)
        {
            return null;
        }

        Renderer renderer = parent.GetComponent<Renderer>();
        if (renderer != null)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindFirstRendererChild(parent.GetChild(i));
            if (result != null)
            {
                return result;
            }
        }

        return null;
        }

        private void LogAssignedReferences()
        {
        Debug.Log(
            "[ReplayVehicleVisualController] Auto-assigned references on " + gameObject.name +
            "\nvisualRoot: " + GetPath(visualRoot) +
            "\nfrontLeftSteerPivot: " + GetPath(frontLeftSteerPivot) +
            "\nfrontRightSteerPivot: " + GetPath(frontRightSteerPivot) +
            "\nfrontLeftWheel: " + GetPath(frontLeftWheel) +
            "\nfrontRightWheel: " + GetPath(frontRightWheel) +
            "\nrearLeftWheel: " + GetPath(rearLeftWheel) +
            "\nrearRightWheel: " + GetPath(rearRightWheel),
            this
        );
        }

        private static string GetPath(Transform target)
        {
        if (target == null)
        {
            return "<null>";
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
        if (parent == null)
        {
            return null;
        }

        if (string.Equals(parent.name, targetName, StringComparison.Ordinal))
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
        }
    }
}
