using System;
using System.Globalization;
using Starfall.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starfall.UI
{
    /// <summary>
    /// Volume, mouse sensitivity and fullscreen options. Values are saved when the panel closes.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text sfxValue;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private TMP_Text sensitivityValue;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Button backButton;

        public event Action Closed;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            sensitivitySlider.minValue = GameSettings.MIN_SENSITIVITY;
            sensitivitySlider.maxValue = GameSettings.MAX_SENSITIVITY;

            masterSlider.onValueChanged.AddListener(v => { GameSettings.MasterVolume = v; RefreshLabels(); });
            musicSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = v; RefreshLabels(); });
            sfxSlider.onValueChanged.AddListener(v => { GameSettings.SfxVolume = v; RefreshLabels(); });
            sensitivitySlider.onValueChanged.AddListener(v => { GameSettings.MouseSensitivity = v; RefreshLabels(); });
            fullscreenToggle.onValueChanged.AddListener(v => GameSettings.Fullscreen = v);
            backButton.onClick.AddListener(Close);

            root.SetActive(false);
        }

        public void Open()
        {
            masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            RefreshLabels();
            root.SetActive(true);
        }

        public void Close()
        {
            GameSettings.Save();
            root.SetActive(false);
            Closed?.Invoke();
        }

        private void RefreshLabels()
        {
            masterValue.text = Percent(masterSlider.value);
            musicValue.text = Percent(musicSlider.value);
            sfxValue.text = Percent(sfxSlider.value);
            sensitivityValue.text = "x" + sensitivitySlider.value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
