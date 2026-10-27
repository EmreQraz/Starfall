using System;
using UnityEngine;

namespace Starfall.Combat
{
    /// <summary>
    /// Hit points shared by the player and the aliens. Weapons find it with GetComponentInParent.
    /// </summary>
    public class Health : MonoBehaviour
    {
        private const float DEFAULT_MAX = 100f;

        [SerializeField] private float maxHealth = DEFAULT_MAX;

        /// <summary>Amount of damage taken and the world point that was hit.</summary>
        public event Action<float, Vector3> Damaged;
        public event Action Died;
        /// <summary>Fired on every change with (current, max).</summary>
        public event Action<float, float> Changed;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsDead => Current <= 0f;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead || amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, maxHealth);
            Damaged?.Invoke(amount, hitPoint);
            if (IsDead) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }
    }
}
