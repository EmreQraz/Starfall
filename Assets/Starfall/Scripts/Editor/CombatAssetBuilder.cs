using System.Collections.Generic;
using Starfall.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using static Starfall.EditorTools.SceneBuildUtils;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Combat assets: materials for the Blender-made alien and blaster (ArtSource/StarfallCombat.blend),
    /// the Alien prefab and the first person weapon view model. Menu: Starfall > Build Combat Assets.
    /// </summary>
    public static class CombatAssetBuilder
    {
        public const string ALIEN_PREFAB_PATH = "Assets/Starfall/Prefabs/Combat/Alien.prefab";

        private const string ART_ROOT = "Assets/Starfall/Art/Combat";
        private const string MODELS = ART_ROOT + "/Models/";
        private const string MATERIALS = ART_ROOT + "/Materials/";
        private const string LIT_SHADER = "Universal Render Pipeline/Lit";

        private const float ALIEN_HEIGHT = 1.7f;
        private const float ALIEN_RADIUS = 0.4f;
        private const float ALIEN_SPEED = 3.2f;
        private const float ALIEN_HEALTH = 100f;

        private static readonly Vector3 VIEW_MODEL_POSITION = new Vector3(0.1f, -0.08f, 0.24f);
        private static readonly Vector3 VIEW_MODEL_EULER = new Vector3(0f, -4f, 0f);   // barrel angled towards the crosshair
        private const float VIEW_MODEL_SCALE = 0.55f;   // the camera's narrow 40° FOV magnifies the full-size model
        private static readonly Vector3 MUZZLE_LOCAL = new Vector3(0f, 0.05f, 0.45f);   // barrel tip in model space
        private static readonly Color ALIEN_GLOW = new Color(0.4f, 1f, 0.2f);
        private static readonly Color BLASTER_CYAN = new Color(0f, 0.9f, 1f);

        [MenuItem("Starfall/Build Combat Assets")]
        public static void Build()
        {
            SetupMaterials();
            BuildAlienPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log($"Combat assets ready ({ALIEN_PREFAB_PATH}).");
        }

        private static void SetupMaterials()
        {
            System.IO.Directory.CreateDirectory(MATERIALS);
            var alien = new Dictionary<string, Material>
            {
                ["M_AlienSkin"] = LitMaterial("M_AlienSkin", new Color(0.2f, 0.27f, 0.14f), 0.45f, 0f),
                ["M_AlienCarapace"] = LitMaterial("M_AlienCarapace", new Color(0.16f, 0.07f, 0.22f), 0.7f, 0.2f),
                ["M_AlienGlow"] = Emissive(LitMaterial("M_AlienGlow", ALIEN_GLOW, 0.5f, 0f), ALIEN_GLOW * 6f),
            };
            var blaster = new Dictionary<string, Material>
            {
                ["M_GunBody"] = LitMaterial("M_GunBody", new Color(0.16f, 0.17f, 0.2f), 0.65f, 0.8f),
                ["M_GunAccent"] = LitMaterial("M_GunAccent", new Color(1f, 0.45f, 0.06f), 0.5f, 0.2f),
                ["M_GunGlow"] = Emissive(LitMaterial("M_GunGlow", BLASTER_CYAN, 0.6f, 0f), BLASTER_CYAN * 6f),
            };
            // Save first: reimporting the models reloads the remapped materials from disk, which would
            // drop unsaved edits such as the _EMISSION keyword.
            AssetDatabase.SaveAssets();
            RemapModelMaterials(MODELS + "SM_Alien.fbx", alien);
            RemapModelMaterials(MODELS + "SM_Blaster.fbx", blaster);
        }

        // ---------- Alien ----------

        private static void BuildAlienPrefab()
        {
            var root = new GameObject("Alien");
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.speed = ALIEN_SPEED;
                agent.acceleration = 14f;
                agent.angularSpeed = 480f;
                agent.radius = ALIEN_RADIUS;
                agent.height = ALIEN_HEIGHT;
                agent.stoppingDistance = 1.9f;   // the head and claws reach ~0.55 m past the capsule
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, ALIEN_HEIGHT / 2f, 0f);
                capsule.height = ALIEN_HEIGHT;
                capsule.radius = ALIEN_RADIUS;

                var health = root.AddComponent<Health>();
                SetFields(health, so => so.FindProperty("maxHealth").floatValue = ALIEN_HEALTH);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("SM_Alien"), root.transform);
                model.transform.localPosition = Vector3.zero;
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                BuildJointHierarchy(model.transform);
                var animator = AddAlienAnimator(root, model.transform);

                var burst = MalfunctionBuilder.CreateParticleSystem("DeathBurst", root.transform,
                    MalfunctionBuilder.AdditiveMaterial("M_AlienGoo", new Color(3f, 3f, 3f, 1f)));
                burst.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                ConfigureBurst(burst, ALIEN_GLOW, speed: new Vector2(1.5f, 5f), size: new Vector2(0.05f, 0.14f), gravity: 1.5f);

                var audio = CreateAudioSource(root, spatial: true);

                var enemy = root.AddComponent<AlienEnemy>();
                SetFields(enemy, so =>
                {
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.FindProperty("deathBurst").objectReferenceValue = burst;
                    so.FindProperty("audioSource").objectReferenceValue = audio;
                });

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ALIEN_PREFAB_PATH));
                PrefabUtility.SaveAsPrefabAsset(root, ALIEN_PREFAB_PATH, out bool success);
                if (!success) throw new System.InvalidOperationException($"Failed to save {ALIEN_PREFAB_PATH}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Child joint -> parent joint. The FBX holds the parts flat, each with its origin on its joint.</summary>
        private static readonly (string joint, string parent)[] ALIEN_JOINTS =
        {
            ("Torso", "Pelvis"), ("Head", "Torso"), ("Jaw", "Head"),
            ("UpperArm_L", "Torso"), ("ForeArm_L", "UpperArm_L"), ("UpperArm_R", "Torso"), ("ForeArm_R", "UpperArm_R"),
            ("Thigh_L", "Pelvis"), ("Shin_L", "Thigh_L"), ("Thigh_R", "Pelvis"), ("Shin_R", "Thigh_R"),
        };

        /// <summary>
        /// Parents the flat parts into a skeleton here instead of in Blender: the FBX exporter's axis baking
        /// only stays clean for top-level objects, and this keeps every joint's rest rotation at identity.
        /// </summary>
        private static void BuildJointHierarchy(Transform model)
        {
            foreach (var (joint, parent) in ALIEN_JOINTS)
            {
                FindJoint(model, joint).SetParent(FindJoint(model, parent), true);
            }
        }

        /// <summary>Wires the joints of the jointed Blender model (see ArtSource/StarfallCombat.blend) to AlienAnimator.</summary>
        private static AlienAnimator AddAlienAnimator(GameObject root, Transform model)
        {
            var animator = root.AddComponent<AlienAnimator>();
            SetFields(animator, so =>
            {
                foreach (var (field, joint) in new[]
                         {
                             ("pelvis", "Pelvis"), ("torso", "Torso"), ("head", "Head"), ("jaw", "Jaw"),
                             ("upperArmL", "UpperArm_L"), ("foreArmL", "ForeArm_L"),
                             ("upperArmR", "UpperArm_R"), ("foreArmR", "ForeArm_R"),
                             ("thighL", "Thigh_L"), ("shinL", "Shin_L"), ("thighR", "Thigh_R"), ("shinR", "Shin_R"),
                         })
                {
                    so.FindProperty(field).objectReferenceValue = FindJoint(model, joint);
                }
            });
            return animator;
        }

        private static Transform FindJoint(Transform root, string jointName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == jointName) return t;
            }
            throw new System.InvalidOperationException($"Alien model has no joint '{jointName}'. Re-export SM_Alien.fbx.");
        }

        public static AlienEnemy LoadAlienPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ALIEN_PREFAB_PATH);
            if (prefab == null) throw new System.IO.FileNotFoundException($"Alien prefab missing: {ALIEN_PREFAB_PATH}. Run Build Combat Assets.");
            return prefab.GetComponent<AlienEnemy>();
        }

        // ---------- Weapon ----------

        /// <summary>
        /// Builds the blaster view model under the player camera. It starts inactive; the returned
        /// Weapon's Equip() shows it.
        /// </summary>
        public static Weapon CreateWeapon(Camera camera)
        {
            var root = new GameObject("Blaster");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = VIEW_MODEL_POSITION;
            root.transform.localEulerAngles = VIEW_MODEL_EULER;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("SM_Blaster"), root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * VIEW_MODEL_SCALE;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            var muzzle = Group("Muzzle", root.transform);
            muzzle.localPosition = MUZZLE_LOCAL * VIEW_MODEL_SCALE;
            var flash = CreatePointLight("MuzzleFlash", muzzle, muzzle.position, BLASTER_CYAN, 4f, 5f);
            flash.transform.localPosition = Vector3.zero;

            var beam = CreateBeam(root.transform);

            var impact = MalfunctionBuilder.CreateParticleSystem("Impact", root.transform,
                MalfunctionBuilder.AdditiveMaterial("M_BlasterImpact", new Color(3f, 3f, 3f, 1f)));
            ConfigureBurst(impact, BLASTER_CYAN, speed: new Vector2(1f, 4f), size: new Vector2(0.02f, 0.06f), gravity: 0.6f);

            var audio = CreateAudioSource(root, spatial: false);

            var weapon = root.AddComponent<Weapon>();
            SetFields(weapon, so =>
            {
                so.FindProperty("viewOrigin").objectReferenceValue = camera.transform;
                so.FindProperty("muzzle").objectReferenceValue = muzzle;
                so.FindProperty("muzzleFlash").objectReferenceValue = flash;
                so.FindProperty("beam").objectReferenceValue = beam;
                so.FindProperty("impact").objectReferenceValue = impact;
                so.FindProperty("audioSource").objectReferenceValue = audio;
            });
            root.SetActive(false);
            return weapon;
        }

        /// <summary>
        /// The blaster as a world object that drops out of the locker and is picked up with E. Starts inactive.
        /// </summary>
        public static WeaponPickup CreatePickup(Weapon weapon, Transform parent, Vector3 position, Quaternion rotation)
        {
            const float COLLIDER_PADDING = 0.12f;   // a bigger target is easier to aim at on the floor

            var root = new GameObject("BlasterPickup");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("SM_Blaster"), root.transform);
            model.transform.localPosition = Vector3.zero;

            var box = AddBoundsCollider(root);
            box.size += Vector3.one * COLLIDER_PADDING;
            var body = root.AddComponent<Rigidbody>();
            body.mass = 2f;
            body.isKinematic = true;   // held in the locker until Drop()
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Soft and a little above the gun: right at floor level a brighter light blows out the floor through bloom.
            var glow = CreatePointLight("Glow", root.transform, position, BLASTER_CYAN, 0.35f, 1.8f);
            glow.transform.localPosition = Vector3.up * 0.4f;
            var audio = CreateAudioSource(root, spatial: true);

            var pickup = root.AddComponent<WeaponPickup>();
            SetFields(pickup, so =>
            {
                so.FindProperty("weapon").objectReferenceValue = weapon;
                so.FindProperty("audioSource").objectReferenceValue = audio;
            });
            root.SetActive(false);
            return pickup;
        }

        private static LineRenderer CreateBeam(Transform parent)
        {
            var go = new GameObject("Beam", typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthCurve = AnimationCurve.Linear(0f, 0.025f, 1f, 0.01f);
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = MalfunctionBuilder.AdditiveMaterial("M_BlasterBeam", new Color(0f, 3.5f, 4f, 1f));
            line.startColor = line.endColor = Color.white;
            return line;
        }

        // ---------- Shared helpers ----------

        private static void ConfigureBurst(ParticleSystem ps, Color color, Vector2 speed, Vector2 size, float gravity)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.rateOverTime = 0f;   // bursts come from code via Emit()
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = MalfunctionBuilder.FadeOut(Color.white, color);
        }

        private static AudioSource CreateAudioSource(GameObject go, bool spatial)
        {
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = spatial ? 1f : 0f;
            audio.volume = 0.7f;
            audio.minDistance = 2f;
            audio.maxDistance = 30f;
            return audio;
        }

        private static GameObject LoadModel(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MODELS + name + ".fbx");
            if (model == null) throw new System.IO.FileNotFoundException($"Model missing: {MODELS}{name}.fbx");
            return model;
        }

        private static Material LitMaterial(string name, Color color, float smoothness, float metallic)
        {
            string path = MATERIALS + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(LIT_SHADER));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
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
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var pair in materials)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            }
            importer.SaveAndReimport();
        }
    }
}
