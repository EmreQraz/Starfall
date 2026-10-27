using System;
using System.Collections.Generic;
using Starfall.Interaction;
using Starfall.Questions;
using Starfall.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Starfall.Devices
{
    public enum DeviceState
    {
        Broken,
        Repaired
    }

    /// <summary>
    /// A broken device the player repairs by answering all of its questions correctly.
    /// Needs a (non-trigger) collider on itself or a child so the interactor can hit it.
    /// </summary>
    public class RepairableDevice : MonoBehaviour, IInteractable
    {
        [SerializeField] private string deviceName = "panel";
        [Tooltip("Verb shown in the prompt and panel title, e.g. \"Repair\" or \"Unlock\".")]
        [SerializeField] private string actionVerb = "Repair";
        [SerializeField] private List<QuestionData> questions = new List<QuestionData>();

        [Header("On repaired (optional)")]
        [SerializeField] private Door doorToOpen;
        [Tooltip("Objects (lights, screens, effects) switched on when repaired.")]
        [SerializeField] private GameObject[] activateOnRepair = Array.Empty<GameObject>();
        [Tooltip("Objects (warning lights, sparks) switched off when repaired.")]
        [SerializeField] private GameObject[] deactivateOnRepair = Array.Empty<GameObject>();

        [SerializeField] private QuestionPanel questionPanel;

        public UnityEvent OnRepaired = new UnityEvent();

        /// <summary>Code-side notification, used by LevelManager.</summary>
        public event Action<RepairableDevice> Repaired;

        public DeviceState State { get; private set; } = DeviceState.Broken;
        public bool IsRepaired => State == DeviceState.Repaired;
        public string DeviceName => deviceName;

        public string Prompt => $"{actionVerb} {deviceName}";
        public bool CanInteract => !IsRepaired && questionPanel != null && !questionPanel.IsOpen;

        private void Awake()
        {
            if (questionPanel == null) questionPanel = FindAnyObjectByType<QuestionPanel>();
            if (questionPanel == null)
            {
                Debug.LogError($"No QuestionPanel in the scene; '{deviceName}' cannot be repaired.", this);
            }
        }

        public void Interact()
        {
            if (!CanInteract) return;
            questionPanel.Open($"{actionVerb.ToUpperInvariant()}: {deviceName.ToUpperInvariant()}", questions, Repair);
        }

        private void Repair()
        {
            if (IsRepaired) return;
            State = DeviceState.Repaired;

            if (doorToOpen != null) doorToOpen.Open();
            SetAllActive(activateOnRepair, true);
            SetAllActive(deactivateOnRepair, false);

            OnRepaired.Invoke();
            Repaired?.Invoke(this);
        }

        private static void SetAllActive(GameObject[] objects, bool isActive)
        {
            foreach (var go in objects)
            {
                if (go != null) go.SetActive(isActive);
            }
        }
    }
}
