using Starfall.Devices;
using Starfall.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Adds the "this device is broken" effect: spark bursts with a crackle, smoke, an optional flickering
    /// fault light and a blinking SYSTEM FAILURE label. Put the returned root in the device's
    /// "Deactivate On Repair" list so everything stops once it is repaired.
    /// </summary>
    public static class MalfunctionBuilder
    {
        private const string FX_DIR = "Assets/Starfall/Art/FX/";
        private const string KIT_TEXTURES = "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD/Textures/Others/";
        private const string PARTICLE_SHADER = "Universal Render Pipeline/Particles/Unlit";
        private const float LABEL_PIXELS_PER_METER = 260f;
        private const float LABEL_WIDTH = 580f;
        private const float LABEL_HEIGHT = 96f;
        private const float LABEL_BORDER = 5f;
        private static readonly Color SPARK_HDR_TINT = new Color(6f, 3.2f, 1.2f, 1f);   // > 1 so bloom makes sparks glow
        private const int TRANSPARENT_QUEUE = 3000;

        private static readonly Color SPARK_COLOR = new Color(1f, 0.65f, 0.2f);
        private static readonly Color FAULT_RED = new Color(1f, 0.15f, 0.1f);

        /// <param name="sparkPoint">World point the sparks and smoke come from.</param>
        /// <param name="outward">Direction from the device surface towards the player.</param>
        /// <param name="labelPoint">World point of the warning label.</param>
        public static GameObject Add(Transform device, Vector3 sparkPoint, Vector3 outward, Vector3 labelPoint, bool addFaultLight)
        {
            var root = new GameObject("Malfunction");
            root.transform.SetParent(device, true);
            root.transform.SetPositionAndRotation(sparkPoint, Quaternion.LookRotation(outward));

            var sparks = CreateSparks(root.transform);
            CreateSmoke(root.transform);
            var flash = SceneBuildUtils.CreatePointLight("SparkFlash", root.transform, sparkPoint + outward * 0.3f, SPARK_COLOR, 0f, 4f);
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.volume = 0.6f;
            audio.minDistance = 1f;
            audio.maxDistance = 15f;

            var emitter = root.AddComponent<SparkEmitter>();
            SceneBuildUtils.SetFields(emitter, so =>
            {
                so.FindProperty("sparks").objectReferenceValue = sparks;
                so.FindProperty("flash").objectReferenceValue = flash;
                so.FindProperty("crackleSource").objectReferenceValue = audio;
            });

            if (addFaultLight)
            {
                var fault = SceneBuildUtils.CreatePointLight("FaultLight", root.transform, sparkPoint + outward * 0.6f, FAULT_RED, 2f, 5f);
                fault.gameObject.AddComponent<FlickeringLight>();
            }

            CreateWarningLabel(root.transform, labelPoint, outward);
            return root;
        }

        private static ParticleSystem CreateSparks(Transform parent)
        {
            var ps = CreateParticleSystem("Sparks", parent, SparkMaterial());
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            main.startColor = SPARK_COLOR;
            main.gravityModifier = 1.2f;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 0f;     // bursts come from SparkEmitter
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.03f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeOut(Color.white, new Color(1f, 0.3f, 0.05f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 3f;
            return ps;
        }

        private static void CreateSmoke(Transform parent)
        {
            var ps = CreateParticleSystem("Smoke", parent, SmokeMaterial());
            ps.transform.localRotation = Quaternion.LookRotation(Vector3.up);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.1f, 0.1f, 0.11f, 0.6f);
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 8f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.08f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.5f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = FadeOut(Color.white, Color.white);
        }

        private static ParticleSystem CreateParticleSystem(string name, Transform parent, Material material)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        private static ParticleSystem.MinMaxGradient FadeOut(Color start, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void CreateWarningLabel(Transform parent, Vector3 position, Vector3 outward)
        {
            var go = new GameObject("WarningLabel", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-outward));
            go.transform.localScale = Vector3.one / LABEL_PIXELS_PER_METER;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(LABEL_WIDTH, LABEL_HEIGHT);
            go.AddComponent<BlinkingGraphic>();

            var border = UiBuildUtils.CreateImage("Border", go.transform, FAULT_RED);
            UiBuildUtils.Stretch(border.rectTransform);
            var plate = UiBuildUtils.CreateImage("Plate", go.transform, new Color(0.06f, 0.01f, 0.01f, 1f));
            UiBuildUtils.Stretch(plate.rectTransform, LABEL_BORDER);
            var text = UiBuildUtils.CreateText("Text", go.transform, "! SYSTEM FAILURE !", 52, FAULT_RED, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            UiBuildUtils.Stretch(text.rectTransform);
        }

        private static Material SparkMaterial()
        {
            var material = ParticleMaterial("M_Sparks", "Glow.png");
            material.SetColor("_BaseColor", SPARK_HDR_TINT);
            material.SetFloat("_Blend", 2f);   // additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            return material;
        }

        private static Material SmokeMaterial()
        {
            var material = ParticleMaterial("M_Smoke", "Smoke.png");
            material.SetFloat("_Blend", 0f);   // alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            return material;
        }

        private static Material ParticleMaterial(string name, string kitTexture)
        {
            string path = FX_DIR + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                System.IO.Directory.CreateDirectory(FX_DIR);
                material = new Material(Shader.Find(PARTICLE_SHADER));
                AssetDatabase.CreateAsset(material, path);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KIT_TEXTURES + kitTexture);
            if (texture == null) Debug.LogWarning($"FX texture missing: {KIT_TEXTURES}{kitTexture}");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);   // callers may override (e.g. HDR sparks)
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = TRANSPARENT_QUEUE;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
