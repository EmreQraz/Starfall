using UnityEngine;

namespace Starfall.Combat
{
    /// <summary>
    /// Combat sounds generated in code (like the spark crackle), so no audio assets are needed.
    /// Each clip is created once and shared.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SAMPLE_RATE = 22050;

        private static AudioClip blasterShot;
        private static AudioClip alienScreech;
        private static AudioClip alienHit;
        private static AudioClip alienDeath;
        private static AudioClip playerHurt;
        private static AudioClip reload;
        private static AudioClip clank;

        public static AudioClip BlasterShot => blasterShot ??= Create("BlasterShot", 0.22f, BlasterSample);
        public static AudioClip AlienScreech => alienScreech ??= Create("AlienScreech", 0.7f, ScreechSample);
        public static AudioClip AlienHit => alienHit ??= Create("AlienHit", 0.12f, HitSample);
        public static AudioClip AlienDeath => alienDeath ??= Create("AlienDeath", 0.9f, DeathSample);
        public static AudioClip PlayerHurt => playerHurt ??= Create("PlayerHurt", 0.25f, HurtSample);
        public static AudioClip Reload => reload ??= Create("Reload", 0.5f, ReloadSample);
        public static AudioClip Clank => clank ??= Create("Clank", 0.35f, ClankSample);

        private delegate float SampleFunc(float t, float progress, System.Random rng);

        private static AudioClip Create(string name, float length, SampleFunc sample)
        {
            int count = Mathf.RoundToInt(SAMPLE_RATE * length);
            var data = new float[count];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                data[i] = Mathf.Clamp(sample(t, (float)i / count, rng), -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        private static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;

        /// <summary>Falling "pew": a pitch sweep from 1800 Hz down to 200 Hz with a noisy attack.</summary>
        private static float BlasterSample(float t, float p, System.Random rng)
        {
            float freq = Mathf.Lerp(1800f, 200f, Mathf.Sqrt(p));
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + Square(freq * 0.5f * t) * 0.15f;
            float attack = p < 0.08f ? Noise(rng) * 0.5f : 0f;
            return (tone + attack) * Mathf.Pow(1f - p, 2f) * 0.8f;
        }

        /// <summary>Wobbling, rising-then-falling screech with grit.</summary>
        private static float ScreechSample(float t, float p, System.Random rng)
        {
            float freq = 420f + Mathf.Sin(p * Mathf.PI) * 380f + Mathf.Sin(t * 2f * Mathf.PI * 22f) * 60f;
            float tone = Square(freq * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.25f;
            float envelope = Mathf.Sin(p * Mathf.PI);
            return (tone + Noise(rng) * 0.15f) * envelope * 0.6f;
        }

        private static float HitSample(float t, float p, System.Random rng)
        {
            float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(260f, 90f, p) * t);
            return (thump * 0.7f + Noise(rng) * 0.4f) * (1f - p) * 0.8f;
        }

        /// <summary>Long descending gurgle that fades into noise.</summary>
        private static float DeathSample(float t, float p, System.Random rng)
        {
            float freq = Mathf.Lerp(520f, 60f, p) + Mathf.Sin(t * 2f * Mathf.PI * 14f) * 40f;
            float tone = Square(freq * t) * (1f - p) * 0.35f;
            return (tone + Noise(rng) * 0.25f * p) * (1f - p) * 0.8f;
        }

        private static float HurtSample(float t, float p, System.Random rng)
        {
            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 70f, p) * t);
            return (tone * 0.8f + Noise(rng) * 0.3f) * Mathf.Pow(1f - p, 1.5f);
        }

        /// <summary>Metal hitting the floor: a few inharmonic ringing partials over a noisy knock.</summary>
        private static float ClankSample(float t, float p, System.Random rng)
        {
            float ring = Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.4f
                         + Mathf.Sin(2f * Mathf.PI * 1310f * t) * 0.3f
                         + Mathf.Sin(2f * Mathf.PI * 2270f * t) * 0.2f;
            float knock = p < 0.05f ? Noise(rng) * 0.8f : 0f;
            return (ring * Mathf.Exp(-p * 9f) + knock) * 0.8f;
        }

        /// <summary>Two mechanical clicks with a short servo whine between them.</summary>
        private static float ReloadSample(float t, float p, System.Random rng)
        {
            float click = (p < 0.04f || (p > 0.78f && p < 0.83f)) ? Noise(rng) * 0.9f : 0f;
            float whine = p > 0.15f && p < 0.7f ? Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(600f, 900f, p) * t) * 0.12f : 0f;
            return click + whine;
        }
    }
}
