using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Small helpers shared by the scene builders: prefab lookup by name, placement,
    /// colliders and serialized-field assignment.
    /// </summary>
    public static class SceneBuildUtils
    {
        private static readonly string[] PREFAB_SEARCH_ROOTS =
        {
            "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD",
            "Assets/Starter Assets",
            "Assets/Starfall"
        };

        /// <summary>Finds a prefab by exact file name. Throws with a clear message if missing.</summary>
        public static GameObject LoadPrefab(string prefabName)
        {
            var path = AssetDatabase.FindAssets($"{prefabName} t:Prefab", PREFAB_SEARCH_ROOTS)
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == prefabName);
            if (path == null) throw new System.IO.FileNotFoundException($"Prefab '{prefabName}' not found in project.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static GameObject Place(string prefabName, Transform parent, Vector3 position, float yRotation,
            Vector3? scale = null, string name = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(prefabName), parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yRotation, 0f));
            if (scale.HasValue) go.transform.localScale = scale.Value;
            if (name != null) go.name = name;
            return go;
        }

        public static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Finds a direct child by name prefix and local position (used to swap kit wall segments).</summary>
        public static GameObject FindChildAt(GameObject root, string namePrefix, Vector3 localPosition)
        {
            const float TOLERANCE = 0.01f;
            foreach (Transform child in root.transform)
            {
                if (child.name.StartsWith(namePrefix) && (child.localPosition - localPosition).sqrMagnitude < TOLERANCE)
                {
                    return child.gameObject;
                }
            }
            throw new System.InvalidOperationException(
                $"No child '{namePrefix}*' at {localPosition} under '{root.name}'. Did the kit prefab change?");
        }

        /// <summary>Adds a BoxCollider on the root that wraps all child renderers.</summary>
        public static BoxCollider AddBoundsCollider(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;

            var toLocal = go.transform.worldToLocalMatrix;
            var bounds = new Bounds(toLocal.MultiplyPoint3x4(renderers[0].bounds.center), Vector3.zero);
            foreach (var r in renderers)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    bounds.Encapsulate(toLocal.MultiplyPoint3x4(corner));
                }
            }

            var box = go.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            return box;
        }

        public static List<GameObject> LightObjects(GameObject root)
        {
            return root.GetComponentsInChildren<Light>(true).Select(l => l.gameObject).ToList();
        }

        public static void SetActive(IEnumerable<GameObject> objects, bool isActive)
        {
            foreach (var go in objects) go.SetActive(isActive);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public static Light CreatePointLight(string name, Transform parent, Vector3 position, Color color,
            float intensity, float range)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        public static void SetFields(Object target, System.Action<SerializedObject> assign)
        {
            var so = new SerializedObject(target);
            assign(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetObjectArray(SerializedProperty property, IReadOnlyList<Object> values)
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        /// <summary>
        /// Builds a fresh scene in the background and saves it to 'path'. If that scene is open in the editor
        /// (without unsaved changes) it is replaced and stays open; unsaved changes abort the build.
        /// </summary>
        public static void BuildSceneAt(string path, System.Action buildContents)
        {
            var existing = SceneManager.GetSceneByPath(path);
            bool wasOpen = existing.IsValid() && existing.isLoaded;
            if (wasOpen && existing.isDirty)
            {
                throw new System.InvalidOperationException(
                    $"'{path}' is open with unsaved changes. Save or close it, then build again.");
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                buildContents();
            }
            catch
            {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
                throw;
            }

            if (wasOpen) EditorSceneManager.CloseScene(existing, true);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"Scene saved to {path}");

            if (wasOpen) return;   // keep the rebuilt scene open, like before
            if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
            EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>Puts the Starfall scenes first in Build Settings, in play order, and keeps any others after them.</summary>
        public static void UpdateBuildSettings()
        {
            var ordered = new[]
            {
                MenuSceneBuilder.MAIN_MENU_PATH,
                MenuSceneBuilder.INTRO_PATH,
                Level1SceneBuilder.SCENE_PATH,
                PlaceholderSceneBuilder.LEVEL2_PATH,
            }.Where(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null).ToArray();

            var others = EditorBuildSettings.scenes.Where(s => !ordered.Contains(s.path));
            EditorBuildSettings.scenes = ordered.Select(p => new EditorBuildSettingsScene(p, true)).Concat(others).ToArray();
            Debug.Log("Build Settings: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path)));
        }

        public static int EnsureLayer(string layerName)
        {
            const int FIRST_USER_LAYER = 8;
            const int LAYER_COUNT = 32;

            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = FIRST_USER_LAYER; i < LAYER_COUNT; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new System.InvalidOperationException($"No free layer slot for '{layerName}'.");
        }
    }
}
