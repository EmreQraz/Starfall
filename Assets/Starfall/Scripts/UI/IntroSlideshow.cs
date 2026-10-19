using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Starfall.UI
{
    /// <summary>
    /// Story slideshow: cross-fading images with a slow Ken Burns move and a typewriter caption.
    /// Space / Enter / click continues, Esc skips. Slides are edited in the Inspector.
    /// </summary>
    public class IntroSlideshow : MonoBehaviour
    {
        [Serializable]
        public class Slide
        {
            public Texture2D image;
            [TextArea(2, 4)] public string caption;
        }

        [SerializeField] private List<Slide> slides = new List<Slide>();
        [SerializeField] private string nextSceneName = "Level1";

        [Header("References")]
        [SerializeField] private RawImage imageA;
        [SerializeField] private RawImage imageB;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private CanvasGroup captionGroup;

        [Header("Timing")]
        [SerializeField] private float firstSlideDelay = 1.2f;
        [SerializeField] private float crossfadeDuration = 1.6f;
        [SerializeField] private float charactersPerSecond = 38f;
        [SerializeField] private float baseHoldTime = 2.2f;
        [SerializeField] private float holdTimePerCharacter = 0.035f;
        [SerializeField] private float captionFadeDuration = 0.5f;

        [Header("Input")]
        [SerializeField] private InputAction continueAction = new InputAction("Continue", InputActionType.Button);
        [SerializeField] private InputAction skipAction = new InputAction("Skip", InputActionType.Button, "<Keyboard>/escape");

        private RawImage front;
        private RawImage back;
        private bool continuePressed;
        private bool finished;

        private void Awake()
        {
            if (continueAction.bindings.Count == 0)
            {
                continueAction.AddBinding("<Keyboard>/space");
                continueAction.AddBinding("<Keyboard>/enter");
                continueAction.AddBinding("<Mouse>/leftButton");
                continueAction.AddBinding("<Gamepad>/buttonSouth");
            }
            front = imageA;
            back = imageB;
            SetAlpha(front, 0f);
            SetAlpha(back, 0f);
            captionGroup.alpha = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        private void OnEnable()
        {
            continueAction.performed += OnContinue;
            skipAction.performed += OnSkip;
            continueAction.Enable();
            skipAction.Enable();
        }

        private void OnDisable()
        {
            continueAction.performed -= OnContinue;
            skipAction.performed -= OnSkip;
            continueAction.Disable();
            skipAction.Disable();
        }

        private void Start()
        {
            if (slides.Count == 0)
            {
                Debug.LogWarning("IntroSlideshow has no slides; loading the next scene.", this);
                Finish();
                return;
            }
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(firstSlideDelay);
            for (int i = 0; i < slides.Count; i++)
            {
                yield return ShowImage(slides[i].image, i % 2 == 1);
                yield return TypeCaption(slides[i].caption);
                yield return WaitForReader(slides[i].caption.Length);
                yield return FadeCaption(1f, 0f);
            }
            Finish();
        }

        private IEnumerator ShowImage(Texture2D image, bool flipDrift)
        {
            (front, back) = (back, front);
            front.texture = image;
            front.transform.SetAsLastSibling();   // images share a container below the caption and letterbox
            if (front.TryGetComponent(out KenBurns kenBurns)) kenBurns.Restart(flipDrift);

            for (float t = 0f; t < crossfadeDuration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(front, Mathf.SmoothStep(0f, 1f, t / crossfadeDuration));
                yield return null;
            }
            SetAlpha(front, 1f);
            SetAlpha(back, 0f);
        }

        private IEnumerator TypeCaption(string text)
        {
            caption.text = text;
            caption.maxVisibleCharacters = 0;
            captionGroup.alpha = 1f;
            continuePressed = false;

            float visible = 0f;
            while (visible < text.Length && !continuePressed)
            {
                visible += charactersPerSecond * Time.unscaledDeltaTime;
                caption.maxVisibleCharacters = Mathf.FloorToInt(visible);
                yield return null;
            }
            caption.maxVisibleCharacters = text.Length;
            continuePressed = false;
        }

        private IEnumerator WaitForReader(int characterCount)
        {
            float hold = baseHoldTime + characterCount * holdTimePerCharacter;
            for (float t = 0f; t < hold && !continuePressed; t += Time.unscaledDeltaTime) yield return null;
            continuePressed = false;
        }

        private IEnumerator FadeCaption(float from, float to)
        {
            for (float t = 0f; t < captionFadeDuration; t += Time.unscaledDeltaTime)
            {
                captionGroup.alpha = Mathf.Lerp(from, to, t / captionFadeDuration);
                yield return null;
            }
            captionGroup.alpha = to;
        }

        private void OnContinue(InputAction.CallbackContext context) => continuePressed = true;

        private void OnSkip(InputAction.CallbackContext context) => Finish();

        private void Finish()
        {
            if (finished) return;
            finished = true;
            ScreenFader.LoadScene(nextSceneName);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
