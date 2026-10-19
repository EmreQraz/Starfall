using System;
using UnityEngine;

namespace Starfall.Settings
{
    /// <summary>
    /// Player options stored in PlayerPrefs. Listeners (audio, mouse look) subscribe to Changed.
    /// </summary>
    public static class GameSettings
    {
        public const float DEFAULT_VOLUME = 0.8f;
        public const float DEFAULT_SENSITIVITY = 1f;
        public const float MIN_SENSITIVITY = 0.2f;
        public const float MAX_SENSITIVITY = 3f;

        private const string MASTER_KEY = "starfall.volume.master";
        private const string MUSIC_KEY = "starfall.volume.music";
        private const string SFX_KEY = "starfall.volume.sfx";
        private const string SENSITIVITY_KEY = "starfall.mouse.sensitivity";

        public static event Action Changed;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MASTER_KEY, DEFAULT_VOLUME);
            set => SetFloat(MASTER_KEY, Mathf.Clamp01(value));
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MUSIC_KEY, DEFAULT_VOLUME);
            set => SetFloat(MUSIC_KEY, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SFX_KEY, DEFAULT_VOLUME);
            set => SetFloat(SFX_KEY, Mathf.Clamp01(value));
        }

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(SENSITIVITY_KEY, DEFAULT_SENSITIVITY);
            set => SetFloat(SENSITIVITY_KEY, Mathf.Clamp(value, MIN_SENSITIVITY, MAX_SENSITIVITY));
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set
            {
                Screen.fullScreen = value;
                Changed?.Invoke();
            }
        }

        public static void Save() => PlayerPrefs.Save();

        private static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            AudioListener.volume = MasterVolume;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
            AudioListener.volume = MasterVolume;
        }
    }
}
