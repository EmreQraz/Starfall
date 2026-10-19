using Starfall.Settings;
using Starfall.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Starfall.EditorTools.UiBuildUtils;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Builds the MainMenu (title, Start / Settings / Quit) and Intro (story slideshow) scenes.
    /// Menu: Starfall > Build Menu And Intro Scenes.
    /// </summary>
    public static class MenuSceneBuilder
    {
        public const string MAIN_MENU_PATH = "Assets/Starfall/Scenes/MainMenu.unity";
        public const string INTRO_PATH = "Assets/Starfall/Scenes/Intro.unity";

        private const string INTRO_ART = "Assets/Starfall/Art/Intro/";
        private const string SHADE_GRADIENT_PATH = "Assets/Starfall/Art/UI/T_ShadeGradient.png";
        private const string MUSIC_CLIP = "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD/Sounds/Ambiant_Loop.wav";
        private const int INTRO_TEXTURE_MAX_SIZE = 4096;
        private const float IMAGE_BLEED = 80f;          // keeps image edges hidden while Ken Burns drifts
        private const float LETTERBOX_HEIGHT = 90f;

        // Story captions, one per slide (Intro_01.png ... Intro_06.png).
        private static readonly string[] CAPTIONS =
        {
            "Year 2189. The research station STARFALL orbits a lonely gas giant at the edge of charted space.",
            "Without warning, a meteor storm tears through the outer hull.",
            "Main power fails. Doors seal shut. The station's AI goes silent.",
            "In the dark, a single crew member wakes up: you, the station's youngest engineer.",
            "Every system that still runs speaks only in code, and only someone who understands it can bring it back.",
            "Wake up, Engineer. Starfall needs you.",
        };

        [MenuItem("Starfall/Build Menu And Intro Scenes")]
        public static void Build()
        {
            ConfigureIntroTextures();
            SceneBuildUtils.BuildSceneAt(MAIN_MENU_PATH, () => { CreateUiCamera(); BuildMainMenu(); });
            SceneBuildUtils.BuildSceneAt(INTRO_PATH, () => { CreateUiCamera(); BuildIntro(); });
            SceneBuildUtils.UpdateBuildSettings();
        }

        // ---------- Main menu ----------

        private static void BuildMainMenu()
        {
            var canvas = CreateCanvas("MenuCanvas", null, 0);
            CreateEventSystem();

            var background = CreateRawImage("Background", canvas.transform, IntroTexture(1), Color.white);
            Stretch(background.rectTransform, -IMAGE_BLEED);
            var kenBurns = background.gameObject.AddComponent<KenBurns>();
            SceneBuildUtils.SetFields(kenBurns, so =>
            {
                so.FindProperty("duration").floatValue = 40f;
                so.FindProperty("zoom").floatValue = 0.06f;
                so.FindProperty("loop").boolValue = true;
            });
            var shade = CreateRawImage("LeftShade", canvas.transform, ShadeGradient(), new Color(0f, 0.01f, 0.03f, 1f));
            SetAnchors(shade.rectTransform, Vector2.zero, new Vector2(0.65f, 1f));

            var column = CreateRect("Column", canvas.transform);
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(0.45f, 1f);
            column.offsetMin = new Vector2(150f, 160f);
            column.offsetMax = new Vector2(0f, -200f);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            var title = CreateText("Title", column, "STARFALL", 110, CYAN, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 12f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            AddLayout(title.gameObject, 140f);
            var subtitle = CreateText("Subtitle", column, "Repair the station. Learn to code.", 32, ORANGE, TextAlignmentOptions.Left);
            AddLayout(subtitle.gameObject, 60f);
            AddLayout(CreateRect("Spacer", column).gameObject, 40f);

            var buttons = CreateRect("Buttons", column);
            var buttonLayout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            buttonLayout.spacing = 16f;
            buttonLayout.childControlWidth = buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = buttonLayout.childForceExpandHeight = false;
            var buttonSize = new Vector2(380f, 72f);
            var start = CreateButton("StartButton", buttons, "START", buttonSize);
            var settings = CreateButton("SettingsButton", buttons, "SETTINGS", buttonSize);
            var quit = CreateButton("QuitButton", buttons, "QUIT", buttonSize);

            var version = CreateText("Version", canvas.transform, "v0.1", 20, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.BottomRight);
            Stretch(version.rectTransform, 24f);

            var settingsPanel = BuildSettingsPanel(canvas.transform);
            var menu = canvas.gameObject.AddComponent<MainMenu>();
            SceneBuildUtils.SetFields(menu, so =>
            {
                so.FindProperty("buttonsRoot").objectReferenceValue = buttons.gameObject;
                so.FindProperty("startButton").objectReferenceValue = start;
                so.FindProperty("settingsButton").objectReferenceValue = settings;
                so.FindProperty("quitButton").objectReferenceValue = quit;
                so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            });

            CreateMusic(0.6f);
            CreateFader();
        }

        private static SettingsPanel BuildSettingsPanel(Transform parent)
        {
            var holder = CreateRect("SettingsPanel", parent);
            Stretch(holder);
            var panel = holder.gameObject.AddComponent<SettingsPanel>();

            var dim = CreateImage("PanelRoot", holder, new Color(0f, 0.02f, 0.05f, 0.6f));
            Stretch(dim.rectTransform);
            var window = CreateImage("Window", dim.transform, NAVY);
            Center(window.rectTransform, new Vector2(820f, 640f));
            AddOutline(window.gameObject, CYAN);

            var content = CreateRect("Content", window.transform);
            Stretch(content, 48f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 22f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateText("Title", content, "SETTINGS", 40, CYAN, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 10f;
            AddLayout(title.gameObject, 60f);

            var master = SliderRow(content, "Master Volume", out var masterValue);
            var music = SliderRow(content, "Music Volume", out var musicValue);
            var sfx = SliderRow(content, "Effects Volume", out var sfxValue);
            var sensitivity = SliderRow(content, "Mouse Sensitivity", out var sensitivityValue);
            var fullscreen = ToggleRow(content, "Fullscreen");
            AddLayout(CreateRect("Spacer", content).gameObject, 10f, flexible: true);
            var back = CreateButton("BackButton", content, "BACK", new Vector2(260f, 64f));

            SceneBuildUtils.SetFields(panel, so =>
            {
                so.FindProperty("root").objectReferenceValue = dim.gameObject;
                so.FindProperty("masterSlider").objectReferenceValue = master;
                so.FindProperty("masterValue").objectReferenceValue = masterValue;
                so.FindProperty("musicSlider").objectReferenceValue = music;
                so.FindProperty("musicValue").objectReferenceValue = musicValue;
                so.FindProperty("sfxSlider").objectReferenceValue = sfx;
                so.FindProperty("sfxValue").objectReferenceValue = sfxValue;
                so.FindProperty("sensitivitySlider").objectReferenceValue = sensitivity;
                so.FindProperty("sensitivityValue").objectReferenceValue = sensitivityValue;
                so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreen;
                so.FindProperty("backButton").objectReferenceValue = back;
            });
            return panel;
        }

        private static RectTransform Row(Transform parent, string label)
        {
            var row = CreateRect(label.Replace(" ", "") + "Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            AddLayout(row.gameObject, 52f);

            var text = CreateText("Label", row, label, 28, TEXT, TextAlignmentOptions.MidlineLeft);
            text.gameObject.AddComponent<LayoutElement>().preferredWidth = 280f;
            return row;
        }

        private static Slider SliderRow(Transform parent, string label, out TMP_Text valueText)
        {
            var row = Row(parent, label);
            var slider = CreateSlider("Slider", row);
            var element = slider.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.preferredHeight = 40f;
            valueText = CreateText("Value", row, "80%", 26, ORANGE, TextAlignmentOptions.MidlineRight);
            valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;
            return slider;
        }

        private static Toggle ToggleRow(Transform parent, string label)
        {
            return CreateToggle("Toggle", Row(parent, label));
        }

        // ---------- Intro ----------

        private static void BuildIntro()
        {
            var canvas = CreateCanvas("IntroCanvas", null, 0);

            var images = CreateRect("Images", canvas.transform);
            Stretch(images);
            var imageA = IntroImage("Image_A", images);
            var imageB = IntroImage("Image_B", images);

            var top = CreateImage("Letterbox_Top", canvas.transform, Color.black);
            SetAnchors(top.rectTransform, new Vector2(0f, 1f), Vector2.one);
            top.rectTransform.offsetMin = new Vector2(0f, -LETTERBOX_HEIGHT);
            var bottom = CreateImage("Letterbox_Bottom", canvas.transform, Color.black);
            SetAnchors(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            bottom.rectTransform.offsetMax = new Vector2(0f, LETTERBOX_HEIGHT);

            var captionBox = CreateRect("CaptionBox", canvas.transform);
            SetAnchors(captionBox, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f));
            captionBox.offsetMin = new Vector2(0f, LETTERBOX_HEIGHT + 30f);
            captionBox.offsetMax = new Vector2(0f, LETTERBOX_HEIGHT + 190f);
            var captionGroup = captionBox.gameObject.AddComponent<CanvasGroup>();
            var caption = CreateText("Caption", captionBox, "", 40, new Color(0.95f, 0.97f, 1f), TextAlignmentOptions.Center);
            Stretch(caption.rectTransform);
            caption.characterSpacing = 2f;
            caption.outlineWidth = 0.15f;
            caption.outlineColor = new Color32(0, 0, 0, 200);

            var hint = CreateText("Hint", canvas.transform, "SPACE  continue     ESC  skip", 20,
                new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.MidlineRight);
            SetAnchors(hint.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            hint.rectTransform.offsetMin = new Vector2(0f, 0f);
            hint.rectTransform.offsetMax = new Vector2(-40f, LETTERBOX_HEIGHT);
            hint.characterSpacing = 6f;

            var slideshow = canvas.gameObject.AddComponent<IntroSlideshow>();
            SceneBuildUtils.SetFields(slideshow, so =>
            {
                var slides = so.FindProperty("slides");
                slides.arraySize = CAPTIONS.Length;
                for (int i = 0; i < CAPTIONS.Length; i++)
                {
                    var slide = slides.GetArrayElementAtIndex(i);
                    slide.FindPropertyRelative("image").objectReferenceValue = IntroTexture(i + 1);
                    slide.FindPropertyRelative("caption").stringValue = CAPTIONS[i];
                }
                so.FindProperty("nextSceneName").stringValue = "Level1";
                so.FindProperty("imageA").objectReferenceValue = imageA;
                so.FindProperty("imageB").objectReferenceValue = imageB;
                so.FindProperty("caption").objectReferenceValue = caption;
                so.FindProperty("captionGroup").objectReferenceValue = captionGroup;
            });

            CreateMusic(0.7f);
            CreateFader();
        }

        private static RawImage IntroImage(string name, Transform parent)
        {
            var image = CreateRawImage(name, parent, null, new Color(1f, 1f, 1f, 0f));
            Stretch(image.rectTransform, -IMAGE_BLEED);
            var kenBurns = image.gameObject.AddComponent<KenBurns>();
            SceneBuildUtils.SetFields(kenBurns, so =>
            {
                so.FindProperty("duration").floatValue = 11f;
                so.FindProperty("zoom").floatValue = 0.07f;
            });
            return image;
        }

        // ---------- Shared ----------

        private static void CreateUiCamera()
        {
            var camera = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        /// <summary>Background music track; also used by the level builders.</summary>
        public static void CreateMusic(float baseVolume)
        {
            var go = new GameObject("Music", typeof(AudioSource));
            var source = go.GetComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MUSIC_CLIP);
            if (source.clip == null) Debug.LogWarning($"Music clip missing: {MUSIC_CLIP}");
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var track = go.AddComponent<MusicTrack>();
            SceneBuildUtils.SetFields(track, so => so.FindProperty("baseVolume").floatValue = baseVolume);
        }

        private static Texture2D IntroTexture(int number)
        {
            string path = $"{INTRO_ART}Intro_{number:00}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new System.IO.FileNotFoundException($"Intro image missing: {path}");
            return texture;
        }

        /// <summary>Horizontal alpha ramp (opaque left, clear right) so the menu shade has no hard edge.</summary>
        private static Texture2D ShadeGradient()
        {
            const int WIDTH = 256;
            const float MAX_ALPHA = 0.85f;

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SHADE_GRADIENT_PATH);
            if (existing != null) return existing;

            var texture = new Texture2D(WIDTH, 1, TextureFormat.RGBA32, false);
            for (int x = 0; x < WIDTH; x++)
            {
                float t = x / (WIDTH - 1f);
                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, MAX_ALPHA * (1f - Mathf.SmoothStep(0f, 1f, t))));
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SHADE_GRADIENT_PATH));
            System.IO.File.WriteAllBytes(SHADE_GRADIENT_PATH, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(SHADE_GRADIENT_PATH);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SHADE_GRADIENT_PATH);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SHADE_GRADIENT_PATH);
        }

        private static void ConfigureIntroTextures()
        {
            for (int i = 1; i <= CAPTIONS.Length; i++)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath($"{INTRO_ART}Intro_{i:00}.png");
                if (importer == null) throw new System.IO.FileNotFoundException($"Intro image missing: Intro_{i:00}.png");
                if (importer.maxTextureSize == INTRO_TEXTURE_MAX_SIZE && !importer.mipmapEnabled) continue;

                importer.maxTextureSize = INTRO_TEXTURE_MAX_SIZE;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }
    }
}
