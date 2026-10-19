using TMPro;
using UnityEngine;

namespace Starfall.UI
{
    /// <summary>
    /// The small "[E] Repair panel" hint near the center of the screen.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text label;

        private void Awake()
        {
            Hide();
        }

        public void Show(string text)
        {
            if (label != null && label.text != text) label.text = text;
            if (root != null && !root.activeSelf) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }
    }
}
