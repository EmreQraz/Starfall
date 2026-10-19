using System.Collections.Generic;
using System.Linq;
using Starfall.Devices;
using Starfall.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Starfall.Level
{
    /// <summary>
    /// Tracks every RepairableDevice in the level. When all are repaired it opens the exit door;
    /// a LevelExitTrigger behind that door then calls LoadNextLevel().
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Tooltip("Leave empty to track every RepairableDevice in the scene.")]
        [SerializeField] private List<RepairableDevice> devices = new List<RepairableDevice>();
        [SerializeField] private Door exitDoor;
        [Tooltip("Must be added to Build Settings.")]
        [SerializeField] private string nextSceneName;

        public UnityEvent OnAllDevicesRepaired = new UnityEvent();

        private bool isLoading;

        public bool AllRepaired { get; private set; }

        private void Awake()
        {
            if (devices.Count == 0)
            {
                devices = FindObjectsByType<RepairableDevice>().ToList();
            }
            if (devices.Count == 0)
            {
                Debug.LogWarning("LevelManager found no RepairableDevice; the exit opens immediately.", this);
            }
        }

        private void OnEnable()
        {
            foreach (var device in devices)
            {
                if (device != null) device.Repaired += HandleDeviceRepaired;
            }
        }

        private void OnDisable()
        {
            foreach (var device in devices)
            {
                if (device != null) device.Repaired -= HandleDeviceRepaired;
            }
        }

        private void Start()
        {
            CheckAllRepaired();
        }

        private void HandleDeviceRepaired(RepairableDevice device)
        {
            CheckAllRepaired();
        }

        private void CheckAllRepaired()
        {
            if (AllRepaired || devices.Any(d => d != null && !d.IsRepaired)) return;

            AllRepaired = true;
            if (exitDoor != null) exitDoor.Open();
            OnAllDevicesRepaired.Invoke();
        }

        public void LoadNextLevel()
        {
            if (!AllRepaired || isLoading) return;

            if (string.IsNullOrWhiteSpace(nextSceneName))
            {
                Debug.LogError("LevelManager.nextSceneName is empty.", this);
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                Debug.LogError($"Scene '{nextSceneName}' is not in Build Settings.", this);
                return;
            }

            isLoading = true;
            ScreenFader.LoadScene(nextSceneName);
        }
    }
}
