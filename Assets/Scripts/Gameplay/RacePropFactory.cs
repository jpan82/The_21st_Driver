using System.Collections.Generic;
using UnityEngine;

namespace The21stDriver.Gameplay
{
    public sealed class RacePropFactory
    {
        private readonly Transform root;
        private readonly List<Vector3> trackCenters;
        private readonly List<float> trackHalfRight;
        private readonly List<float> trackHalfLeft;
        private readonly float clearanceExtraMeters;

        public RacePropFactory(
            Transform root,
            List<Vector3> trackCenters,
            List<float> trackHalfRight,
            List<float> trackHalfLeft,
            float clearanceExtraMeters)
        {
            this.root = root;
            this.trackCenters = trackCenters;
            this.trackHalfRight = trackHalfRight;
            this.trackHalfLeft = trackHalfLeft;
            this.clearanceExtraMeters = clearanceExtraMeters;
        }

        public void CreateBlock(string blockName, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            if (TryKeepBlockOutsideTrack(position, scale, out Vector3 safePosition))
            {
                position = safePosition;
            }

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(root, false);
            block.transform.position = position;
            block.transform.rotation = rotation;
            block.transform.localScale = scale;
            Object.Destroy(block.GetComponent<Collider>());

            MeshRenderer renderer = block.GetComponent<MeshRenderer>();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        public void CreateGroundPlane(Vector3 boundsMin, Vector3 boundsMax, Material grassMaterial, float groundPaddingMeters)
        {
            if (grassMaterial == null)
            {
                return;
            }

            Vector3 center = (boundsMin + boundsMax) * 0.5f;
            Vector3 size = boundsMax - boundsMin;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Trackside_Grass";
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(center.x, -0.12f, center.z);
            ground.transform.localScale = new Vector3(
                Mathf.Max(1f, (size.x + groundPaddingMeters) / 10f),
                1f,
                Mathf.Max(1f, (size.z + groundPaddingMeters) / 10f));

            Object.Destroy(ground.GetComponent<Collider>());
            ground.GetComponent<MeshRenderer>().sharedMaterial = grassMaterial;
        }

        public void SpawnBarrierRow(
            Vector3 center,
            Quaternion rotation,
            Vector3 right,
            float sideSign,
            Material barrierMaterial,
            GameObject straightFencePrefab,
            GameObject curvedFencePrefab,
            bool useCurvedFence,
            bool useRibbonWidths,
            float halfLeftHere,
            float halfRightHere,
            float cornerSeverityDeg,
            float barrierOffsetMeters,
            float fenceOffsetMeters,
            float decorBarrierClearanceBeyondAsphaltMeters,
            float decorFenceClearanceBeyondAsphaltMeters,
            float curvedFenceMaxCornerDeg,
            float cornerFenceExtraOutwardMeters,
            float barrierThicknessMeters,
            float barrierHeightMeters,
            float barrierLengthMeters,
            float fencePrefabAdditionalOutwardMeters,
            float fenceVerticalOffset,
            float fencePrefabYawOffsetDegrees,
            float curvedFenceRightSideExtraYawDegrees)
        {
            float offsetSign = sideSign < 0f ? -1f : 1f;
            float barrierDist = barrierOffsetMeters;
            float fenceDist = fenceOffsetMeters;
            if (useRibbonWidths)
            {
                float halfOnSide = sideSign < 0f ? halfLeftHere : halfRightHere;
                barrierDist = halfOnSide + decorBarrierClearanceBeyondAsphaltMeters;
                fenceDist = halfOnSide + decorFenceClearanceBeyondAsphaltMeters;
            }

            float sharpness = Mathf.InverseLerp(curvedFenceMaxCornerDeg, 45f, cornerSeverityDeg);
            if (sharpness > 0f)
            {
                float extra = cornerFenceExtraOutwardMeters * sharpness;
                barrierDist += extra * 0.5f;
                fenceDist += extra;
            }

            float barrierHalfThick = barrierThicknessMeters * 0.5f;
            barrierDist += barrierHalfThick;
            fenceDist += barrierHalfThick + Mathf.Max(0f, fencePrefabAdditionalOutwardMeters);

            Vector3 barrierPosition = center + right * offsetSign * barrierDist + Vector3.up * (barrierHeightMeters * 0.5f);

            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = sideSign < 0f ? "Barrier_Left" : "Barrier_Right";
            barrier.transform.SetParent(root, false);
            barrier.transform.position = barrierPosition;
            barrier.transform.rotation = rotation;
            barrier.transform.localScale = new Vector3(barrierThicknessMeters, barrierHeightMeters, barrierLengthMeters);
            Object.Destroy(barrier.GetComponent<Collider>());

            MeshRenderer barrierRenderer = barrier.GetComponent<MeshRenderer>();
            if (barrierMaterial != null)
            {
                barrierRenderer.sharedMaterial = barrierMaterial;
            }

            bool pickCurved = useCurvedFence && curvedFencePrefab != null;
            GameObject fencePrefab = pickCurved ? curvedFencePrefab : straightFencePrefab;
            if (fencePrefab != null)
            {
                Vector3 fencePosition = center + right * offsetSign * fenceDist + Vector3.up * fenceVerticalOffset;
                float sideYaw = pickCurved && offsetSign > 0f ? curvedFenceRightSideExtraYawDegrees : 0f;
                Quaternion fenceRotation = rotation * Quaternion.Euler(0f, fencePrefabYawOffsetDegrees + sideYaw, 0f);
                GameObject fence = Object.Instantiate(fencePrefab, fencePosition, fenceRotation, root);
                fence.transform.localScale = Vector3.one;
            }
        }

        public void SpawnTireStack(
            Vector3 center,
            Vector3 right,
            float turnSide,
            Material tireMaterial,
            bool useRibbonWidths,
            float halfLeftHere,
            float halfRightHere,
            float tireStackOffsetMeters,
            float decorTireClearanceBeyondAsphaltMeters)
        {
            if (tireMaterial == null)
            {
                return;
            }

            float outerSide = turnSide >= 0f ? 1f : -1f;
            float dist = tireStackOffsetMeters;
            if (useRibbonWidths)
            {
                float halfOut = outerSide > 0f ? halfRightHere : halfLeftHere;
                dist = halfOut + decorTireClearanceBeyondAsphaltMeters;
            }

            Vector3 basePosition = center + right * outerSide * dist + Vector3.up * 0.5f;

            GameObject stackRoot = new GameObject(turnSide >= 0f ? "TireStack_Right" : "TireStack_Left");
            stackRoot.transform.SetParent(root, false);
            stackRoot.transform.position = basePosition;

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tire.transform.SetParent(stackRoot.transform, false);
                    tire.transform.localPosition = new Vector3((col - 1) * 0.55f, row * 0.28f, 0f);
                    tire.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    tire.transform.localScale = new Vector3(0.48f, 0.18f, 0.48f);
                    Object.Destroy(tire.GetComponent<Collider>());
                    tire.GetComponent<MeshRenderer>().sharedMaterial = tireMaterial;
                }
            }
        }

        public void EnforceTrackClearanceForAllProps()
        {
            if (root == null || trackCenters == null || trackCenters.Count < 2
                || trackHalfRight == null || trackHalfLeft == null
                || trackHalfRight.Count != trackCenters.Count)
            {
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name == "Trackside_Grass")
                {
                    continue;
                }

                if (!TryGetHierarchyBounds(child, out Bounds bounds))
                {
                    continue;
                }

                if (bounds.min.y > 1.2f)
                {
                    continue;
                }

                int idx = NearestCenterlineIndex(trackCenters, bounds.center);
                Vector3 center = trackCenters[idx];
                Vector3 right = GetTrackRightAtIndex(trackCenters, idx);
                float lateral = Vector3.Dot(bounds.center - center, right);

                float halfTrack = lateral >= 0f ? trackHalfRight[idx] : trackHalfLeft[idx];
                float footprint = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
                float required = halfTrack + footprint + clearanceExtraMeters;

                if (Mathf.Abs(lateral) >= required)
                {
                    continue;
                }

                float sign = lateral >= 0f ? 1f : -1f;
                if (Mathf.Abs(lateral) < 0.001f)
                {
                    sign = 1f;
                }

                float delta = sign * required - lateral;
                child.position += right * delta;
            }
        }

        private bool TryKeepBlockOutsideTrack(Vector3 position, Vector3 scale, out Vector3 adjusted)
        {
            adjusted = position;

            if (trackCenters == null || trackCenters.Count < 2
                || trackHalfRight == null || trackHalfLeft == null
                || trackHalfRight.Count != trackCenters.Count)
            {
                return false;
            }

            float baseY = position.y - scale.y * 0.5f;
            if (baseY > 1.2f)
            {
                return false;
            }

            int idx = NearestCenterlineIndex(trackCenters, position);
            Vector3 center = trackCenters[idx];
            Vector3 right = GetTrackRightAtIndex(trackCenters, idx);

            Vector3 toPos = position - center;
            float lateral = Vector3.Dot(toPos, right);
            float halfTrack = lateral >= 0f ? trackHalfRight[idx] : trackHalfLeft[idx];

            float footprint = 0.5f * Mathf.Sqrt(scale.x * scale.x + scale.z * scale.z);
            float required = halfTrack + footprint + clearanceExtraMeters;
            if (Mathf.Abs(lateral) >= required)
            {
                return false;
            }

            float sign = lateral >= 0f ? 1f : -1f;
            if (Mathf.Abs(lateral) < 0.001f)
            {
                sign = 1f;
            }

            float delta = sign * required - lateral;
            adjusted = position + right * delta;
            return true;
        }

        private static bool TryGetHierarchyBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            bool hasAny = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (!hasAny)
                {
                    bounds = r.bounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return hasAny;
        }

        private static int NearestCenterlineIndex(List<Vector3> centers, Vector3 world)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            Vector3 flat = world;
            flat.y = 0f;
            for (int i = 0; i < centers.Count; i++)
            {
                Vector3 p = centers[i];
                p.y = 0f;
                float sqr = (p - flat).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        private static Vector3 GetTrackRightAtIndex(List<Vector3> centers, int i)
        {
            if (centers == null || centers.Count < 2)
            {
                return Vector3.right;
            }

            i = Mathf.Clamp(i, 0, centers.Count - 1);
            Vector3 forward;
            if (i < centers.Count - 1)
            {
                forward = centers[i + 1] - centers[i];
            }
            else
            {
                forward = centers[i] - centers[i - 1];
            }

            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f)
            {
                return Vector3.right;
            }

            forward.Normalize();
            return Vector3.Cross(Vector3.up, forward).normalized;
        }
    }
}
