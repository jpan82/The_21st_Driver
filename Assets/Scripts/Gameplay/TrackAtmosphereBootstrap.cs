using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using The21stDriver.Vehicles;

namespace The21stDriver.Gameplay
{
    [DisallowMultipleComponent]
    public class TrackAtmosphereBootstrap : MonoBehaviour
    {
        private const string Cc0SkyResource = "CC0_Race/Track/autumn_field_puresky_2k";
        private const string Cc0ConcreteDiffResource = "CC0_Race/Barrier/concrete_road_barrier_diff_2k";
        private const string Cc0ConcreteNorResource = "CC0_Race/Barrier/concrete_road_barrier_nor_gl_2k";
        private const string Cc0AsphaltColorResource = "CC0_Race/Track/asphalt_track_diff_2k";
        private const string Cc0AsphaltNorResource = "CC0_Race/Track/asphalt_track_nor_gl_2k";

        [SerializeField] private VolumeProfile postProcessProfile;

        [Header("CC0 atmosphere assets (loaded from Resources/CC0_Race by default, drag in overrides if needed)")]
        [SerializeField] private bool useBundledCc0FromResources = true;
        [SerializeField] private Texture2D panoramicSkyOverride;
        [SerializeField] private float panoramicSkyExposure = 1.05f;
        [SerializeField] private float panoramicSkyRotationDegrees;
        [SerializeField] private Texture2D concreteAlbedoOverride;
        [SerializeField] private Texture2D concreteNormalOverride;
        [SerializeField] private Texture2D asphaltAlbedoOverride;
        [SerializeField] private Texture2D asphaltNormalOverride;

        private bool applied;
        private Material backdropStandLowerMaterial;
        private Material backdropStandUpperMaterial;
        private Material backdropPoleMaterial;
        private Material backdropGroundMaterial;

        private void Start()
        {
            StartCoroutine(ApplyWhenCarsExist());
        }

        private IEnumerator ApplyWhenCarsExist()
        {
            for (int i = 0; i < 20; i++)
            {
                if (HasAnyActiveCar())
                {
                    break;
                }

                yield return null;
            }

            ApplyAtmosphere();
        }

        private void ApplyAtmosphere()
        {
            if (applied)
            {
                return;
            }

            applied = true;

            Camera targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                targetCamera.farClipPlane = 4000f;

                UniversalAdditionalCameraData additionalCameraData = targetCamera.GetUniversalAdditionalCameraData();
                if (postProcessProfile != null)
                {
                    additionalCameraData.renderPostProcessing = true;
                }
            }

            ConfigureLightingAndSkybox();

            Texture2D concreteAlbedo = ResolveConcreteAlbedo();
            Texture2D concreteNormal = ResolveConcreteNormal();
            Texture2D asphaltAlbedo = ResolveAsphaltAlbedo();
            Texture2D asphaltNormal = ResolveAsphaltNormal();

            backdropStandLowerMaterial = CreateTexturedLitMaterial(new Color(0.16f, 0.17f, 0.20f, 1f), concreteAlbedo, concreteNormal);
            backdropStandUpperMaterial = CreateTexturedLitMaterial(new Color(0.23f, 0.24f, 0.27f, 1f), concreteAlbedo, concreteNormal);
            backdropPoleMaterial = CreateTexturedLitMaterial(new Color(0.14f, 0.15f, 0.17f, 1f), concreteAlbedo, concreteNormal);

            backdropGroundMaterial = null;
            if (asphaltAlbedo != null)
            {
                backdropGroundMaterial = CreateTexturedLitMaterial(Color.white, asphaltAlbedo, asphaltNormal);
                ApplyTextureTiling(backdropGroundMaterial, new Vector2(280f, 280f));
            }

            if (postProcessProfile != null)
            {
                CreateGlobalVolume(postProcessProfile);
            }

            Vector3 center = EstimateSceneCenter();
            BuildBackdrop(center);
            BuildAtmosphericParticles(center);

            DynamicGI.UpdateEnvironment();
        }

        private bool HasAnyActiveCar()
        {
            if (FindObjectsByType<SmoothMover>(FindObjectsSortMode.None).Length > 0)
            {
                return true;
            }

            return FindObjectsByType<CSVMovementPlayer>(FindObjectsSortMode.None).Length > 0;
        }

        private Vector3 EstimateSceneCenter()
        {
            List<Vector3> positions = new List<Vector3>();

            foreach (SmoothMover mover in FindObjectsByType<SmoothMover>(FindObjectsSortMode.None))
            {
                positions.Add(mover.transform.position);
            }

            foreach (CSVMovementPlayer player in FindObjectsByType<CSVMovementPlayer>(FindObjectsSortMode.None))
            {
                positions.Add(player.transform.position);
            }

            if (positions.Count == 0)
            {
                return transform.position;
            }

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < positions.Count; i++)
            {
                sum += positions[i];
            }

            return sum / positions.Count;
        }

        private void ConfigureLightingAndSkybox()
        {
            bool usedHdriSky = false;
            Texture2D skyTexture = panoramicSkyOverride;
            if (skyTexture == null && useBundledCc0FromResources)
            {
                skyTexture = Resources.Load<Texture2D>(Cc0SkyResource);
            }

            if (skyTexture != null)
            {
                Shader panoramicShader = Shader.Find("Skybox/Panoramic");
                if (panoramicShader != null)
                {
                    Material skyboxMaterial = new Material(panoramicShader);
                    skyboxMaterial.SetTexture("_MainTex", skyTexture);
                    skyboxMaterial.SetFloat("_Exposure", panoramicSkyExposure);
                    skyboxMaterial.SetFloat("_Rotation", panoramicSkyRotationDegrees);
                    RenderSettings.skybox = skyboxMaterial;
                    usedHdriSky = true;
                }
            }

            if (!usedHdriSky)
            {
                Shader skyboxShader = Shader.Find("Skybox/Procedural");
                if (skyboxShader != null)
                {
                    Material skyboxMaterial = new Material(skyboxShader);
                    skyboxMaterial.SetColor("_SkyTint", new Color(0.38f, 0.50f, 0.72f, 1f));
                    skyboxMaterial.SetColor("_GroundColor", new Color(0.04f, 0.05f, 0.06f, 1f));
                    skyboxMaterial.SetFloat("_Exposure", 1.05f);
                    skyboxMaterial.SetFloat("_SunSize", 0.05f);
                    skyboxMaterial.SetFloat("_AtmosphereThickness", 1.35f);
                    RenderSettings.skybox = skyboxMaterial;
                }
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.40f, 0.49f, 0.58f, 1f);
            RenderSettings.fogDensity = 0.00185f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.30f, 0.34f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.16f, 0.18f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
        }

        private void CreateGlobalVolume(VolumeProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                Volume existing = volumes[i];
                if (existing == null || !existing.isGlobal)
                {
                    continue;
                }

                if (existing.sharedProfile == profile || existing.gameObject.name == "Track Global Volume")
                {
                    existing.sharedProfile = profile;
                    existing.priority = Mathf.Max(existing.priority, 10f);
                    return;
                }
            }

            GameObject volumeObject = new GameObject("Track Global Volume");
            volumeObject.layer = gameObject.layer;

            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        private void BuildBackdrop(Vector3 center)
        {
            Transform root = new GameObject("Track_Backdrop").transform;
            root.position = Vector3.zero;

            float radius = 1800f;
            int standCount = 12;
            float standWidth = 160f;
            float standDepth = 28f;

            for (int i = 0; i < standCount; i++)
            {
                float angle = (Mathf.PI * 2f / standCount) * i;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 basePosition = center + direction * radius;

                GameObject standRoot = new GameObject("Backdrop_Stand_" + i);
                standRoot.transform.SetParent(root, false);
                standRoot.transform.position = basePosition;
                standRoot.transform.rotation = Quaternion.LookRotation((center - basePosition).normalized, Vector3.up);

                CreateStandBlock(standRoot.transform, new Vector3(0f, 12f, 0f), new Vector3(standWidth, 24f, standDepth), backdropStandLowerMaterial);
                CreateStandBlock(standRoot.transform, new Vector3(0f, 26f, -2f), new Vector3(standWidth * 0.95f, 8f, standDepth * 0.92f), backdropStandUpperMaterial);
                CreateCrowdBand(standRoot.transform, standWidth, standDepth);
            }

            CreateBackdropPoles(root, center, radius * 0.72f);

            if (backdropGroundMaterial != null)
            {
                CreateBackdropGround(root, center);
            }
        }

        private void CreateBackdropPoles(Transform root, Vector3 center, float radius)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = (Mathf.PI * 2f / 4f) * i + 0.35f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 position = center + direction * radius;

                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Light_Pole_" + i;
                pole.transform.SetParent(root, false);
                pole.transform.position = position + Vector3.up * 24f;
                pole.transform.rotation = Quaternion.LookRotation((center - position).normalized, Vector3.up);
                pole.transform.localScale = new Vector3(0.8f, 24f, 0.8f);
                Destroy(pole.GetComponent<Collider>());
                pole.GetComponent<Renderer>().sharedMaterial = backdropPoleMaterial;

                GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "Light_Cluster_" + i;
                lamp.transform.SetParent(root, false);
                lamp.transform.position = position + Vector3.up * 50f;
                lamp.transform.localScale = Vector3.one * 4f;
                Destroy(lamp.GetComponent<Collider>());
                lamp.GetComponent<Renderer>().sharedMaterial = CreateLitMaterial(new Color(1f, 0.95f, 0.75f, 1f));
            }
        }

        private void CreateStandBlock(Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            Destroy(block.GetComponent<Collider>());
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void CreateBackdropGround(Transform root, Vector3 center)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Backdrop_Ground";
            plane.transform.SetParent(root, false);
            float groundY = center.y - 12f;
            plane.transform.position = new Vector3(center.x, groundY, center.z);
            const float planeMeshExtent = 10f;
            float worldSpan = 9200f;
            float scale = worldSpan / planeMeshExtent;
            plane.transform.localScale = new Vector3(scale, 1f, scale);
            Destroy(plane.GetComponent<Collider>());
            plane.GetComponent<Renderer>().sharedMaterial = backdropGroundMaterial;
        }

        private Texture2D ResolveConcreteAlbedo()
        {
            if (concreteAlbedoOverride != null)
            {
                return concreteAlbedoOverride;
            }

            return useBundledCc0FromResources ? Resources.Load<Texture2D>(Cc0ConcreteDiffResource) : null;
        }

        private Texture2D ResolveConcreteNormal()
        {
            if (concreteNormalOverride != null)
            {
                return concreteNormalOverride;
            }

            return useBundledCc0FromResources ? Resources.Load<Texture2D>(Cc0ConcreteNorResource) : null;
        }

        private Texture2D ResolveAsphaltAlbedo()
        {
            if (asphaltAlbedoOverride != null)
            {
                return asphaltAlbedoOverride;
            }

            return useBundledCc0FromResources ? Resources.Load<Texture2D>(Cc0AsphaltColorResource) : null;
        }

        private Texture2D ResolveAsphaltNormal()
        {
            if (asphaltNormalOverride != null)
            {
                return asphaltNormalOverride;
            }

            return useBundledCc0FromResources ? Resources.Load<Texture2D>(Cc0AsphaltNorResource) : null;
        }

        private static void ApplyTextureTiling(Material material, Vector2 tiling)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", tiling);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_MainTex", tiling);
            }

            if (material.HasProperty("_BumpMap"))
            {
                material.SetTextureScale("_BumpMap", tiling);
            }
        }

        private Material CreateTexturedLitMaterial(Color tint, Texture2D albedo, Texture2D normalMap)
        {
            if (albedo == null && normalMap == null)
            {
                return CreateLitMaterial(tint);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            if (albedo != null)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", albedo);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", albedo);
                }
            }

            if (normalMap != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }

            material.SetColor("_Color", tint);
            return material;
        }

        private void CreateCrowdBand(Transform parent, float standWidth, float standDepth)
        {
            GameObject sparkObject = new GameObject("Crowd_Band");
            sparkObject.transform.SetParent(parent, false);
            sparkObject.transform.localPosition = new Vector3(0f, 31f, -1f);

            ParticleSystem particleSystem = sparkObject.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.92f, 0.92f, 0.94f, 0.55f),
                new Color(0.75f, 0.12f, 0.12f, 0.55f));
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = particleSystem.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(16f, 28f);

            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(standWidth * 0.95f, 3f, standDepth * 0.75f);
            shape.position = new Vector3(0f, 0.5f, -1f);

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.72f, 0.24f, 0.2f), 0.5f),
                    new GradientColorKey(new Color(1f, 1f, 1f, 0.05f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.8f, 0.15f),
                    new GradientAlphaKey(0.4f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.05f;
            noise.scrollSpeed = 0.1f;

            particleSystem.Play();

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = CreateParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private void BuildAtmosphericParticles(Vector3 center)
        {
            CreateHazeSystem(center + new Vector3(0f, 12f, 0f));
            CreateDustSystem(center + new Vector3(0f, 5f, 0f));
        }

        private void CreateHazeSystem(Vector3 position)
        {
            GameObject hazeObject = new GameObject("Track_Haze");
            hazeObject.transform.position = position;

            ParticleSystem particleSystem = hazeObject.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.94f, 0.96f, 1f, 0.08f));
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = particleSystem.emission;
            emission.rateOverTime = 10f;

            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1800f, 80f, 1800f);

            var velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // Keep X/Y/Z in the same MinMaxCurve mode to avoid
            // "Particle Velocity curves must all be in the same mode" warnings.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.03f;
            noise.scrollSpeed = 0.06f;

            particleSystem.Play();

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = CreateParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private void CreateDustSystem(Vector3 position)
        {
            GameObject dustObject = new GameObject("Track_Dust");
            dustObject.transform.position = position;

            ParticleSystem particleSystem = dustObject.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.88f, 0.88f, 0.85f, 0.18f));
            main.maxParticles = 450;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = particleSystem.emission;
            emission.rateOverTime = 20f;

            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1400f, 4f, 1400f);

            var velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // Keep X/Y/Z in the same MinMaxCurve mode to avoid
            // "Particle Velocity curves must all be in the same mode" warnings.
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            particleSystem.Play();

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = CreateParticleMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private Material CreateLitMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        private Material CreateParticleMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            Material material = new Material(shader);
            material.color = Color.white;
            return material;
        }
    }
}