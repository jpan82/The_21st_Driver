using UnityEngine;
using System.Collections.Generic;

public class F1_TopDownCamera : MonoBehaviour
{
    [Header("高度设置")]
    public float cameraFixedHeight = 600f; // 【关键】：设定一个固定的海拔高度
    public float followSmoothness = 5f;    // 跟随平滑度

    private List<Transform> allCars = new List<Transform>();
    private int currentCarIndex = 0;
    private Transform targetCar;

    void LateUpdate()
    {
        // 1. 查找赛车
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

        // 2. Tab 键切换目标
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentCarIndex = (currentCarIndex + 1) % allCars.Count;
            targetCar = allCars[currentCarIndex];
        }

        // 3. 计算位置：X 和 Z 跟随赛车，但 Y 轴始终使用 cameraFixedHeight
        // 这样即使赛车爬坡到 Y=530，相机依然留在 Y=600
        Vector3 targetPos = new Vector3(targetCar.position.x, cameraFixedHeight, targetCar.position.z);
        
        // 4. 平滑移动位置
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSmoothness);

        // 5. 强制垂直俯视 (X=90, Y=0, Z=0)
        transform.rotation = Quaternion.Euler(90, 0, 0);
    }
}