using System.Collections.Generic;
using System.Linq;
using Starfall.Devices;
using Starfall.Level;
using Starfall.Settings;
using Starfall.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Starfall.EditorTools.SceneBuildUtils;
using static Starfall.EditorTools.StationBuildUtils;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Builds Assets/Starfall/Scenes/Level1.unity from kit prefabs. Menu: Starfall > Build Level 1 Scene.
    /// Layout (top view, +X east, +Z north): cabin (x 0..18) -> cabin door -> corridor (x 20..44)
    /// -> exit door -> exit vestibule with the trigger to the Transit scene. All windows face south (-Z).
    /// </summary>
    public static class Level1SceneBuilder
    {
        public const string SCENE_PATH = "Assets/Starfall/Scenes/Level1.unity";
        private const string NEXT_SCENE = "Transit";

        private const float CABIN_DOOR_X = 18f;
        private const float CORRIDOR_START_X = 20f;
        private const int CORRIDOR_SEGMENTS = 4;
        private const float CORRIDOR_Z = 6f;          // corridor floor spans z 6..12
        private const float DOOR_TOP_Z = 12f;         // door openings span z 6..12
        private const float CONSOLE_DEPTH = 1.5f;     // Wall_Console_01_Large thickness
        private const float LEVEL_MUSIC_VOLUME = 0.3f;

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
            var cabinDoor = BuildDoor("CabinDoor", env, CABIN_DOOR_X, DOOR_TOP_Z);
            BuildCorridor(Group("Corridor", env), out var corridorLights, out var console);
            var exitDoor = BuildDoor("ExitDoor", env, CorridorEndX, DOOR_TOP_Z);
            var exitLights = BuildExitArea(Group("ExitArea", env), out var exitTrigger);

            var hud = InstantiateHud();
            BuildPlayer(hud, new Vector3(2.8f, 0.1f, 9f), 90f, out _);

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
            brokenGlow.gameObject.AddComponent<FlickeringLight>();
            var malfunction = MalfunctionBuilder.Add(panel.transform, new Vector3(17.85f, 2.05f, 13.05f), Vector3.left,
                new Vector3(17.8f, 2.45f, 13.3f), addFaultLight: false);

            var device = panel.AddComponent<RepairableDevice>();
            SetFields(device, so =>
            {
                so.FindProperty("deviceName").stringValue = "door panel";
                SetObjectArray(so.FindProperty("questions"), new Object[] { Question("L1_D1_Q1_IfElse"), Question("L1_D1_Q2_And") });
                so.FindProperty("doorToOpen").objectReferenceValue = cabinDoor;
                SetObjectArray(so.FindProperty("activateOnRepair"),
                    corridorLights.Append(repairedGlow.gameObject).Cast<Object>().ToList());
                SetObjectArray(so.FindProperty("deactivateOnRepair"), new Object[] { brokenGlow.gameObject, malfunction });
            });
            return device;
        }

        private static RepairableDevice BuildTerminal(GameObject console, List<GameObject> exitLights)
        {
            AddBoundsCollider(console);
            var malfunction = MalfunctionBuilder.Add(console.transform, new Vector3(40.5f, 2.4f, 10.45f), Vector3.back,
                new Vector3(41f, 3.3f, 10.4f), addFaultLight: true);
            var repairedGlow = CreatePointLight("RepairedGlow", console.transform, new Vector3(41f, 2.2f, 10f),
                REPAIRED_GREEN, 1.5f, 4f);
            repairedGlow.gameObject.SetActive(false);

            var device = console.AddComponent<RepairableDevice>();
            SetFields(device, so =>
            {
                so.FindProperty("deviceName").stringValue = "corridor terminal";
                SetObjectArray(so.FindProperty("questions"), new Object[] { Question("L1_D2_Q3_Assignment"), Question("L1_D2_Q4_Float") });
                SetObjectArray(so.FindProperty("activateOnRepair"),
                    exitLights.Append(repairedGlow.gameObject).Cast<Object>().ToList());
                SetObjectArray(so.FindProperty("deactivateOnRepair"), new Object[] { malfunction });
            });
            return device;
        }
    }
}
