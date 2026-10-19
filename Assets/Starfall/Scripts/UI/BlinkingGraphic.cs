using UnityEngine;

namespace Starfall.UI
{
    /// <summary>
    /// Pulses a CanvasGroup's alpha (warning labels, alerts).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BlinkingGraphic : MonoBehaviour
    {
        [SerializeField] private float speed = 3f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.2f;

        private CanvasGroup group;

        private void Awake() => group = GetComponent<CanvasGroup>();

        private void Update()
        {
            float wave = (Mathf.Sin(Time.time * speed * Mathf.PI) + 1f) * 0.5f;
            group.alpha = Mathf.Lerp(minAlpha, 1f, wave);
        }
    }
}
