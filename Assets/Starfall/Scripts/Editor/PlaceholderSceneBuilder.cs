using TMPro;
using UnityEditor;
using UnityEngine;
using static Starfall.EditorTools.UiBuildUtils;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Builds the "Level 2 — Coming soon" placeholder scene.
    /// </summary>
    public static class PlaceholderSceneBuilder
    {
        public const string LEVEL2_PATH = "Assets/Starfall/Scenes/Level2.unity";

        [MenuItem("Starfall/Build Level 2 Placeholder")]
        public static void BuildLevel2()
        {
            SceneBuildUtils.BuildSceneAt(LEVEL2_PATH, () =>
            {
                var camera = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.skybox = SpaceBackdropBuilder.SkyboxMaterial;

                var canvas = CreateCanvas("Canvas", null, 0);
                var text = CreateText("Title", canvas.transform, "LEVEL 2\n<size=40><color=#FF9F1A>Coming soon</color></size>",
                    72, CYAN, TextAlignmentOptions.Center);
                Stretch(text.rectTransform);
                CreateFader();
            });
        }
    }
}
