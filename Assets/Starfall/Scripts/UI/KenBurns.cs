using UnityEngine;

namespace Starfall.UI
{
    /// <summary>
    /// Slow cinematic zoom and drift on a UI image. Restart() begins a new move.
    /// </summary>
    public class KenBurns : MonoBehaviour
    {
        [SerializeField] private float duration = 12f;
        [SerializeField] private float zoom = 0.08f;
        [SerializeField] private Vector2 drift = new Vector2(30f, 12f);
        [SerializeField] private bool loop;

        private RectTransform rect;
        private float startTime;
        private float direction = 1f;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Restart();
        }

        public void Restart(bool flipDirection = false)
        {
            if (flipDirection) direction = -direction;
            startTime = Time.unscaledTime;
        }

        private void Update()
        {
            float t = (Time.unscaledTime - startTime) / duration;
            t = loop ? Mathf.PingPong(t, 1f) : Mathf.Clamp01(t);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            rect.localScale = Vector3.one * (1f + zoom * eased);
            rect.anchoredPosition = drift * (direction * (eased - 0.5f));
        }
    }
}
