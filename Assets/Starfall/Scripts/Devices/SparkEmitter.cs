using UnityEngine;

namespace Starfall.Devices
{
    /// <summary>
    /// Random bursts of sparks with a light flash and an electric crackle (generated, no audio asset needed).
    /// Disable the GameObject to stop it, e.g. through RepairableDevice "Deactivate On Repair".
    /// </summary>
    public class SparkEmitter : MonoBehaviour
    {
        private const int CRACKLE_SAMPLE_RATE = 22050;
        private const float CRACKLE_LENGTH = 0.35f;

        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private Light flash;
        [SerializeField] private AudioSource crackleSource;
        [SerializeField] private Vector2 interval = new Vector2(0.6f, 2.2f);
        [SerializeField] private Vector2Int sparksPerBurst = new Vector2Int(20, 45);
        [SerializeField] private float flashIntensity = 4f;
        [SerializeField] private float flashFadeSpeed = 18f;

        private static AudioClip crackleClip;
        private float nextBurst;

        private void Awake()
        {
            if (crackleClip == null) crackleClip = CreateCrackleClip();
            if (crackleSource != null) crackleSource.clip = crackleClip;
            if (flash != null) flash.intensity = 0f;
        }

        private void OnEnable()
        {
            ScheduleNext();
        }

        private void OnDisable()
        {
            if (flash != null) flash.intensity = 0f;
        }

        private void Update()
        {
            if (flash != null) flash.intensity = Mathf.MoveTowards(flash.intensity, 0f, flashFadeSpeed * Time.deltaTime);
            if (Time.time < nextBurst) return;

            Burst();
            ScheduleNext();
        }

        private void Burst()
        {
            if (sparks != null) sparks.Emit(Random.Range(sparksPerBurst.x, sparksPerBurst.y + 1));
            if (flash != null) flash.intensity = flashIntensity;
            if (crackleSource != null)
            {
                crackleSource.pitch = Random.Range(0.85f, 1.25f);
                crackleSource.Play();
            }
        }

        private void ScheduleNext()
        {
            nextBurst = Time.time + Random.Range(interval.x, interval.y);
        }

        /// <summary>Short burst of decaying noise pops that sounds like an electric crackle.</summary>
        private static AudioClip CreateCrackleClip()
        {
            int length = Mathf.RoundToInt(CRACKLE_SAMPLE_RATE * CRACKLE_LENGTH);
            var samples = new float[length];
            var rng = new System.Random(7);
            float envelope = 1f;
            for (int i = 0; i < length; i++)
            {
                bool pop = rng.NextDouble() < 0.02;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                samples[i] = noise * envelope * (pop ? 1f : 0.25f);
                envelope *= 0.9997f;
            }
            var clip = AudioClip.Create("SparkCrackle", length, 1, CRACKLE_SAMPLE_RATE, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
