using UnityEngine;

namespace Starfall.Level
{
    /// <summary>
    /// Trigger volume behind the exit door. Loads the next level when the player walks in
    /// (only after every device is repaired).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LevelExitTrigger : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private string playerTag = "Player";

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake()
        {
            if (levelManager == null) levelManager = FindAnyObjectByType<LevelManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (levelManager != null && other.CompareTag(playerTag)) levelManager.LoadNextLevel();
        }
    }
}
