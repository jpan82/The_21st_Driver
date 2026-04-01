using UnityEngine;
using System.Collections.Generic;

public class F1_TopDownCamera : MonoBehaviour
{
    private enum CameraMode
    {
        TopDown,
        ChaseWorld
    }

    [Header("俯视视角")]
    public float cameraFixedHeight = 600f;
    public float followSmoothness = 5f;

    [Header("跟车视角")]
    public Vector3 chaseWorldOffset = new Vector3(0f, 130f, -200f);
    public float chaseLookAtHeight = 2.5f;
    public float rotationSmoothness = 6f;

    [Header("输入")]
    public KeyCode switchCarKey = KeyCode.Tab;
    public KeyCode switchCameraModeKey = KeyCode.C;

    private List<Transform> allCars = new List<Transform>();
    private int currentCarIndex = 0;
    private Transform targetCar;
    private CameraMode cameraMode = CameraMode.TopDown;

    void LateUpdate()
    {
        if (targetCar == null)
        {
            RefreshTargets();
            return;
        }

        if (Input.GetKeyDown(switchCarKey))
        {
            if (allCars.Count == 0)
            {
                RefreshTargets();
            }

            if (allCars.Count == 0)
            {
                return;
            }

            currentCarIndex = (currentCarIndex + 1) % allCars.Count;
            targetCar = allCars[currentCarIndex];
        }

        if (Input.GetKeyDown(switchCameraModeKey))
        {
            cameraMode = GetNextCameraMode(cameraMode);
        }

        if (cameraMode == CameraMode.TopDown)
        {
            UpdateTopDownView();
        }
        else
        {
            UpdateChaseWorldView();
        }
    }

    void UpdateTopDownView()
    {
        Vector3 targetPos = new Vector3(targetCar.position.x, cameraFixedHeight, targetCar.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSmoothness);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void UpdateChaseWorldView()
    {
        Vector3 desiredPosition = targetCar.position + chaseWorldOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSmoothness);

        Vector3 lookTarget = targetCar.position + Vector3.up * chaseLookAtHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSmoothness);
    }

    CameraMode GetNextCameraMode(CameraMode currentMode)
    {
        if (currentMode == CameraMode.TopDown)
        {
            return CameraMode.ChaseWorld;
        }

        return CameraMode.TopDown;
    }

    void RefreshTargets()
    {
        allCars.Clear();

        F1_Driver_Follower[] drivers = Object.FindObjectsByType<F1_Driver_Follower>(FindObjectsSortMode.None);
        foreach (var driver in drivers)
        {
            allCars.Add(driver.transform);
        }

        if (allCars.Count == 0)
        {
            CSVMovementPlayer[] singleCarPlayers = Object.FindObjectsByType<CSVMovementPlayer>(FindObjectsSortMode.None);
            foreach (var player in singleCarPlayers)
            {
                allCars.Add(player.transform);
            }
        }

        if (allCars.Count > 0)
        {
            currentCarIndex = Mathf.Clamp(currentCarIndex, 0, allCars.Count - 1);
            targetCar = allCars[currentCarIndex];
        }
    }
}