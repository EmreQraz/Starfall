using UnityEngine;

namespace Starfall.Devices
{
    /// <summary>
    /// Makes a light flicker like a faulty bulb, with short random blackouts.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickeringLight : MonoBehaviour
    {
        [SerializeField] private float flickerSpeed = 18f;
        [SerializeField, Range(0f, 1f)] private float minIntensity = 0.25f;
        [SerializeField] private float blackoutChancePerSecond = 0.35f;
        [SerializeField] private Vector2 blackoutDuration = new Vector2(0.05f, 0.35f);

        private Light target;
        private float baseIntensity;
        private float blackoutUntil;
        private float noiseOffset;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            noiseOffset = Random.value * 100f;
        }

        private void OnDisable()
        {
            if (target != null) target.intensity = baseIntensity;
        }

        private void Update()
        {
            if (Random.value < blackoutChancePerSecond * Time.deltaTime)
            {
                blackoutUntil = Time.time + Random.Range(blackoutDuration.x, blackoutDuration.y);
            }

            if (Time.time < blackoutUntil)
            {
                target.intensity = 0f;
                return;
            }

            float noise = Mathf.PerlinNoise(noiseOffset, Time.time * flickerSpeed);
            target.intensity = baseIntensity * Mathf.Lerp(minIntensity, 1f, noise);
        }
    }
}
