using System.Collections;
using Starfall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starfall.UI
{
    /// <summary>
    /// Full-screen black overlay: fades in when a scene starts and fades picture and sound out
    /// before loading the next one. Use ScreenFader.LoadScene instead of SceneManager.LoadScene.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        private const float DEFAULT_FADE_IN = 1.5f;
        private const float DEFAULT_FADE_OUT = 1.2f;

        [SerializeField] private float fadeInDuration = DEFAULT_FADE_IN;
        [SerializeField] private float fadeOutDuration = DEFAULT_FADE_OUT;

        private static ScreenFader instance;
        private CanvasGroup group;
        private bool isLoading;

        private void Awake()
        {
            instance = this;
            group = GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            AudioListener.volume = 0f;
        }

        private void Start()
        {
            StartCoroutine(Fade(1f, 0f, fadeInDuration));
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Fades out (if a fader exists in the scene) and loads the scene.</summary>
        public static void LoadScene(string sceneName)
        {
            if (instance == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
            if (!instance.isLoading) instance.StartCoroutine(instance.FadeOutAndLoad(sceneName));
        }

        private IEnumerator FadeOutAndLoad(string sceneName)
        {
            isLoading = true;
            group.blocksRaycasts = true;
            yield return Fade(group.alpha, 1f, fadeOutDuration);
            SceneManager.LoadScene(sceneName);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAmount(Mathf.SmoothStep(from, to, t / duration));
                yield return null;
            }
            SetAmount(to);
            group.blocksRaycasts = to > 0.5f;
        }

        private void SetAmount(float blackness)
        {
            group.alpha = blackness;
            AudioListener.volume = GameSettings.MasterVolume * (1f - blackness);
        }
    }
}
