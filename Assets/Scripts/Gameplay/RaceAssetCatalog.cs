using UnityEngine;

namespace The21stDriver.Gameplay
{
    public static class RaceAssetCatalog
    {
        public struct TracksideAssets
        {
            public Texture2D grassAlbedo;
            public Texture2D grassNormal;
            public Texture2D barrierAlbedo;
            public Texture2D barrierNormal;
            public Texture2D rubberAlbedo;
            public Texture2D rubberNormal;
            public GameObject straightFencePrefab;
            public GameObject curvedFencePrefab;
        }

        public struct TracksideMaterials
        {
            public Material grass;
            public Material barrier;
            public Material tire;
        }

        public struct OperationsMaterials
        {
            public Material board;
            public Material post;
            public Material flag;
            public Material banner;
        }

        public struct LandmarkMaterials
        {
            public Material paint;
            public Material gantry;
            public Material lightOn;
            public Material paddock;
        }

        public static TracksideAssets LoadTracksideAssets()
        {
            TracksideAssets assets = new TracksideAssets
            {
                grassAlbedo = Resources.Load<Texture2D>("CC0_Race/Grass/Grass001_1K-JPG_Color"),
                grassNormal = Resources.Load<Texture2D>("CC0_Race/Grass/Grass001_1K-JPG_NormalGL"),
                barrierAlbedo = Resources.Load<Texture2D>("CC0_Race/Barrier/concrete_road_barrier_diff_2k"),
                barrierNormal = Resources.Load<Texture2D>("CC0_Race/Barrier/concrete_road_barrier_nor_gl_2k"),
                rubberAlbedo = Resources.Load<Texture2D>("CC0_Race/Rubber/Rubber004_2K-JPG_Color"),
                rubberNormal = Resources.Load<Texture2D>("CC0_Race/Rubber/Rubber004_2K-JPG_NormalGL"),
                straightFencePrefab = Resources.Load<GameObject>("CC0_Race/Kenney/fenceStraight"),
                curvedFencePrefab = Resources.Load<GameObject>("CC0_Race/Kenney/fenceCurved")
            };

            return assets;
        }

        public static TracksideMaterials BuildTracksideMaterials(TracksideAssets assets)
        {
            TracksideMaterials mats = new TracksideMaterials
            {
                grass = CreateTexturedLitMaterial(new Color(0.38f, 0.49f, 0.23f, 1f), assets.grassAlbedo, assets.grassNormal),
                barrier = CreateTexturedLitMaterial(Color.white, assets.barrierAlbedo, assets.barrierNormal),
                tire = CreateTexturedLitMaterial(new Color(0.12f, 0.12f, 0.12f, 1f), assets.rubberAlbedo, assets.rubberNormal)
            };

            if (mats.grass != null)
            {
                ApplyTextureTiling(mats.grass, new Vector2(60f, 60f));
            }

            if (mats.barrier != null)
            {
                ApplyTextureTiling(mats.barrier, new Vector2(8f, 1f));
            }

            if (mats.tire != null)
            {
                ApplyTextureTiling(mats.tire, new Vector2(3f, 3f));
            }

            return mats;
        }

        public static OperationsMaterials BuildOperationsMaterials(Material frameMaterial, Color marshalFlagColor)
        {
            OperationsMaterials mats = new OperationsMaterials
            {
                board = CreateTexturedLitMaterial(new Color(0.95f, 0.95f, 0.95f, 1f), null, null),
                post = frameMaterial ?? CreateTexturedLitMaterial(new Color(0.22f, 0.23f, 0.26f, 1f), null, null),
                flag = CreateTexturedLitMaterial(marshalFlagColor, null, null),
                banner = CreateTexturedLitMaterial(new Color(0.78f, 0.08f, 0.08f, 1f), null, null)
            };

            return mats;
        }

        public static LandmarkMaterials BuildLandmarkMaterials()
        {
            LandmarkMaterials mats = new LandmarkMaterials
            {
                paint = CreateTexturedLitMaterial(new Color(0.95f, 0.95f, 0.95f, 1f), null, null),
                gantry = CreateTexturedLitMaterial(new Color(0.10f, 0.11f, 0.13f, 1f), null, null),
                lightOn = CreateTexturedLitMaterial(new Color(0.88f, 0.14f, 0.14f, 1f), null, null),
                paddock = CreateTexturedLitMaterial(new Color(0.36f, 0.38f, 0.42f, 1f), null, null)
            };

            return mats;
        }

        public static Material CreateSponsorBoardMaterial(int index)
        {
            Color[] palette =
            {
                new Color(0.85f, 0.10f, 0.10f, 1f),
                new Color(0.05f, 0.10f, 0.14f, 1f),
                new Color(0.06f, 0.42f, 0.76f, 1f),
                new Color(0.85f, 0.63f, 0.08f, 1f),
                new Color(0.95f, 0.95f, 0.95f, 1f)
            };

            return CreateTexturedLitMaterial(palette[Mathf.Abs(index) % palette.Length], null, null);
        }

        public static Material CreateTexturedLitMaterial(Color tint, Texture2D albedo, Texture2D normalMap)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
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

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }

            return material;
        }

        public static void ApplyTextureTiling(Material material, Vector2 tiling)
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
    }
}
