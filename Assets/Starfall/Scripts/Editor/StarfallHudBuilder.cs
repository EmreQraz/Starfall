using Starfall.Questions;
using Starfall.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Starfall.EditorTools.UiBuildUtils;
using static Starfall.EditorTools.SceneBuildUtils;

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

        private static readonly Vector2 WINDOW_SIZE = new Vector2(1180, 820);
        private const float OPTION_HEIGHT = 74f;
        private const float BACKDROP_TEXTURE_ALPHA = 0.07f;
        private static readonly Vector2 HEALTH_PANEL_SIZE = new Vector2(460, 96);
        private static readonly Vector2 AMMO_PANEL_SIZE = new Vector2(260, 110);
        private static readonly Vector2 BANNER_SIZE = new Vector2(900, 130);

        private static readonly Color DIM = new Color(0f, 0.02f, 0.05f, 0.75f);
        private static readonly Color OPTION_BG = PANEL_BG;

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
                BuildCombatHud(canvasGo.transform);
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

        /// <summary>
        /// Adds (or replaces) the combat HUD inside the existing prefab without rebuilding the rest,
        /// so scenes that reference objects inside the prefab keep their links.
        /// </summary>
        [MenuItem("Starfall/Update Combat HUD In Prefab")]
        public static void UpdateCombatHud()
        {
            var contents = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            try
            {
                var old = contents.transform.Find("CombatHud");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                BuildCombatHud(contents.transform);
                var crosshair = contents.transform.Find("Crosshair");
                int index = crosshair != null ? crosshair.GetSiblingIndex() + 1 : 0;
                contents.transform.Find("CombatHud").SetSiblingIndex(index);

                PrefabUtility.SaveAsPrefabAsset(contents, PREFAB_PATH, out bool success);
                if (!success) throw new System.InvalidOperationException($"Failed to save {PREFAB_PATH}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void BuildCrosshair(Transform parent)
        {
            var dot = CreateImage("Crosshair", parent, new Color(1f, 1f, 1f, 0.6f));
            Center(dot.rectTransform, new Vector2(6, 6));
            dot.raycastTarget = false;
        }

        /// <summary>Health bar (bottom left), ammo (bottom right), wave banner, damage flash and death screen.</summary>
        private static void BuildCombatHud(Transform parent)
        {
            var holder = CreateRect("CombatHud", parent);
            Stretch(holder);
            var hud = holder.gameObject.AddComponent<CombatHud>();

            var flash = CreateImage("DamageFlash", holder, new Color(0.8f, 0f, 0f, 0f));
            Stretch(flash.rectTransform);
            flash.raycastTarget = false;

            // Health: label, bar frame with a left-anchored fill, number.
            var healthRoot = CreateImage("Health", holder, PANEL_BG);
            Corner(healthRoot.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 40f), HEALTH_PANEL_SIZE);
            AddOutline(healthRoot.gameObject, new Color(CYAN.r, CYAN.g, CYAN.b, 0.5f));
            var label = CreateText("Label", healthRoot.transform, "VITALS", 22, CYAN, TextAlignmentOptions.TopLeft);
            label.characterSpacing = 6f;
            SetAnchors(label.rectTransform, new Vector2(0f, 0.5f), Vector2.one);
            label.rectTransform.offsetMin = new Vector2(16f, 0f);
            label.rectTransform.offsetMax = new Vector2(-16f, -8f);

            var track = CreateImage("Track", healthRoot.transform, new Color(0f, 0f, 0f, 0.6f));
            SetAnchors(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
            track.rectTransform.offsetMin = new Vector2(16f, 14f);
            track.rectTransform.offsetMax = new Vector2(-96f, -2f);
            var fill = CreateImage("Fill", track.transform, new Color(0.3f, 1f, 0.5f));
            SetAnchors(fill.rectTransform, Vector2.zero, Vector2.one);
            fill.raycastTarget = false;

            var number = CreateText("Value", healthRoot.transform, "100", 40, TEXT, TextAlignmentOptions.MidlineRight);
            number.fontStyle = FontStyles.Bold;
            SetAnchors(number.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0.62f));
            number.rectTransform.offsetMin = new Vector2(-90f, 4f);
            number.rectTransform.offsetMax = new Vector2(-16f, 0f);

            // Ammo
            var ammoRoot = CreateImage("Ammo", holder, PANEL_BG);
            Corner(ammoRoot.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 40f), AMMO_PANEL_SIZE);
            AddOutline(ammoRoot.gameObject, new Color(ORANGE.r, ORANGE.g, ORANGE.b, 0.6f));
            var ammoLabel = CreateText("Label", ammoRoot.transform, "BLASTER", 22, ORANGE, TextAlignmentOptions.TopLeft);
            ammoLabel.characterSpacing = 6f;
            Stretch(ammoLabel.rectTransform, 12);
            var ammoText = CreateText("Value", ammoRoot.transform, "16 / 16", 52, TEXT, TextAlignmentOptions.BottomRight);
            ammoText.fontStyle = FontStyles.Bold;
            Stretch(ammoText.rectTransform, 12);

            // Banner
            var bannerBox = CreateImage("Banner", holder, new Color(NAVY.r, NAVY.g, NAVY.b, 0.85f));
            bannerBox.rectTransform.anchorMin = bannerBox.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            bannerBox.rectTransform.pivot = new Vector2(0.5f, 1f);
            bannerBox.rectTransform.anchoredPosition = new Vector2(0f, -140f);
            bannerBox.rectTransform.sizeDelta = BANNER_SIZE;
            AddOutline(bannerBox.gameObject, CYAN);
            var bannerGroup = bannerBox.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            var bannerText = CreateText("Text", bannerBox.transform, "WAVE 1 / 3", 40, TEXT, TextAlignmentOptions.Center);
            bannerText.fontStyle = FontStyles.Bold;
            Stretch(bannerText.rectTransform, 10);

            // Death screen
            var death = CreateImage("DeathScreen", holder, new Color(0.15f, 0f, 0f, 0.85f));
            Stretch(death.rectTransform);
            var deathText = CreateText("Text", death.transform,
                "<color=#FF5C5C>SYSTEMS CRITICAL</color>\n<size=40%>Vital signs lost. Restarting sector...</size>",
                96, TEXT, TextAlignmentOptions.Center);
            deathText.fontStyle = FontStyles.Bold;
            Stretch(deathText.rectTransform);

            SetFields(hud, so =>
            {
                so.FindProperty("healthRoot").objectReferenceValue = healthRoot.gameObject;
                so.FindProperty("healthFill").objectReferenceValue = fill;
                so.FindProperty("healthText").objectReferenceValue = number;
                so.FindProperty("ammoRoot").objectReferenceValue = ammoRoot.gameObject;
                so.FindProperty("ammoText").objectReferenceValue = ammoText;
                so.FindProperty("banner").objectReferenceValue = bannerGroup;
                so.FindProperty("bannerText").objectReferenceValue = bannerText;
                so.FindProperty("damageFlash").objectReferenceValue = flash;
                so.FindProperty("deathScreen").objectReferenceValue = death.gameObject;
            });
        }

        private static void Corner(RectTransform rect, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
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
    }
}
