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
        private float rotationDelayTimer = 0f;
        private const float ROTATION_DELAY = 2f;
        
        private Model_Script aiModel;
        private Vector3 lastPosition;
        private float currentSpeed;
        private int lastAction = -1;

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
            // 初始化模型
            aiModel = GetComponentInChildren<Model_Script>();
            lastPosition = transform.position;
        }

        void Update()
        {
            if (sampler == null || !sampler.IsValid) return;
            
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

            Vector3 currentPos = sampler.SamplePosition(t);

            // NPC自动寻找玩家赛车
            if (aiModel != null && aiModel.playerCar == null)
            {
                PlayerCarController spawnedPlayer = FindObjectOfType<PlayerCarController>();
                if (spawnedPlayer != null)
                {
                    aiModel.playerCar = spawnedPlayer.transform;
                    Debug.Log($"{gameObject.name} 成功锁定了玩家目标！");
                }
            }
            
			// 模型提供反馈
            if (aiModel != null && aiModel.playerCar != null && Time.deltaTime > 0)
            {
                currentSpeed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
				float[] features = BuildAIFeatures();
				int action = aiModel.GetAIAction(features);

				if (action != lastAction)
				{
					LogAIAction(action);
					lastAction = action;
				}
            }
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

            float lookT = Mathf.Min(t + 0.05f, sampler.EndTime);
            Vector3 targetPos = sampler.SamplePosition(lookT);
            Vector3 direction = new Vector3(targetPos.x - currentPos.x, 0f, targetPos.z - currentPos.z).normalized;

            if (direction.sqrMagnitude > 0.001f && rotationDelayTimer > ROTATION_DELAY)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction, Vector3.up);
                Quaternion targetRotation = Quaternion.Euler(ctrl.fixedXRotation, lookRot.eulerAngles.y, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * ctrl.rotationSmoothness);
            }
        }

        // 模型数据
        private float[] BuildAIFeatures()
        {
            float[] features = (float[])defaultFeatures.Clone();
            Transform player = aiModel.playerCar;

            Vector3 relativePos = player.position - transform.position;
            float approxDeltaS = Vector3.Dot(relativePos, transform.forward); 
            float approxDeltaD = Vector3.Dot(relativePos, transform.right);   

            features[0] = approxDeltaS;                             
            features[1] = approxDeltaD;                             
            features[3] = relativePos.magnitude;                    
            features[4] = currentSpeed * 3.6f;                      
            features[7] = 0f; // 因为是只读测试，横向偏移永远是 0

            return features;
        }

        // 模型反馈
        private void LogAIAction(int action)
        {
            string[] labels = { "avoid_left (应向左避让)", "avoid_right (应向右避让)", "block_left (应向左防守)", "block_right (应向右防守)", "keep (保持路线)" };
            if (action >= 0 && action < 5)
            {
                
                Debug.Log($"[Model] {gameObject.name} 决定 -> {labels[action]}");
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