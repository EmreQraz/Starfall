using Starfall.Devices;
using Starfall.Interaction;
using Starfall.Questions;
using Starfall.Settings;
using Starfall.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Starfall.EditorTools.SceneBuildUtils;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Station pieces shared by the gameplay scene builders (Level 1, Transit): kit doors, crates,
    /// emergency lights, the player rig, render settings and post-processing.
    /// </summary>
    public static class StationBuildUtils
    {
        public const float SEGMENT = 6f;            // kit wall / floor module size
        public const string HUD_PATH = "Assets/Starfall/Prefabs/UI/StarfallHUD.prefab";

        private const string QUESTIONS = "Assets/Starfall/Data/Questions/";
        private const string POST_FX_PATH = "Assets/Starfall/Settings/Level1_PostFX.asset";
        private const string PLAYER_PREFAB = "NestedParent_Unpack";
        private const float DOOR_WIDTH_SCALE = 0.6f; // kit door is 10 m wide; 0.6 fits one 6 m wall segment
        private const float KIT_DOOR_WIDTH = 10f;

        public static readonly Color EMERGENCY_RED = new Color(1f, 0.12f, 0.08f);
        public static readonly Color PANEL_CYAN = new Color(0.2f, 0.85f, 1f);
        public static readonly Color REPAIRED_GREEN = new Color(0.3f, 1f, 0.5f);

        public static GameObject InstantiateHud() =>
            (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HUD_PATH));

        public static void EmergencyLight(Transform parent, Vector3 position, float yRotation, float range)
        {
            var fixture = Place("Light_SideLight_01", parent, position, yRotation, name: "EmergencyLight");
            var light = fixture.GetComponentInChildren<Light>(true);
            light.color = EMERGENCY_RED;
            light.intensity = 2.5f;
            light.range = range;
        }

        public static void PlaceCrate(Transform parent, Vector3 position, float yRotation)
        {
            var crate = Place("Crate_01", parent, position, yRotation);
            SetActive(LightObjects(crate), false);
            AddBoundsCollider(crate);
        }

        /// <summary>
        /// Kit sliding door (two half arches + two leaves), scaled to fill a 6 m opening that runs from
        /// openingTopZ - 6 to openingTopZ in a wall whose inner face is at x = wallX.
        /// </summary>
        public static Door BuildDoor(string name, Transform parent, float wallX, float openingTopZ)
        {
            float width = KIT_DOOR_WIDTH * DOOR_WIDTH_SCALE;
            var root = Group(name, parent);
            root.position = new Vector3(wallX, 0f, openingTopZ);
            var scale = new Vector3(DOOR_WIDTH_SCALE, 1f, 1f);

            Place("DoorWay_01_Large", root, root.position, 270f, scale, "Frame_A");
            Place("DoorWay_01_Large", root, root.position + new Vector3(1f, 0f, -width), 90f, scale, "Frame_B");
            var left = Place("Door_Left_01", root, root.position + new Vector3(1f, 0f, 0f), 270f, scale, "Leaf_Left");
            var right = Place("Door_Right_01", root, root.position + new Vector3(1f, 0f, -width), 270f, scale, "Leaf_Right");

            var door = root.gameObject.AddComponent<Door>();
            float slide = width * 0.5f;
            SetFields(door, so =>
            {
                var leaves = so.FindProperty("leaves");
                leaves.arraySize = 2;
                leaves.GetArrayElementAtIndex(0).FindPropertyRelative("transform").objectReferenceValue = left.transform;
                leaves.GetArrayElementAtIndex(0).FindPropertyRelative("openOffset").vector3Value = new Vector3(0f, 0f, slide);
                leaves.GetArrayElementAtIndex(1).FindPropertyRelative("transform").objectReferenceValue = right.transform;
                leaves.GetArrayElementAtIndex(1).FindPropertyRelative("openOffset").vector3Value = new Vector3(0f, 0f, -slide);
            });
            return door;
        }

        public static QuestionData Question(string name)
        {
            var q = AssetDatabase.LoadAssetAtPath<QuestionData>(QUESTIONS + name + ".asset");
            if (q == null) throw new System.IO.FileNotFoundException($"Question asset missing: {QUESTIONS}{name}.asset");
            return q;
        }

        /// <summary>
        /// Starter Assets first person rig with the Starfall interaction components, wired to the HUD.
        /// Returns the PlayerCapsule (tagged "Player"); its camera is Camera.main once the scene runs.
        /// </summary>
        public static GameObject BuildPlayer(GameObject hud, Vector3 position, float yRotation, out Camera camera)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(PLAYER_PREFAB));
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            player.name = "Player";

            var capsule = player.transform.Find("PlayerCapsule").gameObject;
            capsule.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yRotation, 0f));

            camera = player.transform.Find("MainCamera").GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // The Cinemachine 3 version of the Starter Assets prefab loses the follow target.
            var cameraRoot = capsule.transform.Find("PlayerCameraRoot");
            var followCamera = player.transform.Find("PlayerFollowCamera").GetComponent("CinemachineCamera");
            SetFields(followCamera, so => so.FindProperty("Target.TrackingTarget").objectReferenceValue = cameraRoot);

            capsule.AddComponent<MouseSensitivityApplier>();
            var playerLock = capsule.AddComponent<PlayerControlLock>();
            var interactor = capsule.AddComponent<PlayerInteractor>();
            var viewOrigin = camera.transform;
            SetFields(interactor, so =>
            {
                so.FindProperty("viewOrigin").objectReferenceValue = viewOrigin;
                so.FindProperty("promptUI").objectReferenceValue = hud.GetComponentInChildren<InteractionPromptUI>(true);
            });
            SetFields(hud.GetComponentInChildren<QuestionPanel>(true),
                so => so.FindProperty("playerLock").objectReferenceValue = playerLock);
            return capsule;
        }

        public static void ApplyRenderSettings()
        {
            RenderSettings.skybox = SpaceBackdropBuilder.SkyboxMaterial;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.055f);
            RenderSettings.reflectionIntensity = 0.25f;
            RenderSettings.fog = false;
        }

        public static void BuildPostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(POST_FX_PATH);
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(POST_FX_PATH));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, POST_FX_PATH);

                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(0.9f);
                bloom.intensity.Override(0.9f);
                bloom.scatter.Override(0.65f);
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.3f);
                vignette.smoothness.Override(0.5f);
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
                AssetDatabase.SaveAssets();
            }

            var volume = new GameObject("GlobalVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }
}
