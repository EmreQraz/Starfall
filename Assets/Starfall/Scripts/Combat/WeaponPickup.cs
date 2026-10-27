using Starfall.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace Starfall.Combat
{
    /// <summary>
    /// The blaster lying in the world. It starts hidden; Drop() lets it fall out of the weapon locker with
    /// physics, and the player picks it up with the interact key, which equips the first person Weapon.
    /// Needs a non-trigger collider and a Rigidbody (kinematic until dropped).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WeaponPickup : MonoBehaviour, IInteractable
    {
        private const float MIN_CLANK_SPEED = 0.6f;

        [SerializeField] private Weapon weapon;
        [Tooltip("Local space: +Z is the way the barrel points.")]
        [SerializeField] private Vector3 dropVelocity = new Vector3(0f, 1f, 1.6f);
        [SerializeField] private Vector3 dropSpin = new Vector3(3f, 5f, 0f);
        [SerializeField] private AudioSource audioSource;

        public UnityEvent OnPickedUp = new UnityEvent();

        private Rigidbody body;
        private bool isDropped;

        public string Prompt => "Pick up blaster";
        public bool CanInteract => isDropped && weapon != null;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (weapon == null) weapon = FindAnyObjectByType<Weapon>(FindObjectsInactive.Include);
            if (weapon == null) Debug.LogError("WeaponPickup has no Weapon to equip.", this);
        }

        /// <summary>Shows the pickup and throws it out of the locker.</summary>
        public void Drop()
        {
            if (isDropped) return;
            isDropped = true;
            gameObject.SetActive(true);
            body.isKinematic = false;
            body.linearVelocity = transform.TransformDirection(dropVelocity);
            body.angularVelocity = dropSpin;
        }

        public void Interact()
        {
            if (!CanInteract) return;
            weapon.Equip();
            OnPickedUp.Invoke();
            Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (audioSource == null || collision.relativeVelocity.magnitude < MIN_CLANK_SPEED) return;
            audioSource.pitch = Random.Range(0.9f, 1.15f);
            audioSource.PlayOneShot(ProceduralSfx.Clank, Mathf.Clamp01(collision.relativeVelocity.magnitude / 4f));
        }
    }
}
