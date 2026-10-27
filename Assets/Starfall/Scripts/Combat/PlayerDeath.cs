using System.Collections;
using Starfall.Interaction;
using Starfall.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starfall.Combat
{
    /// <summary>
    /// Plays the hurt sound when the player is hit. On death it freezes the controls and,
    /// after a pause for the death screen, reloads the current scene. Put it on the PlayerCapsule.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDeath : MonoBehaviour
    {
        private const float DEFAULT_RESTART_DELAY = 2.5f;

        [SerializeField] private float restartDelay = DEFAULT_RESTART_DELAY;
        [SerializeField] private PlayerControlLock playerLock;
        [SerializeField] private AudioSource hurtSource;

        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (playerLock == null) playerLock = GetComponent<PlayerControlLock>();
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        private void HandleDamaged(float amount, Vector3 point)
        {
            if (hurtSource != null) hurtSource.PlayOneShot(ProceduralSfx.PlayerHurt);
        }

        private void HandleDied()
        {
            if (playerLock != null) playerLock.Lock();
            StartCoroutine(RestartAfterDelay());
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);
            ScreenFader.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
