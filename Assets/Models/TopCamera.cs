using UnityEngine;
using System.Collections.Generic;

public class F1_TopDownCamera : MonoBehaviour
{
    private enum CameraMode
    {
        TopDown,
        Chase
    }

    [Header("俯视视角")]
    public float cameraFixedHeight = 600f;
    public float followSmoothness = 5f;

    [Header("跟车视角")]
    public Vector3 chaseOffset = new Vector3(0f, 10f, -14f);
    public float chaseLookAtHeight = 1.5f;
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
            F1_Driver_Follower[] drivers = Object.FindObjectsByType<F1_Driver_Follower>(FindObjectsSortMode.None);
            if (drivers.Length > 0)
            {
                allCars.Clear();
                foreach (var d in drivers) allCars.Add(d.transform);
                targetCar = allCars[0];
            }
            return;
        }

        if (Input.GetKeyDown(switchCarKey))
        {
            currentCarIndex = (currentCarIndex + 1) % allCars.Count;
            targetCar = allCars[currentCarIndex];
        }

        if (Input.GetKeyDown(switchCameraModeKey))
        {
            cameraMode = cameraMode == CameraMode.TopDown ? CameraMode.Chase : CameraMode.TopDown;
        }

        if (cameraMode == CameraMode.TopDown)
        {
            UpdateTopDownView();
        }
        else
        {
            UpdateChaseView();
        }
    }

    void UpdateTopDownView()
    {
        Vector3 targetPos = new Vector3(targetCar.position.x, cameraFixedHeight, targetCar.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSmoothness);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void UpdateChaseView()
    {
        Vector3 rotatedOffset = targetCar.rotation * chaseOffset;
        Vector3 desiredPosition = targetCar.position + rotatedOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSmoothness);

        Vector3 lookTarget = targetCar.position + Vector3.up * chaseLookAtHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationSmoothness);
    }
}