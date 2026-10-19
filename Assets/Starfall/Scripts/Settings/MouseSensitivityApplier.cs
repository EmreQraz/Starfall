using StarterAssets;
using UnityEngine;

namespace Starfall.Settings
{
    /// <summary>
    /// Applies the mouse sensitivity setting to the Starter Assets controller. Put it on the PlayerCapsule.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class MouseSensitivityApplier : MonoBehaviour
    {
        private FirstPersonController controller;
        private float baseRotationSpeed;

        private void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            baseRotationSpeed = controller.RotationSpeed;
            Apply();
        }

        private void OnEnable() => GameSettings.Changed += Apply;

        private void OnDisable() => GameSettings.Changed -= Apply;

        private void Apply()
        {
            controller.RotationSpeed = baseRotationSpeed * GameSettings.MouseSensitivity;
        }
    }
}
