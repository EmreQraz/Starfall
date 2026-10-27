using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Starfall.Combat
{
    /// <summary>
    /// First person blaster. Put it on the weapon view model under the camera; the GameObject starts
    /// inactive and Equip() hands it to the player. Hitscan: a ray from the camera damages the first
    /// Health it hits, and a short beam is drawn from the muzzle to the hit point.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        private const float DEFAULT_DAMAGE = 34f;
        private const float DEFAULT_RANGE = 60f;
        private const float DEFAULT_FIRE_INTERVAL = 0.18f;
        private const int DEFAULT_MAGAZINE = 16;
        private const float DEFAULT_RELOAD_TIME = 1.3f;
        private const float EQUIP_TIME = 0.45f;
        private const float BEAM_TIME = 0.05f;
        private const int IMPACT_PARTICLES = 14;

        [Header("Shooting")]
        [SerializeField] private Transform viewOrigin;
        [SerializeField] private float damage = DEFAULT_DAMAGE;
        [SerializeField] private float range = DEFAULT_RANGE;
        [SerializeField] private float fireInterval = DEFAULT_FIRE_INTERVAL;
        [SerializeField] private int magazineSize = DEFAULT_MAGAZINE;
        [SerializeField] private float reloadTime = DEFAULT_RELOAD_TIME;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Effects")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private Light muzzleFlash;
        [SerializeField] private LineRenderer beam;
        [SerializeField] private ParticleSystem impact;
        [SerializeField] private AudioSource audioSource;

        [Header("Motion")]
        [SerializeField] private Vector3 recoilKick = new Vector3(0f, 0.01f, -0.06f);
        [SerializeField] private float recoilPitch = 6f;
        [SerializeField] private float returnSpeed = 12f;
        [SerializeField] private Vector3 loweredOffset = new Vector3(0f, -0.35f, 0f);

        [Header("Input")]
        [SerializeField]
        private InputAction fireAction = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
        [SerializeField]
        private InputAction reloadAction = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");

        /// <summary>(rounds in magazine, magazine size)</summary>
        public event Action<int, int> AmmoChanged;
        public event Action<bool> ReloadingChanged;
        public event Action Equipped;

        public int Ammo { get; private set; }
        public int MagazineSize => magazineSize;
        public bool IsReloading { get; private set; }
        public bool IsEquipped { get; private set; }

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 offset;
        private float pitchOffset;
        private float nextShotTime;
        private float flashIntensity;

        private void Awake()
        {
            if (viewOrigin == null && Camera.main != null) viewOrigin = Camera.main.transform;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            Ammo = magazineSize;
            if (muzzleFlash != null)
            {
                flashIntensity = muzzleFlash.intensity;
                muzzleFlash.enabled = false;
            }
            if (beam != null) beam.enabled = false;
            if (impact != null) impact.transform.SetParent(null, true);   // impacts live in world space
        }

        private void OnEnable()
        {
            fireAction.Enable();
            reloadAction.Enable();
        }

        private void OnDisable()
        {
            fireAction.Disable();
            reloadAction.Disable();
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            if (beam != null) beam.enabled = false;
        }

        /// <summary>Shows the weapon with a short raise animation and enables shooting.</summary>
        public void Equip()
        {
            if (IsEquipped) return;

            IsEquipped = true;
            gameObject.SetActive(true);
            offset = loweredOffset;
            nextShotTime = Time.time + EQUIP_TIME;
            Equipped?.Invoke();
            AmmoChanged?.Invoke(Ammo, magazineSize);
        }

        private void Update()
        {
            if (fireAction.IsPressed()) TryFire();
            if (reloadAction.WasPressedThisFrame() && Ammo < magazineSize) StartReload();
            UpdateMotion();
        }

        private void TryFire()
        {
            if (IsReloading || Time.time < nextShotTime) return;
            if (Ammo <= 0)
            {
                StartReload();
                return;
            }

            nextShotTime = Time.time + fireInterval;
            Ammo--;
            AmmoChanged?.Invoke(Ammo, magazineSize);
            Shoot();
            if (Ammo == 0) StartReload();
        }

        private void Shoot()
        {
            var ray = new Ray(viewOrigin.position, viewOrigin.forward);
            Vector3 end = ray.GetPoint(range);
            if (Physics.Raycast(ray, out var hit, range, hitLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var target = hit.collider.GetComponentInParent<Health>();
                if (target != null) target.TakeDamage(damage, hit.point);
                SpawnImpact(hit.point, hit.normal);
            }

            if (audioSource != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(ProceduralSfx.BlasterShot);
            }
            offset += recoilKick;
            pitchOffset += recoilPitch;
            StartCoroutine(ShowShotEffects(end));
        }

        private void SpawnImpact(Vector3 point, Vector3 normal)
        {
            if (impact == null) return;
            impact.transform.SetPositionAndRotation(point + normal * 0.02f, Quaternion.LookRotation(normal));
            impact.Emit(IMPACT_PARTICLES);
        }

        private IEnumerator ShowShotEffects(Vector3 end)
        {
            if (muzzleFlash != null)
            {
                muzzleFlash.intensity = flashIntensity;
                muzzleFlash.enabled = true;
            }
            if (beam != null && muzzle != null)
            {
                beam.SetPosition(0, muzzle.position);
                beam.SetPosition(1, end);
                beam.enabled = true;
            }

            yield return new WaitForSeconds(BEAM_TIME);

            if (muzzleFlash != null) muzzleFlash.enabled = false;
            if (beam != null) beam.enabled = false;
        }

        private void StartReload()
        {
            if (IsReloading || !isActiveAndEnabled) return;
            StartCoroutine(Reload());
        }

        private IEnumerator Reload()
        {
            IsReloading = true;
            ReloadingChanged?.Invoke(true);
            if (audioSource != null) audioSource.PlayOneShot(ProceduralSfx.Reload);

            for (float t = 0f; t < reloadTime; t += Time.deltaTime)
            {
                // Dip the weapon down and back up during the reload.
                offset = loweredOffset * Mathf.Sin(t / reloadTime * Mathf.PI) * 0.6f;
                yield return null;
            }

            Ammo = magazineSize;
            IsReloading = false;
            ReloadingChanged?.Invoke(false);
            AmmoChanged?.Invoke(Ammo, magazineSize);
        }

        private void UpdateMotion()
        {
            float blend = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
            if (!IsReloading) offset = Vector3.Lerp(offset, Vector3.zero, blend);
            pitchOffset = Mathf.Lerp(pitchOffset, 0f, blend);

            transform.localPosition = restPosition + offset;
            transform.localRotation = restRotation * Quaternion.Euler(-pitchOffset, 0f, 0f);
        }
    }
}
