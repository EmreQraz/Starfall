using System.Collections;
using Starfall.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starfall.UI
{
    /// <summary>
    /// Health bar, ammo counter, wave announcements, red damage flash and the death screen.
    /// Lives in the shared HUD prefab and hides itself in scenes where the player has no Health.
    /// </summary>
    public class CombatHud : MonoBehaviour
    {
        private const float LOW_HEALTH = 0.3f;
        private const float MID_HEALTH = 0.6f;
        private const float FLASH_FADE_SPEED = 2.5f;
        private const float MAX_FLASH_ALPHA = 0.45f;
        private const float BANNER_TIME = 2.2f;
        private const float BANNER_FADE = 0.4f;
        private const float LOW_PULSE_SPEED = 6f;

        [SerializeField] private GameObject healthRoot;
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject ammoRoot;
        [SerializeField] private TMP_Text ammoText;
        [SerializeField] private CanvasGroup banner;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private Image damageFlash;
        [SerializeField] private GameObject deathScreen;
        [SerializeField] private string playerTag = "Player";

        [SerializeField] private Color healthyColor = new Color(0.3f, 1f, 0.5f);
        [SerializeField] private Color hurtColor = new Color(1f, 0.62f, 0.1f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.2f, 0.15f);

        private Health player;
        private Weapon weapon;
        private WaveSpawner spawner;
        private Coroutine bannerRoutine;

        private void Awake()
        {
            var playerGo = GameObject.FindWithTag(playerTag);
            if (playerGo != null) player = playerGo.GetComponentInParent<Health>();
            weapon = FindAnyObjectByType<Weapon>(FindObjectsInactive.Include);
            spawner = FindAnyObjectByType<WaveSpawner>();

            healthRoot.SetActive(player != null);
            ammoRoot.SetActive(false);
            banner.alpha = 0f;
            deathScreen.SetActive(false);
            SetFlashAlpha(0f);
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.Changed += UpdateHealth;
                player.Damaged += HandleDamaged;
                player.Died += HandleDied;
            }
            if (weapon != null)
            {
                weapon.Equipped += HandleEquipped;
                weapon.AmmoChanged += UpdateAmmo;
                weapon.ReloadingChanged += HandleReloading;
            }
            if (spawner != null)
            {
                spawner.WaveStarted += HandleWaveStarted;
                spawner.WaveCleared += HandleWaveCleared;
                spawner.AllWavesCleared += HandleAllCleared;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.Changed -= UpdateHealth;
                player.Damaged -= HandleDamaged;
                player.Died -= HandleDied;
            }
            if (weapon != null)
            {
                weapon.Equipped -= HandleEquipped;
                weapon.AmmoChanged -= UpdateAmmo;
                weapon.ReloadingChanged -= HandleReloading;
            }
            if (spawner != null)
            {
                spawner.WaveStarted -= HandleWaveStarted;
                spawner.WaveCleared -= HandleWaveCleared;
                spawner.AllWavesCleared -= HandleAllCleared;
            }
        }

        private void Start()
        {
            if (player != null) UpdateHealth(player.Current, player.Max);
        }

        private void Update()
        {
            if (damageFlash.color.a > 0f)
            {
                SetFlashAlpha(Mathf.MoveTowards(damageFlash.color.a, 0f, FLASH_FADE_SPEED * Time.deltaTime));
            }
            if (player != null && !player.IsDead && player.Current / player.Max <= LOW_HEALTH)
            {
                // Pulse the bar while health is critical.
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * LOW_PULSE_SPEED));
                healthFill.color = new Color(criticalColor.r, criticalColor.g, criticalColor.b, pulse);
            }
        }

        private void UpdateHealth(float current, float max)
        {
            float ratio = max > 0f ? current / max : 0f;
            healthFill.rectTransform.anchorMax = new Vector2(ratio, 1f);   // the bar is anchored to the left edge
            healthFill.color = ratio <= LOW_HEALTH ? criticalColor : ratio <= MID_HEALTH ? hurtColor : healthyColor;
            healthText.text = Mathf.CeilToInt(current).ToString();
        }

        private void HandleDamaged(float amount, Vector3 point)
        {
            float strength = Mathf.Clamp01(amount / (player.Max * 0.25f));
            SetFlashAlpha(Mathf.Max(damageFlash.color.a, MAX_FLASH_ALPHA * Mathf.Lerp(0.5f, 1f, strength)));
        }

        private void HandleDied()
        {
            ammoRoot.SetActive(false);
            deathScreen.SetActive(true);
        }

        private void HandleEquipped()
        {
            ammoRoot.SetActive(true);
            ShowBanner("BLASTER ONLINE\n<size=60%>Left click: fire   R: reload</size>");
        }

        private void UpdateAmmo(int ammo, int magazine)
        {
            string color = ammo <= magazine / 4 ? "#FF5C5C" : "#FFFFFF";
            ammoText.text = $"<color={color}>{ammo}</color><size=60%> / {magazine}</size>";
        }

        private void HandleReloading(bool isReloading)
        {
            if (isReloading) ammoText.text = "<size=70%>RELOADING...</size>";
        }

        private void HandleWaveStarted(int wave, int total) =>
            ShowBanner($"<color=#FF5C5C>WARNING</color>\nHOSTILES INCOMING  -  WAVE {wave} / {total}");

        private void HandleWaveCleared(int wave, int total)
        {
            if (wave < total) ShowBanner("WAVE CLEARED\n<size=60%>Med-system: health restored</size>");
        }

        private void HandleAllCleared() => ShowBanner("AREA SECURE\n<size=60%>The exit door is open</size>");

        private void ShowBanner(string message)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(message));
        }

        private IEnumerator BannerRoutine(string message)
        {
            bannerText.text = message;
            yield return FadeBanner(1f);
            yield return new WaitForSeconds(BANNER_TIME);
            yield return FadeBanner(0f);
            bannerRoutine = null;
        }

        private IEnumerator FadeBanner(float target)
        {
            while (!Mathf.Approximately(banner.alpha, target))
            {
                banner.alpha = Mathf.MoveTowards(banner.alpha, target, Time.deltaTime / BANNER_FADE);
                yield return null;
            }
        }

        private void SetFlashAlpha(float alpha)
        {
            var c = damageFlash.color;
            damageFlash.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
