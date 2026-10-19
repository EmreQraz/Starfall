using Starfall.Questions;
using Starfall.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Builds the shared HUD prefab (crosshair, interaction prompt, question panel).
    /// Menu: Starfall > Build HUD Prefab. Re-running overwrites the prefab.
    /// </summary>
    public static class StarfallHudBuilder
    {
        private const string PREFAB_PATH = "Assets/Starfall/Prefabs/UI/StarfallHUD.prefab";
        private const string HUD_TEXTURE_PATH =
            "Assets/Creepy_Cat/3D Scifi Kit Starter Kit_HD/Textures/Others/HUD_Screen_01.png";

        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920, 1080);
        private static readonly Vector2 WINDOW_SIZE = new Vector2(1180, 820);
        private const float OPTION_HEIGHT = 74f;
        private const float BACKDROP_TEXTURE_ALPHA = 0.07f;

        private static readonly Color CYAN = new Color(0f, 0.9f, 1f, 1f);
        private static readonly Color ORANGE = new Color(1f, 0.62f, 0.1f, 1f);
        private static readonly Color NAVY = new Color(0.03f, 0.08f, 0.13f, 0.97f);
        private static readonly Color DIM = new Color(0f, 0.02f, 0.05f, 0.75f);
        private static readonly Color TEXT = new Color(0.85f, 0.95f, 1f, 1f);
        private static readonly Color OPTION_BG = new Color(0.05f, 0.16f, 0.22f, 0.9f);

        [MenuItem("Starfall/Build HUD Prefab")]
        public static void Build()
        {
            var canvasGo = new GameObject("StarfallHUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = REFERENCE_RESOLUTION;
                scaler.matchWidthOrHeight = 0.5f;

                BuildCrosshair(canvasGo.transform);
                BuildPrompt(canvasGo.transform);
                BuildQuestionPanel(canvasGo.transform);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PREFAB_PATH));
                PrefabUtility.SaveAsPrefabAsset(canvasGo, PREFAB_PATH, out bool success);
                if (success) Debug.Log($"Starfall HUD prefab saved to {PREFAB_PATH}");
                else Debug.LogError($"Failed to save HUD prefab to {PREFAB_PATH}");
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        private static void BuildCrosshair(Transform parent)
        {
            var dot = CreateImage("Crosshair", parent, new Color(1f, 1f, 1f, 0.6f));
            Center(dot.rectTransform, new Vector2(6, 6));
            dot.raycastTarget = false;
        }

        private static void BuildPrompt(Transform parent)
        {
            var holder = CreateRect("InteractionPrompt", parent);
            Stretch(holder);
            var promptUI = holder.gameObject.AddComponent<InteractionPromptUI>();

            var box = CreateImage("PromptRoot", holder, new Color(0.02f, 0.07f, 0.1f, 0.8f));
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.anchoredPosition = new Vector2(0, -90);
            box.rectTransform.sizeDelta = new Vector2(460, 54);
            box.raycastTarget = false;
            AddOutline(box.gameObject, CYAN);

            var label = CreateText("Label", box.transform, "[E] Repair panel", 28, CYAN, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            var so = new SerializedObject(promptUI);
            so.FindProperty("root").objectReferenceValue = box.gameObject;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildQuestionPanel(Transform parent)
        {
            var holder = CreateRect("QuestionPanel", parent);
            Stretch(holder);
            var panel = holder.gameObject.AddComponent<QuestionPanel>();

            // Full-screen dim layer; also blocks clicks behind the window.
            var rootImage = CreateImage("PanelRoot", holder, DIM);
            Stretch(rootImage.rectTransform);

            var window = CreateImage("Window", rootImage.transform, NAVY);
            Center(window.rectTransform, WINDOW_SIZE);
            AddOutline(window.gameObject, CYAN);

            var hudTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(HUD_TEXTURE_PATH);
            if (hudTexture != null)
            {
                var backdrop = new GameObject("HudBackdrop", typeof(RectTransform), typeof(RawImage));
                backdrop.transform.SetParent(window.transform, false);
                var raw = backdrop.GetComponent<RawImage>();
                raw.texture = hudTexture;
                raw.color = new Color(1f, 1f, 1f, BACKDROP_TEXTURE_ALPHA);
                raw.raycastTarget = false;
                Stretch(raw.rectTransform);
            }
            else
            {
                Debug.LogWarning($"HUD texture not found at {HUD_TEXTURE_PATH}; panel uses a plain background.");
            }

            var content = CreateRect("Content", window.transform);
            Stretch(content, 40);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Header: title on the left, progress on the right.
            var header = CreateRect("Header", content);
            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childControlWidth = headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            AddLayout(header.gameObject, 48);
            var title = CreateText("Title", header, "REPAIR: PANEL", 34, CYAN, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            var progress = CreateText("Progress", header, "Question 1 / 2", 26, ORANGE, TextAlignmentOptions.MidlineRight);

            var divider = CreateImage("Divider", content, new Color(CYAN.r, CYAN.g, CYAN.b, 0.5f));
            AddLayout(divider.gameObject, 2);

            var question = CreateText("QuestionText", content, "Question text", 30, TEXT, TextAlignmentOptions.TopLeft);
            AddLayout(question.gameObject, 190, flexible: true);

            var options = new QuestionPanelOption[QuestionData.OPTION_COUNT];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = CreateOption(content, i);
            }

            // Feedback: explanation + continue / try again.
            var feedback = CreateRect("Feedback", content);
            var feedbackLayout = feedback.gameObject.AddComponent<HorizontalLayoutGroup>();
            feedbackLayout.spacing = 20;
            feedbackLayout.childControlWidth = feedbackLayout.childControlHeight = true;
            feedbackLayout.childForceExpandWidth = false;
            AddLayout(feedback.gameObject, 120);

            var explanation = CreateText("Explanation", feedback, "Explanation", 24, TEXT, TextAlignmentOptions.MidlineLeft);
            AddLayout(explanation.gameObject, 120, flexibleWidth: true);

            var continueImage = CreateImage("ContinueButton", feedback, new Color(ORANGE.r, ORANGE.g, ORANGE.b, 0.9f));
            var continueButton = continueImage.gameObject.AddComponent<Button>();
            continueButton.targetGraphic = continueImage;
            var continueLayout = continueImage.gameObject.AddComponent<LayoutElement>();
            continueLayout.preferredWidth = 260;
            continueLayout.preferredHeight = 70;
            var continueLabel = CreateText("Label", continueImage.transform, "Continue", 28,
                new Color(0.05f, 0.05f, 0.08f, 1f), TextAlignmentOptions.Center);
            continueLabel.fontStyle = FontStyles.Bold;
            Stretch(continueLabel.rectTransform);

            var hint = CreateText("Hint", content, "Esc: close panel", 18, new Color(TEXT.r, TEXT.g, TEXT.b, 0.45f),
                TextAlignmentOptions.MidlineRight);
            AddLayout(hint.gameObject, 22);

            var so = new SerializedObject(panel);
            so.FindProperty("root").objectReferenceValue = rootImage.gameObject;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("progressText").objectReferenceValue = progress;
            so.FindProperty("questionText").objectReferenceValue = question;
            so.FindProperty("feedbackRoot").objectReferenceValue = feedback.gameObject;
            so.FindProperty("explanationText").objectReferenceValue = explanation;
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.FindProperty("continueLabel").objectReferenceValue = continueLabel;
            var optionsProp = so.FindProperty("optionButtons");
            optionsProp.arraySize = options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var element = optionsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = options[i].Button;
                element.FindPropertyRelative("label").objectReferenceValue = options[i].Label;
                element.FindPropertyRelative("background").objectReferenceValue = options[i].Background;
            }
            so.FindProperty("normalColor").colorValue = OPTION_BG;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private struct QuestionPanelOption
        {
            public Button Button;
            public TMP_Text Label;
            public Image Background;
        }

        private static QuestionPanelOption CreateOption(Transform parent, int index)
        {
            var background = CreateImage($"Option{(char)('A' + index)}", parent, OPTION_BG);
            AddLayout(background.gameObject, OPTION_HEIGHT);
            AddOutline(background.gameObject, new Color(CYAN.r, CYAN.g, CYAN.b, 0.35f));

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = Color.white; // keep correct/wrong tint readable after answering
            button.colors = colors;

            var label = CreateText("Label", background.transform, "Option", 27, TEXT, TextAlignmentOptions.MidlineLeft);
            Stretch(label.rectTransform, 22);

            return new QuestionPanelOption { Button = button, Label = label, Background = background };
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
            Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void AddOutline(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2, -2);
        }

        private static void AddLayout(GameObject go, float preferredHeight, bool flexible = false, bool flexibleWidth = false)
        {
            if (!go.TryGetComponent(out LayoutElement element)) element = go.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            if (flexible) element.flexibleHeight = 1;
            if (flexibleWidth) element.flexibleWidth = 1;
        }

        private static void Stretch(RectTransform rect, float padding = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }
}
