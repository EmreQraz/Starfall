using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Creates the space materials / skybox (once) and places the space backdrop seen through
    /// the windows: ringed gas giant, moon, asteroid field and a broken station.
    /// Models and textures are made in Blender (ArtSource/StarfallSpace.blend).
    /// </summary>
    public static class SpaceBackdropBuilder
    {
        public const string LAYER_NAME = "SpaceBackdrop";

        private const string ART_ROOT = "Assets/Starfall/Art/Space";
        private const string MODELS = ART_ROOT + "/Models/";
        private const string TEXTURES = ART_ROOT + "/Textures/";
        private const string MATERIALS = ART_ROOT + "/Materials/";
        private const string LIT_SHADER = "Universal Render Pipeline/Lit";
        private const int ASTEROID_COUNT = 16;
        private const int ASTEROID_SEED = 1234;

        public static Material SkyboxMaterial =>
            AssetDatabase.LoadAssetAtPath<Material>(MATERIALS + "M_Skybox_Starfield.mat");

        [MenuItem("Starfall/Setup Space Materials")]
        public static void SetupMaterials()
        {
            System.IO.Directory.CreateDirectory(MATERIALS);
            ConfigureTexture("T_Starfield", TextureWrapMode.Repeat, useMips: false, maxSize: 4096);
            ConfigureTexture("T_PlanetRing", TextureWrapMode.Clamp, useMips: true, maxSize: 1024, hasAlpha: true);

            var materials = new Dictionary<string, Material>
            {
                ["M_GasGiant"] = LitMaterial("M_GasGiant", "T_GasGiant", Color.white, 0.15f, 0f),
                ["M_PlanetRing"] = TransparentRing(LitMaterial("M_PlanetRing", "T_PlanetRing", Color.white, 0.1f, 0f)),
                ["M_Moon"] = LitMaterial("M_Moon", "T_Moon", Color.white, 0.05f, 0f),
                ["M_Rock"] = LitMaterial("M_Rock", "T_Rock", Color.white, 0.1f, 0f),
                ["M_StationHull"] = LitMaterial("M_StationHull", null, new Color(0.62f, 0.64f, 0.66f), 0.5f, 0.6f),
                ["M_StationDark"] = LitMaterial("M_StationDark", null, new Color(0.12f, 0.13f, 0.15f), 0.65f, 0.8f),
                ["M_SolarPanel"] = LitMaterial("M_SolarPanel", null, new Color(0.05f, 0.1f, 0.28f), 0.85f, 0.3f),
                ["M_StationLights"] = Emissive(LitMaterial("M_StationLights", null, new Color(1f, 0.35f, 0.1f), 0.5f, 0f),
                    new Color(1f, 0.35f, 0.1f) * 6f),
            };

            // Save first: reimporting the models reloads the remapped materials from disk, which would
            // drop unsaved edits such as the _EMISSION keyword.
            AssetDatabase.SaveAssets();
            foreach (var model in new[] { "SM_RingedPlanet", "SM_Moon", "SM_Asteroid_01", "SM_Asteroid_02",
                         "SM_Asteroid_03", "SM_Asteroid_04", "SM_StationFragment" })
            {
                RemapModelMaterials(MODELS + model + ".fbx", materials);
            }

            var sky = LoadOrCreateMaterial("M_Skybox_Starfield", Shader.Find("Skybox/Panoramic"));
            sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURES + "T_Starfield.png"));
            sky.SetFloat("_Mapping", 1f);      // latitude-longitude layout
            sky.SetFloat("_Exposure", 1.1f);
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
            Debug.Log("Starfall space materials ready.");
        }

        /// <summary>
        /// Places the backdrop. 'viewDirection' is the horizontal direction the windows face.
        /// Returns the root; also creates a directional "star light" that only lights the backdrop layer.
        /// </summary>
        public static Transform Build(Transform parent, Vector3 viewOrigin, Vector3 viewDirection)
        {
            SetupMaterials();
            int layer = SceneBuildUtils.EnsureLayer(LAYER_NAME);
            var root = SceneBuildUtils.Group("SpaceBackdrop", parent);
            var forward = viewDirection.normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            Vector3 At(float side, float up, float distance) => viewOrigin + right * side + Vector3.up * up + forward * distance;

            var planet = PlaceModel("SM_RingedPlanet", root, At(-40f, 60f, 650f), 220f, new Vector3(18f, -12f, 25f));
            Spin(planet, 0f, 0.4f, 0f);
            PlaceModel("SM_Moon", root, At(260f, 140f, 900f), 30f, Vector3.zero);
            var station = PlaceModel("SM_StationFragment", root, At(70f, 15f, 160f), 1f, new Vector3(20f, 30f, 10f));
            Spin(station, 0f, 0f, 1.5f);

            var asteroids = SceneBuildUtils.Group("AsteroidField", root);
            var rng = new System.Random(ASTEROID_SEED);
            float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            for (int i = 0; i < ASTEROID_COUNT; i++)
            {
                var pos = At(Range(-140f, 160f), Range(-40f, 60f), Range(45f, 260f));
                var rot = new Vector3(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));
                var rock = PlaceModel($"SM_Asteroid_0{1 + i % 4}", asteroids, pos, Range(1.5f, 8f), rot);
                Spin(rock, Range(-6f, 6f), Range(-6f, 6f), Range(-6f, 6f));
            }

            var sunGo = new GameObject("StarLight", typeof(Light));
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.LookRotation(right * 1f - Vector3.up * 0.25f + forward * 0.35f);
            var sun = sunGo.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.None;
            sun.cullingMask = 1 << layer;   // lights the backdrop only, never the ship interior
            RenderSettings.sun = sun;

            SceneBuildUtils.SetLayerRecursive(root.gameObject, layer);
            return root;
        }

        private static GameObject PlaceModel(string model, Transform parent, Vector3 position, float scale, Vector3 euler)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MODELS + model + ".fbx");
            if (asset == null) throw new System.IO.FileNotFoundException($"Space model missing: {MODELS}{model}.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return go;
        }

        private static void Spin(GameObject go, float x, float y, float z)
        {
            var turn = go.AddComponent<Turn_Move>();   // rotation helper shipped with the sci-fi kit
            turn.TurnX = x;
            turn.TurnY = y;
            turn.TurnZ = z;
        }

        private static void ConfigureTexture(string name, TextureWrapMode wrap, bool useMips, int maxSize, bool hasAlpha = false)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TEXTURES + name + ".png");
            if (importer == null) throw new System.IO.FileNotFoundException($"Texture missing: {TEXTURES}{name}.png");
            bool upToDate = importer.wrapMode == wrap && importer.mipmapEnabled == useMips &&
                            importer.maxTextureSize == maxSize && importer.alphaIsTransparency == hasAlpha;
            if (upToDate) return;

            importer.wrapMode = wrap;
            importer.mipmapEnabled = useMips;
            importer.maxTextureSize = maxSize;
            importer.alphaIsTransparency = hasAlpha;
            importer.SaveAndReimport();
        }

        private static Material LoadOrCreateMaterial(string name, Shader shader)
        {
            string path = MATERIALS + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material LitMaterial(string name, string texture, Color color, float smoothness, float metallic)
        {
            var material = LoadOrCreateMaterial(name, Shader.Find(LIT_SHADER));
            if (texture != null)
            {
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURES + texture + ".png"));
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TransparentRing(Material material)
        {
            const int TRANSPARENT_QUEUE = 3000;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = TRANSPARENT_QUEUE;
            return material;
        }

        private static Material Emissive(Material material, Color emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return material;
        }

        private static void RemapModelMaterials(string path, Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.IO.FileNotFoundException($"Model missing: {path}");
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var pair in materials)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            }
            importer.SaveAndReimport();
        }
    }
}
