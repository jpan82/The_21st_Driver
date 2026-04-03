using UnityEngine;
using System.Collections.Generic;

public class F1_TopDownCamera : MonoBehaviour
{
    private enum CameraMode { TopDown, ChaseWorld }

    [Header("俯视视角")]
    public float cameraFixedHeight = 400f; // Adjusted for better view
    public float followSmoothness = 5f;

    [Header("跟车视角")]
    public Vector3 chaseLocalOffset = new Vector3(0f, 1f, 3f); // world units: Y=up, Z=behind car
    public float chaseLookAtHeight = 2.5f;
    public float rotationSmoothness = 6f;

    [Header("距离控制")]
    public float distanceAdjustSpeed = 50f;
    public float minTopDownHeight = 50f;
    public float maxTopDownHeight = 1000f;
    public float minChaseDistance = 20f;
    public float maxChaseDistance = 800f;

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
        
        // ADDED THIS LINE: Find the cars from our new Race_Controller
        SmoothMover[] movers = Object.FindObjectsByType<SmoothMover>(FindObjectsSortMode.None);
        foreach (var m in movers) allCars.Add(m.transform);

        // Keep your old search logic for compatibility
        CSVMovementPlayer[] players = Object.FindObjectsByType<CSVMovementPlayer>(FindObjectsSortMode.None);
        foreach (var p in players) allCars.Add(p.transform);

        if (allCars.Count > 0)
        {
            currentCarIndex = Mathf.Clamp(currentCarIndex, 0, allCars.Count - 1);
            targetCar = allCars[currentCarIndex];
        }
    }
}
