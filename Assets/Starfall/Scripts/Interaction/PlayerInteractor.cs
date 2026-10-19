using Starfall.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Starfall.Interaction
{
    /// <summary>
    /// Raycasts from the camera, shows the prompt of the interactable in view and
    /// calls Interact() when the interact key is pressed. Put it on the PlayerCapsule.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        private const float DEFAULT_RANGE = 2.5f;

        [SerializeField] private Transform viewOrigin;
        [SerializeField] private float range = DEFAULT_RANGE;
        [SerializeField] private LayerMask interactableLayers = ~0;
        [SerializeField] private InteractionPromptUI promptUI;

        [SerializeField]
        private InputAction interactAction = new InputAction(
            "Interact", InputActionType.Button, "<Keyboard>/e");

        private IInteractable current;

        private void Awake()
        {
            if (viewOrigin == null && Camera.main != null) viewOrigin = Camera.main.transform;
            if (promptUI == null) promptUI = FindAnyObjectByType<InteractionPromptUI>();
            if (interactAction.bindings.Count == 0) interactAction.AddBinding("<Keyboard>/e");
        }

        private void OnEnable()
        {
            interactAction.Enable();
        }

        private void OnDisable()
        {
            interactAction.Disable();
            SetCurrent(null);
        }

        private void Update()
        {
            SetCurrent(FindTarget());

            if (current != null && interactAction.WasPressedThisFrame())
            {
                current.Interact();
            }
        }

        private IInteractable FindTarget()
        {
            if (viewOrigin == null) return null;

            var ray = new Ray(viewOrigin.position, viewOrigin.forward);
            if (!Physics.Raycast(ray, out var hit, range, interactableLayers, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            return interactable != null && interactable.CanInteract ? interactable : null;
        }

        private void SetCurrent(IInteractable target)
        {
            current = target;
            if (promptUI == null) return;

            if (current == null)
            {
                promptUI.Hide();
                return;
            }

            string key = interactAction.GetBindingDisplayString();
            promptUI.Show($"[{key}] {current.Prompt}");
        }
    }
}
