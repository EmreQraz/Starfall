using StarterAssets;
using UnityEngine;

namespace Starfall.Interaction
{
    /// <summary>
    /// Freezes the Starter Assets first person controller and frees the cursor while
    /// a UI (such as the question panel) is open. Put it on the PlayerCapsule.
    /// </summary>
    public class PlayerControlLock : MonoBehaviour
    {
        [SerializeField] private FirstPersonController controller;
        [SerializeField] private StarterAssetsInputs inputs;
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("Other player behaviours (e.g. the weapon) disabled while locked.")]
        [SerializeField] private Behaviour[] alsoDisable = System.Array.Empty<Behaviour>();

        public bool IsLocked { get; private set; }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<FirstPersonController>();
            if (inputs == null) inputs = GetComponent<StarterAssetsInputs>();
            if (interactor == null) interactor = GetComponentInChildren<PlayerInteractor>();
        }

        public void Lock()
        {
            IsLocked = true;
            SetControlsEnabled(false);
        }

        public void Unlock()
        {
            IsLocked = false;
            SetControlsEnabled(true);
        }

        private void SetControlsEnabled(bool isEnabled)
        {
            if (controller != null) controller.enabled = isEnabled;
            if (interactor != null) interactor.enabled = isEnabled;
            foreach (var behaviour in alsoDisable)
            {
                if (behaviour != null) behaviour.enabled = isEnabled;
            }

            if (inputs != null)
            {
                // Clear any held input so the player does not keep walking after the panel closes.
                inputs.MoveInput(Vector2.zero);
                inputs.LookInput(Vector2.zero);
                inputs.JumpInput(false);
                inputs.SprintInput(false);
                inputs.cursorLocked = isEnabled;
                inputs.cursorInputForLook = isEnabled;
            }

            Cursor.lockState = isEnabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isEnabled;
        }
    }
}
