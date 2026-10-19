using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Starfall.Devices
{
    /// <summary>
    /// Sliding door. The kit door prefabs have no animation, so each leaf (a Door_Left_01 /
    /// Door_Right_01 instance) is moved by its local open offset.
    /// </summary>
    public class Door : MonoBehaviour
    {
        private const float DEFAULT_DURATION = 1.2f;

        [Serializable]
        private class Leaf
        {
            public Transform transform;
            [Tooltip("Local-space offset of the leaf when fully open.")]
            public Vector3 openOffset;
            [NonSerialized] public Vector3 closedPosition;
        }

        [SerializeField] private Leaf[] leaves = Array.Empty<Leaf>();
        [SerializeField] private float duration = DEFAULT_DURATION;
        [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool startsOpen;
        [Tooltip("Played on open/close. If empty, the first AudioSource found on a leaf is used.")]
        [SerializeField] private AudioSource sound;

        public UnityEvent OnOpened = new UnityEvent();

        private Coroutine motion;
        private float openAmount;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            foreach (var leaf in leaves)
            {
                if (leaf.transform != null) leaf.closedPosition = leaf.transform.localPosition;
            }
            if (sound == null) sound = GetComponentInChildren<AudioSource>();

            if (startsOpen)
            {
                IsOpen = true;
                openAmount = 1f;
                ApplyAmount(openAmount);
            }
        }

        public void Open() => MoveTo(true);

        public void Close() => MoveTo(false);

        private void MoveTo(bool open)
        {
            if (IsOpen == open) return;

            IsOpen = open;
            if (sound != null) sound.Play();
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(Animate(open ? 1f : 0f));
        }

        private IEnumerator Animate(float target)
        {
            float speed = duration > 0f ? 1f / duration : float.MaxValue;
            while (!Mathf.Approximately(openAmount, target))
            {
                openAmount = Mathf.MoveTowards(openAmount, target, speed * Time.deltaTime);
                ApplyAmount(openAmount);
                yield return null;
            }

            motion = null;
            if (IsOpen) OnOpened.Invoke();
        }

        private void ApplyAmount(float amount)
        {
            float t = easing.Evaluate(amount);
            foreach (var leaf in leaves)
            {
                if (leaf.transform == null) continue;
                leaf.transform.localPosition = leaf.closedPosition + leaf.openOffset * t;
            }
        }
    }
}
