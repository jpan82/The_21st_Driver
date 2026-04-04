using UnityEngine;
using System.Collections.Generic;
using The21stDriver.Gameplay;
using The21stDriver.Vehicles;

namespace The21stDriver.Camera
{
    public class F1_TopDownCamera : MonoBehaviour
    {
        private enum CameraMode { TopDown, ChaseWorld }

        [Header("俯视视角")]
        [Tooltip("World Y for orthographic-style top view; lower = closer, cars appear larger.")]
        public float cameraFixedHeight = 140f;
        public float followSmoothness = 5f;

        [Header("跟车视角")]
        public Vector3 chaseLocalOffset = new Vector3(0f, 2.5f, -8f); // local Z<0 = behind car
        public float chaseLookAtHeight = 2.5f;
        public float rotationSmoothness = 6f;

        [Header("距离控制")]
        public float distanceAdjustSpeed = 50f;
        public float minTopDownHeight = 25f;
        public float maxTopDownHeight = 1000f;
        public float minChaseDistance = 2f;
        public float maxChaseDistance = 80f;

        [Header("输入")]
        public KeyCode switchCarKey = KeyCode.Tab;
        public KeyCode switchCameraModeKey = KeyCode.C;
        public float orbitSpeed = 60f;

        private List<Transform> allCars = new List<Transform>();
        private int currentCarIndex = 0;
        private Transform targetCar;
        [SerializeField] private CameraMode cameraMode = CameraMode.TopDown;

        void LateUpdate()
        {
        if (targetCar == null)
        {
            RefreshTargets();
            return;
        }

        if (Input.GetKeyDown(switchCarKey))
        {
            RefreshTargets();
            if (allCars.Count == 0) return;
            currentCarIndex = (currentCarIndex + 1) % allCars.Count;
            targetCar = allCars[currentCarIndex];
        }

        if (Input.GetKeyDown(switchCameraModeKey))
        {
            cameraMode = (cameraMode == CameraMode.TopDown) ? CameraMode.ChaseWorld : CameraMode.TopDown;
        }

        HandleDistanceInput();
        HandleOrbitInput();

        if (cameraMode == CameraMode.TopDown) UpdateTopDownView();
        else UpdateChaseWorldView();
        }

        void UpdateTopDownView()
        {
        Vector3 targetPos = new Vector3(targetCar.position.x, cameraFixedHeight, targetCar.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSmoothness);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void UpdateChaseWorldView()
        {
        // Use rotation-only (no scale) so offset values are always in world units
        Vector3 desiredPosition = targetCar.position + targetCar.rotation * chaseLocalOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSmoothness);

        Vector3 lookTarget = targetCar.position + Vector3.up * chaseLookAtHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSmoothness);
        }

        void HandleDistanceInput()
        {
        float input = 0f;
        if (Input.GetKey(KeyCode.UpArrow))   input = -1f;
        if (Input.GetKey(KeyCode.DownArrow)) input =  1f;
        if (input == 0f) return;

        float delta = input * distanceAdjustSpeed * Time.deltaTime;
        if (cameraMode == CameraMode.TopDown)
            cameraFixedHeight = Mathf.Clamp(cameraFixedHeight + delta, minTopDownHeight, maxTopDownHeight);
        else
            chaseLocalOffset = chaseLocalOffset.normalized * Mathf.Clamp(chaseLocalOffset.magnitude + delta, minChaseDistance, maxChaseDistance);
        }

        void HandleOrbitInput()
        {
        float input = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))  input = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) input =  1f;
        if (input == 0f) return;

        float angle = input * orbitSpeed * Time.deltaTime;
        chaseLocalOffset = Quaternion.Euler(0f, angle, 0f) * chaseLocalOffset;
        }

        void RefreshTargets()
        {
        allCars.Clear();

        foreach (SmoothMover m in Object.FindObjectsByType<SmoothMover>(FindObjectsSortMode.None))
        {
            allCars.Add(m.transform);
        }

        foreach (CSVMovementPlayer p in Object.FindObjectsByType<CSVMovementPlayer>(FindObjectsSortMode.None))
        {
            allCars.Add(p.transform);
        }

        if (allCars.Count > 0)
        {
            currentCarIndex = Mathf.Clamp(currentCarIndex, 0, allCars.Count - 1);
            targetCar = allCars[currentCarIndex];
        }
        }
    }
}
