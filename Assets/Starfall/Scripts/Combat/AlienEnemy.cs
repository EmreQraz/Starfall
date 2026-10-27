using System.Collections;
using Starfall.Settings;
using UnityEngine;
using UnityEngine.AI;

namespace Starfall.Combat
{
    /// <summary>
    /// Alien behaviour: screeches on spawn, then hunts the player over the NavMesh. While far away it
    /// weaves from side to side and now and then dashes forward; each alien has its own speed. Close up
    /// it winds up a claw swipe and deals damage at the moment of the slash. AlienAnimator does the visuals.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class AlienEnemy : MonoBehaviour
    {
        private const float REPATH_INTERVAL = 0.2f;
        private const float SPAWN_SCREECH_TIME = 0.8f;
        private const float SLASH_DELAY = 0.22f;       // wind-up before the claws connect
        private const float DESTROY_DELAY = 3f;
        private const float TURN_SPEED = 7f;
        private const float WEAVE_MIN_DISTANCE = 6f;
        private const float NAVMESH_SNAP = 2f;
        private const int DEATH_PARTICLES = 40;

        [SerializeField] private float attackDamage = 12f;
        [SerializeField] private float attackRange = 2.4f;
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private string playerTag = "Player";

        [Header("Movement")]
        [SerializeField] private Vector2 speedRange = new Vector2(2.7f, 3.7f);
        [SerializeField] private float weaveAmplitude = 1.6f;
        [SerializeField] private float weaveFrequency = 0.7f;
        [SerializeField] private float dashMultiplier = 1.8f;
        [SerializeField] private float dashTime = 0.55f;
        [SerializeField] private Vector2 dashInterval = new Vector2(2.5f, 5f);

        [Header("References")]
        [SerializeField] private AlienAnimator animator;
        [SerializeField] private ParticleSystem deathBurst;
        [SerializeField] private AudioSource audioSource;

        private NavMeshAgent agent;
        private Health health;
        private Health target;
        private float baseSpeed;
        private float weaveSeed;
        private float nextRepath;
        private float nextAttack;
        private float nextDash;
        private float dashUntil;
        private float activeAt;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            agent.updateRotation = false;   // turned smoothly in Update instead of snapping
            baseSpeed = Random.Range(speedRange.x, speedRange.y);
            agent.speed = baseSpeed;
            weaveSeed = Random.value * 100f;
            if (audioSource != null) audioSource.volume *= GameSettings.SfxVolume;

            var player = GameObject.FindWithTag(playerTag);
            if (player != null) target = player.GetComponentInParent<Health>();
            if (target == null) Debug.LogWarning("AlienEnemy found no player Health; it will stand still.", this);
            if (animator != null && target != null) animator.LookTarget = target.transform;
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

        private void Start()
        {
            activeAt = Time.time + SPAWN_SCREECH_TIME;
            nextDash = Time.time + Random.Range(dashInterval.x, dashInterval.y);
            if (agent.isOnNavMesh) agent.isStopped = true;
            if (animator != null) animator.Screech();
            if (audioSource != null)
            {
                audioSource.pitch = Random.Range(0.85f, 1.15f);
                audioSource.PlayOneShot(ProceduralSfx.AlienScreech);
            }
        }

        private void Update()
        {
            if (health.IsDead || !agent.isOnNavMesh) return;

            bool hasTarget = target != null && !target.IsDead;
            agent.isStopped = !hasTarget || Time.time < activeAt;
            if (!hasTarget) return;

            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            UpdateSpeed(distance);
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + REPATH_INTERVAL;
                agent.SetDestination(ChooseDestination(toTarget, distance));
            }

            // Face where we are going, or the player when close.
            Vector3 facing = distance <= attackRange * 1.5f || agent.velocity.sqrMagnitude < 0.05f ? toTarget : agent.velocity;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing),
                    1f - Mathf.Exp(-TURN_SPEED * Time.deltaTime));
            }

            if (distance <= attackRange && Time.time >= nextAttack && Time.time >= activeAt) StartCoroutine(Attack());
        }

        private void UpdateSpeed(float distance)
        {
            bool canDash = distance > 4f && distance < 14f;
            if (canDash && Time.time >= nextDash)
            {
                dashUntil = Time.time + dashTime;
                nextDash = Time.time + Random.Range(dashInterval.x, dashInterval.y);
            }
            float wanted = Time.time < dashUntil ? baseSpeed * dashMultiplier : baseSpeed;
            agent.speed = Mathf.MoveTowards(agent.speed, wanted, 10f * Time.deltaTime);
        }

        /// <summary>The player's position, pushed sideways by a slow wave while still far away.</summary>
        private Vector3 ChooseDestination(Vector3 toTarget, float distance)
        {
            Vector3 goal = target.transform.position;
            if (distance < WEAVE_MIN_DISTANCE) return goal;

            Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized);
            float wave = Mathf.Sin((Time.time + weaveSeed) * weaveFrequency * Mathf.PI * 2f)
                         + 0.5f * Mathf.Sin((Time.time + weaveSeed) * weaveFrequency * 3.3f);
            float fade = Mathf.InverseLerp(WEAVE_MIN_DISTANCE, WEAVE_MIN_DISTANCE * 2f, distance);
            Vector3 aim = transform.position + toTarget.normalized * Mathf.Min(distance, 5f) + side * wave * weaveAmplitude * fade;
            return NavMesh.SamplePosition(aim, out var hit, NAVMESH_SNAP, NavMesh.AllAreas) ? hit.position : goal;
        }

        private IEnumerator Attack()
        {
            nextAttack = Time.time + attackCooldown;
            if (animator != null) animator.Attack();
            yield return new WaitForSeconds(SLASH_DELAY);

            if (health.IsDead || target == null) yield break;
            Vector3 offset = target.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude <= attackRange * 1.25f) target.TakeDamage(attackDamage, transform.position);
        }

        private void HandleDamaged(float amount, Vector3 point)
        {
            if (animator != null) animator.Hit();
            agent.velocity *= 0.3f;   // brief stagger
            if (audioSource != null) audioSource.PlayOneShot(ProceduralSfx.AlienHit);
        }

        private void HandleDied()
        {
            StopAllCoroutines();
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

            if (animator != null) animator.Die();
            if (deathBurst != null) deathBurst.Emit(DEATH_PARTICLES);
            if (audioSource != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(ProceduralSfx.AlienDeath);
            }
            StartCoroutine(SinkAndDestroy());
        }

        /// <summary>Lies on the floor for a moment, then sinks through it and is removed.</summary>
        private IEnumerator SinkAndDestroy()
        {
            const float LIE_TIME = 1.6f;
            const float SINK_SPEED = 0.6f;
            yield return new WaitForSeconds(LIE_TIME);
            for (float t = 0f; t < DESTROY_DELAY - LIE_TIME; t += Time.deltaTime)
            {
                transform.position += Vector3.down * SINK_SPEED * Time.deltaTime;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
