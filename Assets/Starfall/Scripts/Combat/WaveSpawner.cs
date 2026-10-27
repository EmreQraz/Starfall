using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Starfall.Combat
{
    /// <summary>
    /// Spawns waves of aliens at the spawn points once StartWaves() is called (e.g. from the weapon
    /// locker's OnRepaired). The next wave starts when every alien of the current one is dead.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        private const float NAVMESH_SNAP_DISTANCE = 3f;
        private const float SPAWN_JITTER = 1.2f;

        [SerializeField] private AlienEnemy alienPrefab;
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private int[] waveSizes = { 3, 5, 7 };
        [SerializeField] private float firstWaveDelay = 2.5f;
        [SerializeField] private float timeBetweenWaves = 4f;
        [SerializeField] private float spawnInterval = 0.9f;
        [Tooltip("Health restored to the player after each cleared wave.")]
        [SerializeField] private float healOnWaveCleared = 30f;
        [SerializeField] private string playerTag = "Player";

        public UnityEvent OnAllWavesCleared = new UnityEvent();

        /// <summary>(wave number starting at 1, wave count)</summary>
        public event Action<int, int> WaveStarted;
        public event Action<int, int> WaveCleared;
        public event Action AllWavesCleared;

        public bool HasStarted { get; private set; }
        public bool IsCleared { get; private set; }
        public int AliveCount { get; private set; }
        public int WaveCount => waveSizes.Length;

        private Health player;

        private void Awake()
        {
            var playerGo = GameObject.FindWithTag(playerTag);
            if (playerGo != null) player = playerGo.GetComponentInParent<Health>();
            if (alienPrefab == null) Debug.LogError("WaveSpawner has no alien prefab.", this);
            if (spawnPoints.Length == 0) Debug.LogError("WaveSpawner has no spawn points.", this);
        }

        public void StartWaves()
        {
            if (HasStarted || alienPrefab == null || spawnPoints.Length == 0) return;
            HasStarted = true;
            StartCoroutine(RunWaves());
        }

        private bool PlayerIsDead => player != null && player.IsDead;

        private IEnumerator RunWaves()
        {
            yield return new WaitForSeconds(firstWaveDelay);

            for (int wave = 0; wave < waveSizes.Length; wave++)
            {
                WaveStarted?.Invoke(wave + 1, waveSizes.Length);
                for (int i = 0; i < waveSizes[wave]; i++)
                {
                    if (PlayerIsDead) yield break;
                    Spawn(spawnPoints[i % spawnPoints.Length]);
                    yield return new WaitForSeconds(spawnInterval);
                }

                yield return new WaitUntil(() => AliveCount == 0 || PlayerIsDead);
                if (PlayerIsDead) yield break;

                if (player != null) player.Heal(healOnWaveCleared);
                WaveCleared?.Invoke(wave + 1, waveSizes.Length);
                if (wave < waveSizes.Length - 1) yield return new WaitForSeconds(timeBetweenWaves);
            }

            IsCleared = true;
            AllWavesCleared?.Invoke();
            OnAllWavesCleared.Invoke();
        }

        private void Spawn(Transform point)
        {
            var jitter = UnityEngine.Random.insideUnitCircle * SPAWN_JITTER;
            var position = point.position + new Vector3(jitter.x, 0f, jitter.y);
            if (NavMesh.SamplePosition(position, out var navHit, NAVMESH_SNAP_DISTANCE, NavMesh.AllAreas))
            {
                position = navHit.position;
            }
            else
            {
                Debug.LogWarning($"Spawn point '{point.name}' is not near the NavMesh.", point);
            }

            var alien = Instantiate(alienPrefab, position, point.rotation);
            AliveCount++;
            alien.GetComponent<Health>().Died += () => AliveCount--;
        }
    }
}
