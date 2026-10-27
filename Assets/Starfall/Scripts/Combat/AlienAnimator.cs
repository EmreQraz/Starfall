using UnityEngine;

namespace Starfall.Combat
{
    /// <summary>
    /// Procedural animation for the jointed alien model (no rig or clips). The walk cycle is driven by
    /// the distance actually travelled so the feet do not slide; on top of it the body bobs, leans into
    /// speed and turns, the head tracks the player with lag and random twitches, and attack, hit and
    /// death poses are blended in. Signs follow Unity's axes: +X pitch tips a limb's tip backwards/down.
    /// </summary>
    public class AlienAnimator : MonoBehaviour
    {
        private const float STRIDE_LENGTH = 1.1f;      // metres per full walk cycle
        private const float ATTACK_TIME = 0.45f;
        private const float SCREECH_TIME = 0.7f;
        private const float DEATH_TIME = 0.7f;
        private const float HIT_RECOVERY = 7f;
        private const float POSE_SHARPNESS = 12f;
        private const string BASE_COLOR = "_BaseColor";

        [Header("Joints")]
        [SerializeField] private Transform pelvis;
        [SerializeField] private Transform torso;
        [SerializeField] private Transform head;
        [SerializeField] private Transform jaw;
        [SerializeField] private Transform upperArmL, foreArmL, upperArmR, foreArmR;
        [SerializeField] private Transform thighL, shinL, thighR, shinR;

        [Header("Motion")]
        [SerializeField] private float runSpeed = 3.5f;
        [SerializeField] private float legSwing = 38f;
        [SerializeField] private float kneeBend = 55f;
        [SerializeField] private float armSwing = 24f;
        [SerializeField] private float bobHeight = 0.07f;
        [SerializeField] private float maxLean = 24f;
        [SerializeField] private float turnLean = 0.08f;   // degrees of roll per degree/second of turning
        [SerializeField] private float headLookSpeed = 6f;
        [SerializeField] private Color hitFlashColor = Color.white;

        public Transform LookTarget { get; set; }

        private Vector3 pelvisRest;
        private Vector3 lastPosition;
        private float lastYaw;
        private float phase;
        private float speed01;
        private float turnRoll;
        private float attackTimer;
        private float screechTimer;
        private float deathTimer = -1f;
        private float hitKick;
        private float flashTimer;
        private float nextTwitch;
        private float twitchYaw;
        private Vector2 headAngles;
        private float seed;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;

        public bool IsDeathFinished => deathTimer >= DEATH_TIME;

        private void Awake()
        {
            pelvisRest = pelvis.localPosition;
            lastPosition = transform.position;
            lastYaw = transform.eulerAngles.y;
            seed = Random.value * 100f;
            phase = Random.value * Mathf.PI * 2f;
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            ScheduleTwitch();
        }

        public void Attack() => attackTimer = ATTACK_TIME;

        public void Screech() => screechTimer = SCREECH_TIME;

        public void Hit()
        {
            hitKick = 1f;
            flashTimer = 0.1f;
            SetFlash(true);
        }

        public void Die()
        {
            deathTimer = 0f;
            SetFlash(false);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateLocomotion(dt);
            UpdateTimers(dt);

            if (deathTimer >= 0f)
            {
                deathTimer += dt;
                PoseDeath(Mathf.Clamp01(deathTimer / DEATH_TIME));
                return;
            }
            PoseAlive(dt);
        }

        private void UpdateLocomotion(float dt)
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            float speed = delta.magnitude / dt;
            speed01 = Mathf.Lerp(speed01, Mathf.Clamp01(speed / runSpeed), 1f - Mathf.Exp(-8f * dt));
            phase += delta.magnitude / STRIDE_LENGTH * Mathf.PI * 2f;

            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;
            turnRoll = Mathf.Lerp(turnRoll, Mathf.Clamp(-yawRate * turnLean, -15f, 15f), 1f - Mathf.Exp(-6f * dt));
        }

        private void UpdateTimers(float dt)
        {
            attackTimer = Mathf.Max(0f, attackTimer - dt);
            screechTimer = Mathf.Max(0f, screechTimer - dt);
            hitKick *= Mathf.Exp(-HIT_RECOVERY * dt);
            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) SetFlash(false);
            }
        }

        private void PoseAlive(float dt)
        {
            float t = Time.time + seed;
            float idle = 1f - speed01;
            float breathe = Mathf.Sin(t * 2.2f);
            float sinP = Mathf.Sin(phase);
            float cosP = Mathf.Cos(phase);

            // Attack: 0..1 over the swing; claws pull back (first 40%) then slash forward.
            float attack = attackTimer > 0f ? 1f - attackTimer / ATTACK_TIME : 0f;
            float windUp = attack > 0f && attack < 0.4f ? attack / 0.4f : 0f;
            float slash = attack >= 0.4f ? Mathf.Sin((attack - 0.4f) / 0.6f * Mathf.PI) : 0f;
            float screech = screechTimer > 0f ? Mathf.Sin((1f - screechTimer / SCREECH_TIME) * Mathf.PI) : 0f;

            // Pelvis: bob twice per cycle, sway side to side, crouch a little when idle.
            float bob = Mathf.Abs(sinP) * bobHeight * speed01;
            float crouch = idle * 0.03f * (1f + breathe * 0.3f);
            pelvis.localPosition = pelvisRest + new Vector3(sinP * 0.025f * speed01, bob - crouch, 0f);
            Blend(pelvis, Quaternion.Euler(0f, sinP * 6f * speed01, cosP * 5f * speed01 + turnRoll * 0.5f), dt);

            // Torso: lean into speed and turns, counter-twist against the hips, recoil when hit.
            float lean = Mathf.Lerp(4f, maxLean, speed01) + breathe * 2f * idle + slash * 18f - windUp * 10f
                         - hitKick * 22f + screech * -14f;
            Blend(torso, Quaternion.Euler(lean, -sinP * 9f * speed01, turnRoll), dt);

            // Legs: opposite phases; the knee folds while the leg swings forward.
            PoseLeg(thighL, shinL, sinP, cosP, dt);
            PoseLeg(thighR, shinR, -sinP, -cosP, dt);

            // Arms: counter-swing to the legs with a loose lag, plus the attack slash.
            float lag = Mathf.Sin(phase - 0.6f);
            float armIdle = Mathf.Sin(t * 1.7f) * 6f * idle;
            float armAttack = windUp * 55f - slash * 95f;
            Blend(upperArmL, Quaternion.Euler(-lag * armSwing * speed01 + armIdle + armAttack - screech * 30f, 0f, -screech * 25f), dt);
            Blend(upperArmR, Quaternion.Euler(lag * armSwing * speed01 - armIdle + armAttack - screech * 30f, 0f, screech * 25f), dt);
            float foreArm = -15f * speed01 - slash * 40f + windUp * 30f;
            Blend(foreArmL, Quaternion.Euler(foreArm + Mathf.Max(0f, lag) * 15f * speed01, 0f, 0f), dt);
            Blend(foreArmR, Quaternion.Euler(foreArm + Mathf.Max(0f, -lag) * 15f * speed01, 0f, 0f), dt);

            PoseHead(dt, t, screech, slash);
        }

        private void PoseLeg(Transform thigh, Transform shin, float sinP, float cosP, float dt)
        {
            float swing = -sinP * legSwing * speed01;                  // negative = leg forward
            float knee = Mathf.Max(0f, cosP) * kneeBend * speed01;      // fold during the forward swing
            float idleBend = (1f - speed01) * 12f;
            Blend(thigh, Quaternion.Euler(swing - idleBend, 0f, 0f), dt);
            Blend(shin, Quaternion.Euler(knee + idleBend * 1.6f, 0f, 0f), dt);
        }

        private void PoseHead(float dt, float t, float screech, float slash)
        {
            if (Time.time >= nextTwitch)
            {
                twitchYaw = Random.Range(-28f, 28f);
                ScheduleTwitch();
            }
            twitchYaw = Mathf.MoveTowards(twitchYaw, 0f, 90f * dt);

            Vector2 desired = Vector2.zero;
            if (LookTarget != null)
            {
                Vector3 local = torso.InverseTransformPoint(LookTarget.position + Vector3.up * 1.4f) - head.localPosition;
                desired.x = Mathf.Clamp(-Mathf.Atan2(local.y, Mathf.Max(0.1f, local.z)) * Mathf.Rad2Deg, -30f, 30f);
                desired.y = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(0.1f, local.z)) * Mathf.Rad2Deg, -55f, 55f);
            }
            headAngles = Vector2.Lerp(headAngles, desired, 1f - Mathf.Exp(-headLookSpeed * dt));

            float tilt = Mathf.Sin(t * 0.9f) * 8f + twitchYaw * 0.4f;
            head.localRotation = Quaternion.Euler(headAngles.x - screech * 25f + hitKick * 15f,
                headAngles.y + twitchYaw, tilt);

            float jawOpen = 4f + Mathf.Max(0f, Mathf.Sin(t * 2.2f)) * 4f + screech * 35f + slash * 25f;
            jaw.localRotation = Quaternion.Euler(jawOpen, 0f, 0f);
        }

        /// <summary>Collapse: knees buckle, the body drops and pitches forward, arms go limp.</summary>
        private void PoseDeath(float k)
        {
            float e = 1f - (1f - k) * (1f - k);   // ease out
            pelvis.localPosition = Vector3.Lerp(pelvis.localPosition, pelvisRest + new Vector3(0f, -0.55f, 0.1f), e);
            pelvis.localRotation = Quaternion.Slerp(pelvis.localRotation, Quaternion.Euler(25f, 0f, 12f), e);
            torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.Euler(55f, 10f, 0f), e);
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(30f, -35f, 20f), e);
            jaw.localRotation = Quaternion.Slerp(jaw.localRotation, Quaternion.Euler(30f, 0f, 0f), e);
            thighL.localRotation = Quaternion.Slerp(thighL.localRotation, Quaternion.Euler(-70f, 0f, 10f), e);
            thighR.localRotation = Quaternion.Slerp(thighR.localRotation, Quaternion.Euler(-55f, 0f, -10f), e);
            shinL.localRotation = Quaternion.Slerp(shinL.localRotation, Quaternion.Euler(110f, 0f, 0f), e);
            shinR.localRotation = Quaternion.Slerp(shinR.localRotation, Quaternion.Euler(100f, 0f, 0f), e);
            upperArmL.localRotation = Quaternion.Slerp(upperArmL.localRotation, Quaternion.Euler(30f, 0f, -30f), e);
            upperArmR.localRotation = Quaternion.Slerp(upperArmR.localRotation, Quaternion.Euler(20f, 0f, 40f), e);
        }

        private static void Blend(Transform joint, Quaternion target, float dt)
        {
            joint.localRotation = Quaternion.Slerp(joint.localRotation, target, 1f - Mathf.Exp(-POSE_SHARPNESS * dt));
        }

        private void ScheduleTwitch() => nextTwitch = Time.time + Random.Range(1.2f, 3.5f);

        private void SetFlash(bool isOn)
        {
            foreach (var r in renderers)
            {
                if (isOn)
                {
                    block.SetColor(BASE_COLOR, hitFlashColor);
                    r.SetPropertyBlock(block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}
