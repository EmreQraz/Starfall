using System.Collections.Generic;
using UnityEngine;

namespace Starfall.Settings
{
    /// <summary>
    /// Scales every non-music AudioSource in the scene by the SFX volume setting.
    /// Put one in each gameplay scene.
    /// </summary>
    public class SfxVolumeApplier : MonoBehaviour
    {
        private readonly Dictionary<AudioSource, float> baseVolumes = new Dictionary<AudioSource, float>();

        private void OnEnable() => GameSettings.Changed += Apply;

        private void OnDisable() => GameSettings.Changed -= Apply;

        private void Start()
        {
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (!source.TryGetComponent<MusicTrack>(out _)) baseVolumes[source] = source.volume;
            }
            Apply();
        }

        private void Apply()
        {
            float volume = GameSettings.SfxVolume;
            foreach (var pair in baseVolumes)
            {
                if (pair.Key != null) pair.Key.volume = pair.Value * volume;
            }
        }
    }
}
