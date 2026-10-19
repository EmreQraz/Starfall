using UnityEngine;
using UnityEngine.UI;

namespace Starfall.UI
{
    /// <summary>
    /// Title screen: Start loads the intro, Settings opens the settings panel, Quit exits.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private string startSceneName = "Intro";
        [SerializeField] private GameObject buttonsRoot;
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private SettingsPanel settingsPanel;

        private void Awake()
        {
            startButton.onClick.AddListener(StartGame);
            settingsButton.onClick.AddListener(OpenSettings);
            quitButton.onClick.AddListener(Quit);
            settingsPanel.Closed += () => buttonsRoot.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void StartGame()
        {
            SetButtonsInteractable(false);
            ScreenFader.LoadScene(startSceneName);
        }

        private void OpenSettings()
        {
            buttonsRoot.SetActive(false);
            settingsPanel.Open();
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetButtonsInteractable(bool isInteractable)
        {
            startButton.interactable = isInteractable;
            settingsButton.interactable = isInteractable;
            quitButton.interactable = isInteractable;
        }
    }
}
