using Starfall.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Starfall.EditorTools
{
    /// <summary>
    /// Shared uGUI construction helpers and the Starfall UI palette (taken from HUD_Screen_01).
    /// </summary>
    public static class UiBuildUtils
    {
        public static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920, 1080);
        public static readonly Color CYAN = new Color(0f, 0.9f, 1f, 1f);
        public static readonly Color ORANGE = new Color(1f, 0.62f, 0.1f, 1f);
        public static readonly Color NAVY = new Color(0.03f, 0.08f, 0.13f, 0.97f);
        public static readonly Color TEXT = new Color(0.85f, 0.95f, 1f, 1f);
        public static readonly Color PANEL_BG = new Color(0.05f, 0.16f, 0.22f, 0.9f);

        private const int FADER_SORTING_ORDER = 1000;

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = REFERENCE_RESOLUTION;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static ScreenFader CreateFader(Transform parent = null)
        {
            var canvas = CreateCanvas("ScreenFader", parent, FADER_SORTING_ORDER);
            var black = CreateImage("Black", canvas.transform, Color.black);
            Stretch(black.rectTransform);
            canvas.gameObject.AddComponent<CanvasGroup>();
            return canvas.gameObject.AddComponent<ScreenFader>();
        }

        public static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static RawImage CreateRawImage(string name, Transform parent, Texture texture, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size,
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

        public static Button CreateButton(string name, Transform parent, string label, Vector2 size)
        {
            var background = CreateImage(name, parent, PANEL_BG);
            background.rectTransform.sizeDelta = size;
            AddOutline(background.gameObject, new Color(CYAN.r, CYAN.g, CYAN.b, 0.6f));
            var element = background.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var text = CreateText("Label", background.transform, label, 30, TEXT, TextAlignmentOptions.Center);
            text.characterSpacing = 8f;
            Stretch(text.rectTransform);
            return button;
        }

        public static Slider CreateSlider(string name, Transform parent)
        {
            var root = CreateRect(name, parent);
            var slider = root.gameObject.AddComponent<Slider>();

            var track = CreateImage("Background", root, new Color(1f, 1f, 1f, 0.15f));
            SetAnchors(track.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            var fillArea = CreateRect("Fill Area", root);
            SetAnchors(fillArea, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            var fill = CreateImage("Fill", fillArea, CYAN);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = CreateRect("Handle Slide Area", root);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = CreateImage("Handle", handleArea, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        public static Toggle CreateToggle(string name, Transform parent)
        {
            const float BOX_SIZE = 40f;
            const float CHECK_PADDING = 8f;

            var box = CreateImage(name, parent, PANEL_BG);
            AddOutline(box.gameObject, CYAN);
            var element = box.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = BOX_SIZE;
            element.preferredHeight = BOX_SIZE;
            var check = CreateImage("Checkmark", box.transform, CYAN);
            Stretch(check.rectTransform, CHECK_PADDING);

            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            return toggle;
        }

        public static void AddOutline(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2, -2);
        }

        public static void AddLayout(GameObject go, float preferredHeight, bool flexible = false, bool flexibleWidth = false)
        {
            if (!go.TryGetComponent(out LayoutElement element)) element = go.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            if (flexible) element.flexibleHeight = 1;
            if (flexibleWidth) element.flexibleWidth = 1;
        }

        public static void Stretch(RectTransform rect, float padding = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }
}
