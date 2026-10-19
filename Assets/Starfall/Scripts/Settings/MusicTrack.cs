using System.Collections;
using UnityEngine;

namespace Starfall.Settings
{
    /// <summary>
    /// Marks an AudioSource as music: its volume follows the Music setting and it can fade in/out.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicTrack : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float baseVolume = 0.5f;
        [SerializeField] private float fadeInDuration = 2f;

        private AudioSource source;
        private float fade;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            Refresh();
        }

        private void OnEnable() => GameSettings.Changed += Refresh;

        private void OnDisable() => GameSettings.Changed -= Refresh;

        private void Start()
        {
            if (!source.isPlaying) source.Play();
            FadeTo(1f, fadeInDuration);
        }

        public void FadeTo(float target, float duration)
        {
            StopAllCoroutines();
            StartCoroutine(Fade(target, duration));
        }

        private IEnumerator Fade(float target, float duration)
        {
            float start = fade;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                fade = Mathf.Lerp(start, target, t / duration);
                Refresh();
                yield return null;
            }
            fade = target;
            Refresh();
        }

        private void Refresh()
        {
            if (source != null) source.volume = baseVolume * GameSettings.MusicVolume * fade;
        }
    }
}
