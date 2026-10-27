using System.Collections.Generic;
using System.Linq;
using Starfall.Combat;
using Starfall.Devices;
using Starfall.Interaction;
using Starfall.Level;
using Starfall.Settings;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.AI;
using static Starfall.EditorTools.SceneBuildUtils;
using static Starfall.EditorTools.StationBuildUtils;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Builds Assets/Starfall/Scenes/Transit.unity, the alien attack between Level 1 and Level 2.
    /// Menu: Starfall > Build Transit Scene.
    /// Layout (top view, +X east, +Z north): armory (x 0..18) with the locked weapon locker -> armory door
    /// -> long corridor (x 20..62) where the aliens come from the far end -> exit door -> vestibule with
    /// the Level 2 trigger. Unlocking the locker (2 questions) drops the blaster on the floor and opens the
    /// armory door; picking the blaster up (E) starts the waves; clearing every wave opens the exit.
    /// </summary>
    public static class TransitSceneBuilder
    {
        public const string SCENE_PATH = "Assets/Starfall/Scenes/Transit.unity";
        private const string NAVMESH_PATH = "Assets/Starfall/Scenes/Transit_NavMesh.asset";
        private const string NEXT_SCENE = "Level2";

        private const float ARMORY_DOOR_X = 18f;
        private const float CORRIDOR_START_X = 20f;
        private const int CORRIDOR_SEGMENTS = 7;
        private const float CORRIDOR_Z = 6f;          // corridor floor spans z 6..12
        private const float DOOR_TOP_Z = 12f;         // door openings span z 6..12
        private const float CONSOLE_DEPTH = 1.5f;     // Wall_Console_01_* thickness
        private const float LEVEL_MUSIC_VOLUME = 0.35f;
        private const float PLAYER_HEALTH = 100f;
        private const float VIEW_MODEL_NEAR_CLIP = 0.05f;
        private static readonly int[] WINDOW_SEGMENTS = { 1, 4 };

        private static float CorridorEndX => CORRIDOR_START_X + CORRIDOR_SEGMENTS * SEGMENT;

        [MenuItem("Starfall/Build Transit Scene")]
        public static void Build()
        {
            CombatAssetBuilder.Build();
            StarfallHudBuilder.UpdateCombatHud();
            BuildSceneAt(SCENE_PATH, BuildContents);
            UpdateBuildSettings();
        }

        private static void BuildContents()
        {
            SpaceBackdropBuilder.SetupMaterials();   // skybox must exist before render settings use it
            ApplyRenderSettings();
            var env = Group("Environment");

            var lockerConsole = BuildArmory(Group("Armory", env));
            var armoryDoor = BuildDoor("ArmoryDoor", env, ARMORY_DOOR_X, DOOR_TOP_Z);
            var spawnPoints = BuildCorridor(Group("Corridor", env));
            var exitDoor = BuildDoor("ExitDoor", env, CorridorEndX, DOOR_TOP_Z);
            var exitLights = BuildExitArea(Group("ExitArea", env), out var exitTrigger);

            // Bake before the player exists so its CharacterController does not cut a hole in the NavMesh.
            BakeNavMesh(env, spawnPoints[0].position);

            var hud = InstantiateHud();
            var capsule = BuildPlayer(hud, new Vector3(13f, 0.1f, 9f), 270f, out var camera);
            var weapon = BuildCombatPlayer(capsule, camera);

            var spawner = new GameObject("WaveSpawner").AddComponent<WaveSpawner>();
            SetFields(spawner, so =>
            {
                so.FindProperty("alienPrefab").objectReferenceValue = CombatAssetBuilder.LoadAlienPrefab();
                SetObjectArray(so.FindProperty("spawnPoints"), spawnPoints.Cast<Object>().ToList());
            });

            var locker = BuildWeaponLocker(Group("Devices"), lockerConsole, armoryDoor, weapon, spawner);

            var manager = new GameObject("LevelManager").AddComponent<LevelManager>();
            SetFields(manager, so =>
            {
                SetObjectArray(so.FindProperty("devices"), new Object[] { locker });
                SetObjectArray(so.FindProperty("waveSpawners"), new Object[] { spawner });
                so.FindProperty("exitDoor").objectReferenceValue = exitDoor;
                so.FindProperty("nextSceneName").stringValue = NEXT_SCENE;
                SetObjectArray(so.FindProperty("activateOnComplete"), exitLights.Cast<Object>().ToList());
            });
            SetFields(exitTrigger, so => so.FindProperty("levelManager").objectReferenceValue = manager);

            BuildPostProcessing();
            MenuSceneBuilder.CreateMusic(LEVEL_MUSIC_VOLUME);
            new GameObject("SfxVolume").AddComponent<SfxVolumeApplier>();
            UiBuildUtils.CreateFader();

            var windowCenter = new Vector3(CORRIDOR_START_X + 3 * SEGMENT, 2f, 0f);
            SpaceBackdropBuilder.Build(null, windowCenter, Vector3.back);
        }

        // ---------- Armory ----------

        /// <summary>Kit room with the armory door opening; returns the weapon locker console.</summary>
        private static GameObject BuildArmory(Transform parent)
        {
            var room = Place("Simple_Room_01", parent, Vector3.zero, 0f);
            foreach (var light in room.GetComponentsInChildren<Light>(true)) light.gameObject.SetActive(false);
            FindChildAt(room, "Wall_Simple_05_Long", new Vector3(18f, 0f, 12f)).SetActive(false);   // armory door opening

            // The west wall's middle segment is a window frame; swap it for a solid wall so the locker has a backing.
            var westWall = FindChildAt(room, "Wall_Simple_05_Long", new Vector3(0f, 0f, 6f));
            westWall.SetActive(false);
            Place("Wall_Simple_01_Long", parent, westWall.transform.position, westWall.transform.eulerAngles.y, name: "LockerWall");

            // The locker stands in front of that wall, facing into the room.
            var lockerPosition = westWall.transform.position + westWall.transform.right * CONSOLE_DEPTH + Vector3.forward * 1.5f;
            var locker = Place("Wall_Console_01_Half", parent, lockerPosition, westWall.transform.eulerAngles.y,
                name: "WeaponLocker");

            PlaceCrate(parent, new Vector3(15f, 0f, 15.6f), 15f);
            PlaceCrate(parent, new Vector3(13.4f, 0f, 16.6f), 90f);
            PlaceCrate(parent, new Vector3(3f, 0f, 2.5f), 40f);
            Place("Sign_Danger_01", parent, new Vector3(0.05f, 2.6f, 4.2f), 0f, Vector3.one * 0.25f);

            EmergencyLight(parent, new Vector3(0f, 3.1f, 14f), 0f, 14f);
            EmergencyLight(parent, new Vector3(18f, 3.1f, 3.5f), 180f, 12f);
            return locker;
        }

        private static RepairableDevice BuildWeaponLocker(Transform devices, GameObject console, Door armoryDoor,
            Weapon weapon, WaveSpawner spawner)
        {
            console.transform.SetParent(devices, true);
            AddBoundsCollider(console);
            // Just in front of the console face (the console spans x 0..1.47 in front of the west wall).
            var lockerCenter = console.transform.position + new Vector3(0.05f, 2f, 1.6f);
            var malfunction = MalfunctionBuilder.Add(console.transform, lockerCenter + new Vector3(0f, 0.2f, 0f),
                Vector3.right, lockerCenter + new Vector3(0.1f, 1.25f, 0f), addFaultLight: true);
            var unlockedGlow = CreatePointLight("UnlockedGlow", console.transform, lockerCenter + Vector3.right * 0.8f,
                REPAIRED_GREEN, 1.5f, 4f);
            unlockedGlow.gameObject.SetActive(false);

            var device = console.AddComponent<RepairableDevice>();
            SetFields(device, so =>
            {
                so.FindProperty("deviceName").stringValue = "weapon locker";
                so.FindProperty("actionVerb").stringValue = "Unlock";
                SetObjectArray(so.FindProperty("questions"),
                    new Object[] { Question("TR_D1_Q1_Equality"), Question("TR_D1_Q2_WhileLoop") });
                so.FindProperty("doorToOpen").objectReferenceValue = armoryDoor;
                SetObjectArray(so.FindProperty("activateOnRepair"), new Object[] { unlockedGlow.gameObject });
                SetObjectArray(so.FindProperty("deactivateOnRepair"), new Object[] { malfunction });
            });
            // Unlocking throws the blaster out onto the floor; the waves start once the player picks it up.
            var pickup = CombatAssetBuilder.CreatePickup(weapon, devices, lockerCenter + new Vector3(0.45f, -0.5f, 0f),
                Quaternion.Euler(0f, 90f, 0f));
            UnityEventTools.AddPersistentListener(device.OnRepaired, pickup.Drop);
            UnityEventTools.AddPersistentListener(pickup.OnPickedUp, spawner.StartWaves);
            return device;
        }

        // ---------- Corridor ----------

        /// <summary>Corridor segments with portholes and cover crates; returns the alien spawn points at the far end.</summary>
        private static List<Transform> BuildCorridor(Transform parent)
        {
            for (int i = 0; i < CORRIDOR_SEGMENTS; i++)
            {
                float x = CORRIDOR_START_X + i * SEGMENT;
                var segment = Place("Corridor_Test_01", parent, new Vector3(x, 0f, CORRIDOR_Z), 0f, name: $"Corridor_{i + 1}");
                if (WINDOW_SEGMENTS.Contains(i))
                {
                    var wall = FindChildAt(segment, "Wall_Simple_01_Long", new Vector3(SEGMENT, 0f, 0f));
                    wall.SetActive(false);
                    Place("Porthole_01_Long", parent, wall.transform.position, wall.transform.eulerAngles.y);
                }
                Place("Light_Corridor_01", parent, new Vector3(x + SEGMENT / 2f, 4f, CORRIDOR_Z + SEGMENT / 2f), 90f);
            }

            // Cover along the walls; the middle stays free so the aliens can always reach the player.
            PlaceCrate(parent, new Vector3(27f, 0f, 11f), 90f);
            PlaceCrate(parent, new Vector3(38f, 0f, 7f), 90f);
            PlaceCrate(parent, new Vector3(38.1f, 0.75f, 7.1f), 75f);
            PlaceCrate(parent, new Vector3(49f, 0f, 11.1f), 20f);
            Place("Sign_Caution_01", parent, new Vector3(45f, 2.2f, 11.97f), 90f, Vector3.one * 0.25f);

            // Red alarm lights that pulse during the attack.
            for (int i = 0; i < 3; i++)
            {
                var alarm = CreatePointLight($"AlarmLight_{i + 1}", parent,
                    new Vector3(CORRIDOR_START_X + 9f + i * 14f, 3.5f, CORRIDOR_Z + SEGMENT / 2f), EMERGENCY_RED, 2.5f, 9f);
                alarm.gameObject.AddComponent<FlickeringLight>();
            }

            var spawns = Group("AlienSpawns", parent);
            var points = new List<Transform>();
            foreach (var z in new[] { 7.5f, 10.5f, 9f })
            {
                var point = Group($"Spawn_{points.Count + 1}", spawns);
                point.SetPositionAndRotation(new Vector3(CorridorEndX - 2.5f, 0f, z), Quaternion.Euler(0f, 270f, 0f));
                points.Add(point);
            }
            return points;
        }

        private static List<GameObject> BuildExitArea(Transform parent, out LevelExitTrigger trigger)
        {
            float vestibuleX = CorridorEndX;
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

        // ---------- NavMesh ----------

        private static void BakeNavMesh(Transform environment, Vector3 probeStart)
        {
            // Door leaves are ignored so the corridor and armory form one NavMesh; the doors open before aliens arrive.
            foreach (var door in environment.GetComponentsInChildren<Door>())
            {
                foreach (Transform leaf in door.transform)
                {
                    if (leaf.name.StartsWith("Leaf_")) leaf.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                }
            }

            var surface = new GameObject("NavMesh").AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            AssetDatabase.DeleteAsset(NAVMESH_PATH);
            AssetDatabase.CreateAsset(surface.navMeshData, NAVMESH_PATH);

            var path = new NavMeshPath();
            var armoryPoint = new Vector3(9f, 0f, 9f);
            bool found = NavMesh.SamplePosition(probeStart, out var from, 2f, NavMesh.AllAreas)
                         && NavMesh.SamplePosition(armoryPoint, out var to, 2f, NavMesh.AllAreas)
                         && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                         && path.status == NavMeshPathStatus.PathComplete;
            if (!found) throw new System.InvalidOperationException("Transit NavMesh: no path from the alien spawns to the armory.");
        }

        // ---------- Player combat ----------

        private static Weapon BuildCombatPlayer(GameObject capsule, Camera camera)
        {
            var health = capsule.AddComponent<Health>();
            SetFields(health, so => so.FindProperty("maxHealth").floatValue = PLAYER_HEALTH);

            var hurtSource = capsule.AddComponent<AudioSource>();
            hurtSource.playOnAwake = false;
            hurtSource.spatialBlend = 0f;
            hurtSource.volume = 0.8f;
            var death = capsule.AddComponent<PlayerDeath>();
            SetFields(death, so => so.FindProperty("hurtSource").objectReferenceValue = hurtSource);

            // The view model sits closer than the default 0.2 m near plane; Cinemachine owns the lens.
            var followCamera = capsule.transform.parent.Find("PlayerFollowCamera").GetComponent("CinemachineCamera");
            SetFields(followCamera, so => so.FindProperty("Lens.NearClipPlane").floatValue = VIEW_MODEL_NEAR_CLIP);

            var weapon = CombatAssetBuilder.CreateWeapon(camera);
            SetFields(capsule.GetComponent<PlayerControlLock>(),
                so => SetObjectArray(so.FindProperty("alsoDisable"), new Object[] { weapon }));
            return weapon;
        }
    }
}
