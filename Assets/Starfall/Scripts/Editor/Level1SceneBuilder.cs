using System.Collections.Generic;
using System.Linq;
using Starfall.Devices;
using Starfall.Interaction;
using Starfall.Level;
using Starfall.Questions;
using Starfall.Settings;
using Starfall.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static Starfall.EditorTools.SceneBuildUtils;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Builds Assets/Starfall/Scenes/Level1.unity from kit prefabs. Menu: Starfall > Build Level 1 Scene.
    /// Layout (top view, +X east, +Z north): cabin (x 0..18) -> cabin door -> corridor (x 20..44)
    /// -> exit door -> exit vestibule with the Level 2 trigger. All windows face south (-Z).
    /// </summary>
    public static class Level1SceneBuilder
    {
        public const string SCENE_PATH = "Assets/Starfall/Scenes/Level1.unity";
        private const string QUESTIONS = "Assets/Starfall/Data/Questions/";
        private const string POST_FX_PATH = "Assets/Starfall/Settings/Level1_PostFX.asset";
        private const string HUD_PATH = "Assets/Starfall/Prefabs/UI/StarfallHUD.prefab";
        private const string PLAYER_PREFAB = "NestedParent_Unpack";
        private const string NEXT_SCENE = "Level2";

        private const float SEGMENT = 6f;            // kit wall / floor module size
        private const float DOOR_WIDTH_SCALE = 0.6f; // kit door is 10 m wide; 0.6 fits one 6 m wall segment
        private const float KIT_DOOR_WIDTH = 10f;
        private const float CABIN_DOOR_X = 18f;
        private const float CORRIDOR_START_X = 20f;
        private const int CORRIDOR_SEGMENTS = 4;
        private const float CORRIDOR_Z = 6f;          // corridor floor spans z 6..12
        private const float DOOR_TOP_Z = 12f;         // door openings span z 6..12
        private const float CONSOLE_DEPTH = 1.5f;     // Wall_Console_01_Large thickness
        private const float LEVEL_MUSIC_VOLUME = 0.3f;

        private static readonly Color EMERGENCY_RED = new Color(1f, 0.12f, 0.08f);
        private static readonly Color PANEL_CYAN = new Color(0.2f, 0.85f, 1f);
        private static readonly Color REPAIRED_GREEN = new Color(0.3f, 1f, 0.5f);

        private static float CorridorEndX => CORRIDOR_START_X + CORRIDOR_SEGMENTS * SEGMENT;

        [MenuItem("Starfall/Build Level 1 Scene")]
        public static void Build()
        {
            SceneBuildUtils.BuildSceneAt(SCENE_PATH, BuildContents);
            PlaceholderSceneBuilder.BuildLevel2();
            SceneBuildUtils.UpdateBuildSettings();
        }

        private static void BuildContents()
        {
            SpaceBackdropBuilder.SetupMaterials();   // skybox must exist before render settings use it
            ApplyRenderSettings();
            var env = Group("Environment");

            BuildCabin(Group("Cabin", env));
            var cabinDoor = BuildDoor("CabinDoor", env, CABIN_DOOR_X);
            BuildCorridor(Group("Corridor", env), out var corridorLights, out var console);
            var exitDoor = BuildDoor("ExitDoor", env, CorridorEndX);
            var exitLights = BuildExitArea(Group("ExitArea", env), out var exitTrigger);

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HUD_PATH));
            BuildPlayer(hud);

            var devices = Group("Devices");
            var doorPanel = BuildDoorPanel(devices, cabinDoor, corridorLights);
            var terminal = BuildTerminal(console, exitLights);

            var manager = new GameObject("LevelManager").AddComponent<LevelManager>();
            SetFields(manager, so =>
            {
                SetObjectArray(so.FindProperty("devices"), new Object[] { doorPanel, terminal });
                so.FindProperty("exitDoor").objectReferenceValue = exitDoor;
                so.FindProperty("nextSceneName").stringValue = NEXT_SCENE;
            });
            SetFields(exitTrigger, so => so.FindProperty("levelManager").objectReferenceValue = manager);

            BuildPostProcessing();
            MenuSceneBuilder.CreateMusic(LEVEL_MUSIC_VOLUME);
            new GameObject("SfxVolume").AddComponent<SfxVolumeApplier>();
            UiBuildUtils.CreateFader();

            var windowCenter = new Vector3(CORRIDOR_START_X + 2 * SEGMENT, 2f, 0f);
            SpaceBackdropBuilder.Build(null, windowCenter, Vector3.back);
        }

        // ---------- Cabin ----------

        private static void BuildCabin(Transform parent)
        {
            var room = Place("Simple_Room_01", parent, Vector3.zero, 0f);
            foreach (var light in room.GetComponentsInChildren<Light>(true)) light.gameObject.SetActive(false); // power is out

            // East wall middle segment -> cabin door opening. South wall middle segment -> porthole.
            FindChildAt(room, "Wall_Simple_05_Long", new Vector3(18f, 0f, 12f)).SetActive(false);
            var southWall = FindChildAt(room, "Wall_Simple_05_Long", new Vector3(12f, 0f, 0f));
            southWall.SetActive(false);
            var porthole = Place("Porthole_01_Long", parent, southWall.transform.position, southWall.transform.eulerAngles.y);
            SetActive(LightObjects(porthole), false);

            AddBoundsCollider(Place("Wall_Bed_01", parent, new Vector3(0f, 0f, 6f), 0f));
            AddBoundsCollider(Place("Wall_Table_01", parent, new Vector3(6f, 0f, 18f), 90f));
            PlaceCrate(parent, new Vector3(15f, 0f, 15.6f), 15f);
            PlaceCrate(parent, new Vector3(13.4f, 0f, 16.6f), 90f);
            PlaceCrate(parent, new Vector3(15.1f, 0.75f, 15.5f), 35f);

            EmergencyLight(parent, new Vector3(0f, 3.1f, 9f), 0f, 14f);
            EmergencyLight(parent, new Vector3(18f, 3.1f, 3.5f), 180f, 10f);
        }

        private static void EmergencyLight(Transform parent, Vector3 position, float yRotation, float range)
        {
            var fixture = Place("Light_SideLight_01", parent, position, yRotation, name: "EmergencyLight");
            var light = fixture.GetComponentInChildren<Light>(true);
            light.color = EMERGENCY_RED;
            light.intensity = 2.5f;
            light.range = range;
        }

        private static void PlaceCrate(Transform parent, Vector3 position, float yRotation)
        {
            var crate = Place("Crate_01", parent, position, yRotation);
            SetActive(LightObjects(crate), false);
            AddBoundsCollider(crate);
        }

        // ---------- Doors ----------

        /// <summary>
        /// Kit sliding door (two half arches + two leaves), scaled to fill the 6 m opening
        /// z 6..12 in a wall whose inner face is at x = wallX.
        /// </summary>
        private static Door BuildDoor(string name, Transform parent, float wallX)
        {
            float width = KIT_DOOR_WIDTH * DOOR_WIDTH_SCALE;
            var root = Group(name, parent);
            root.position = new Vector3(wallX, 0f, DOOR_TOP_Z);
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

        // ---------- Corridor ----------

        private static void BuildCorridor(Transform parent, out List<GameObject> corridorLights, out GameObject console)
        {
            corridorLights = new List<GameObject>();
            console = null;
            for (int i = 0; i < CORRIDOR_SEGMENTS; i++)
            {
                float x = CORRIDOR_START_X + i * SEGMENT;
                var segment = Place("Corridor_Test_01", parent, new Vector3(x, 0f, CORRIDOR_Z), 0f, name: $"Corridor_{i + 1}");

                bool hasWindow = i == 1 || i == 2;
                if (hasWindow)
                {
                    var wall = FindChildAt(segment, "Wall_Simple_01_Long", new Vector3(SEGMENT, 0f, 0f));
                    wall.SetActive(false);
                    var porthole = Place("Porthole_01_Long", parent, wall.transform.position, wall.transform.eulerAngles.y);
                    corridorLights.AddRange(LightObjects(porthole));
                }
                if (i == CORRIDOR_SEGMENTS - 1)
                {
                    // The console is thinner than the wall, so it stands in front of the wall instead of replacing it.
                    var wall = FindChildAt(segment, "Wall_Simple_01_Long", new Vector3(0f, 0f, SEGMENT));
                    var position = wall.transform.position + wall.transform.right * CONSOLE_DEPTH;
                    console = Place("Wall_Console_01_Large", parent, position, wall.transform.eulerAngles.y,
                        name: "Terminal_Console");
                    corridorLights.AddRange(LightObjects(console));
                }

                corridorLights.AddRange(LightObjects(segment));
                var ceiling = Place("Light_Corridor_01", parent, new Vector3(x + SEGMENT / 2f, 4f, CORRIDOR_Z + SEGMENT / 2f), 90f);
                corridorLights.AddRange(LightObjects(ceiling));
            }

            PlaceCrate(parent, new Vector3(22.6f, 0f, 11f), 90f);
            PlaceCrate(parent, new Vector3(34.5f, 0f, 7f), 90f);
            PlaceCrate(parent, new Vector3(34.6f, 0.75f, 7.1f), 75f);
            Place("Sign_Danger_01", parent, new Vector3(29f, 2.2f, 11.97f), 90f, Vector3.one * 0.25f);

            SetActive(corridorLights, false);   // switched on when the door panel is repaired
        }

        private static List<GameObject> BuildExitArea(Transform parent, out LevelExitTrigger trigger)
        {
            float vestibuleX = CorridorEndX;   // its side walls also close the gap beside the door frames
            var vestibule = Place("Corridor_Test_01", parent, new Vector3(vestibuleX, 0f, CORRIDOR_Z), 0f, name: "ExitVestibule");
            var endWall = Place("Wall_Simple_01_Long", parent, new Vector3(vestibuleX + SEGMENT, 0f, DOOR_TOP_Z), 180f, name: "ExitEndWall");

            var exitLights = LightObjects(vestibule).Concat(LightObjects(endWall)).ToList();
            Place("Word_Exit", parent, new Vector3(CorridorEndX - 0.05f, 3.55f, CORRIDOR_Z + SEGMENT / 2f), 180f,
                Vector3.one * 0.11f);
            exitLights.Add(CreatePointLight("ExitGlow_A", parent, new Vector3(CorridorEndX - 0.7f, 3.2f, 7.2f), REPAIRED_GREEN, 3f, 7f).gameObject);
            exitLights.Add(CreatePointLight("ExitGlow_B", parent, new Vector3(CorridorEndX - 0.7f, 3.2f, 10.8f), REPAIRED_GREEN, 3f, 7f).gameObject);
            SetActive(exitLights, false);

            var triggerGo = new GameObject("Level2Trigger", typeof(BoxCollider));
            triggerGo.transform.SetParent(parent, false);
            triggerGo.transform.position = new Vector3(vestibuleX + SEGMENT / 2f, 1.5f, CORRIDOR_Z + SEGMENT / 2f);
            var box = triggerGo.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3f, 3f, SEGMENT - 0.5f);
            trigger = triggerGo.AddComponent<LevelExitTrigger>();
            return exitLights;
        }

        // ---------- Devices ----------

        private static RepairableDevice BuildDoorPanel(Transform parent, Door cabinDoor, List<GameObject> corridorLights)
        {
            var panel = Place("Intercom_01", parent, new Vector3(CABIN_DOOR_X - 0.02f, 1.5f, 13.3f), 180f, Vector3.one * 1.6f,
                "Device1_DoorPanel");
            AddBoundsCollider(panel);

            var brokenGlow = panel.GetComponentInChildren<Light>(true);
            brokenGlow.color = PANEL_CYAN;
            brokenGlow.intensity = 2f;
            brokenGlow.range = 3.5f;
            var repairedGlow = CreatePointLight("RepairedGlow", panel.transform, panel.transform.position + Vector3.left * 0.4f,
                REPAIRED_GREEN, 1.5f, 3f);
            repairedGlow.gameObject.SetActive(false);

            var device = panel.AddComponent<RepairableDevice>();
            SetFields(device, so =>
            {
                so.FindProperty("deviceName").stringValue = "door panel";
                SetObjectArray(so.FindProperty("questions"), new Object[] { Question("L1_D1_Q1_IfElse"), Question("L1_D1_Q2_And") });
                so.FindProperty("doorToOpen").objectReferenceValue = cabinDoor;
                SetObjectArray(so.FindProperty("activateOnRepair"),
                    corridorLights.Append(repairedGlow.gameObject).Cast<Object>().ToList());
                SetObjectArray(so.FindProperty("deactivateOnRepair"), new Object[] { brokenGlow.gameObject });
            });
            return device;
        }

        private static RepairableDevice BuildTerminal(GameObject console, List<GameObject> exitLights)
        {
            AddBoundsCollider(console);
            var device = console.AddComponent<RepairableDevice>();
            SetFields(device, so =>
            {
                so.FindProperty("deviceName").stringValue = "corridor terminal";
                SetObjectArray(so.FindProperty("questions"), new Object[] { Question("L1_D2_Q3_Assignment"), Question("L1_D2_Q4_Float") });
                SetObjectArray(so.FindProperty("activateOnRepair"), exitLights.Cast<Object>().ToList());
            });
            return device;
        }

        private static QuestionData Question(string name)
        {
            var q = AssetDatabase.LoadAssetAtPath<QuestionData>(QUESTIONS + name + ".asset");
            if (q == null) throw new System.IO.FileNotFoundException($"Question asset missing: {QUESTIONS}{name}.asset");
            return q;
        }

        // ---------- Player, lighting ----------

        private static void BuildPlayer(GameObject hud)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(PLAYER_PREFAB));
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            player.name = "Player";

            var capsule = player.transform.Find("PlayerCapsule").gameObject;
            capsule.transform.SetPositionAndRotation(new Vector3(2.8f, 0.1f, 9f), Quaternion.Euler(0f, 90f, 0f));

            var camera = player.transform.Find("MainCamera").GetComponent<Camera>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // The Cinemachine 3 version of the Starter Assets prefab loses the follow target.
            var cameraRoot = capsule.transform.Find("PlayerCameraRoot");
            var followCamera = player.transform.Find("PlayerFollowCamera").GetComponent("CinemachineCamera");
            SetFields(followCamera, so => so.FindProperty("Target.TrackingTarget").objectReferenceValue = cameraRoot);

            capsule.AddComponent<MouseSensitivityApplier>();
            var playerLock = capsule.AddComponent<PlayerControlLock>();
            var interactor = capsule.AddComponent<PlayerInteractor>();
            SetFields(interactor, so =>
            {
                so.FindProperty("viewOrigin").objectReferenceValue = camera.transform;
                so.FindProperty("promptUI").objectReferenceValue = hud.GetComponentInChildren<InteractionPromptUI>(true);
            });
            SetFields(hud.GetComponentInChildren<QuestionPanel>(true),
                so => so.FindProperty("playerLock").objectReferenceValue = playerLock);
        }

        private static void ApplyRenderSettings()
        {
            RenderSettings.skybox = SpaceBackdropBuilder.SkyboxMaterial;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.055f);
            RenderSettings.reflectionIntensity = 0.25f;
            RenderSettings.fog = false;
        }

        private static void BuildPostProcessing()
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
