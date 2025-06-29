using Fusion;
using Game.AnimationControl;
using Game.Character;
using Game.Controllers;
using Game.Managers;
using UnityEngine;
using Fusion.Addons.Physics;

namespace Game.Abilities
{
    public class TackleAbility : BaseAbility
    {
        [Header("Tackle Settings")]
        public GameObject vfxTackle;
        public float hitOpponentForce = 40f;
        public float tackleDuration = 0.2f;
        [SerializeField] private float mTravelDistance = 10f;
        [SerializeField] private LayerMask mWallMask;

        private Rigidbody mRb;
        private float mCooldownTimer;
        private float mTackleTimer;
        private float mRequiredAcceleration;
        private Vector3 mTackleDirection;
        private bool mIsTackling;
        private PlayerMovement mCachedTarget;

        private void Awake()
        {
            mRb = GetComponent<Rigidbody>();
            mTravelDistance = TypeData.tackeTravelDistance;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            UpdateCooldown();

            if (mIsTackling)
                PerformTackleMovement();
        }

        public override void HandleInput()
        {
            if (!CanStartTackle()) 
                return;

            if (!Stats.ConsumeStamina(TypeData.tackleStaminaCost))
                return;
            StartTackle();
        }

        private bool CanStartTackle()
        {
            return HasStateAuthority && Input.ButtonDPressed && mCooldownTimer <= 0f && !mIsTackling;
        }

        private void StartTackle()
        {
            mCooldownTimer = TypeData.tackleCooldown;
            mTackleDirection = transform.forward;
            mTravelDistance = CalculateAvailableTravelDistance(mTackleDirection);

            ComputeTacklePhysics();
            BeginTackleState();
            PlayTackleEffects();
            HandleTackleHitDetection();
        }

        private void PerformTackleMovement()
        {
            mTackleTimer += Runner.DeltaTime;
            mRb.AddForce(mTackleDirection * mRequiredAcceleration, ForceMode.Acceleration);

            if (mTackleTimer >= tackleDuration)
                EndTackle();
        }

        private void EndTackle()
        {
            mIsTackling = false;
            mRb.linearVelocity = Vector3.zero;
            GetComponent<PlayerAnimation>().SetState(PlayerAnimState.Locomotion);
        }

        private float CalculateAvailableTravelDistance(Vector3 direction)
        {
            Vector3 origin = transform.position + direction * 0.1f;
            var physicsScene = Runner.GetPhysicsScene();

            if (physicsScene.Raycast(origin, direction, out var hit, mTravelDistance, mWallMask))
                return hit.distance;

            return mTravelDistance;
        }

        private void ComputeTacklePhysics()
        {
            mRequiredAcceleration = 2f * mTravelDistance / (tackleDuration * tackleDuration);
        }

        private void BeginTackleState()
        {
            mTackleTimer = 0f;
            mIsTackling = true;
            GetComponent<PlayerAnimation>().SetState(PlayerAnimState.Tackle);
        }

        private void PlayTackleEffects()
        {
            if (vfxTackle != null)
            {
                vfxTackle.SetActive(true);
                StartCoroutine(ExtraUtils.SetDelay(1f, () => vfxTackle.SetActive(false)));
            }

            StartCoroutine(ExtraUtils.SetDelay(0.2f, () =>
            {
                GetComponent<PlayerAnimation>().SetState(PlayerAnimState.Locomotion);
            }));
        }

        private void HandleTackleHitDetection()
        {
            var direction = mTackleDirection;
            var physicsScene = Runner.GetPhysicsScene();

            var results = new Collider[8];
            var hits = physicsScene.OverlapSphere(transform.position + direction, 1f, results, ~0, QueryTriggerInteraction.Collide);

            for (var i = 0; i < hits; i++)
            {
                var target = results[i].GetComponentInParent<NetworkObject>();
                if (target == null || target == Object) continue;

                if (HasStateAuthority)
                {
                    RPC_ApplyTackleHit(target.Id, direction, TypeData.tackleForce);
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ApplyTackleHit(NetworkId targetId, Vector3 direction, float force)
        {
            var targetObj = Runner.FindObject(targetId);
            if (targetObj == null) return;

            var target = targetObj.GetComponent<PlayerController>();
            if (target == null) return;

            // Check for parry first
            var parryAbility = target.GetComponent<ParryAbility>();
            if (parryAbility != null && parryAbility.IsParrying)
            {
                Debug.Log($"Tackle parried by {target.name}");
                return;
            }

            // Apply consistent force regardless of network authority
            ApplyConsistentForce(targetObj, direction, force);

            // Apply stun only on StateAuthority to avoid duplication
            if (HasStateAuthority)
            {
                var stunSystem = target.GetComponent<StunSystem>();
                if (stunSystem != null)
                {
                    stunSystem.ApplyStun(TypeData.tackleStunDuration);
                }

                var healthSystem = target.GetComponent<PlayerHealthSystem>();
                if (healthSystem != null && healthSystem.CanTakeDamage())
                {
                    healthSystem.TakeDamage(TypeData.tackleAttackPower, GetComponent<PlayerController>());
                    ScoreManager.Instance.AddScore(GetComponent<PlayerController>(), (int)TypeData.tackleAttackPower, "Tackle");
                }
            }
        }

        private void ApplyConsistentForce(NetworkObject target, Vector3 direction, float force)
        {
            // Always use the same force application method regardless of player type
            var networkRb = target.GetComponent<NetworkRigidbody3D>();
            if (networkRb != null && networkRb.Rigidbody != null)
            {
                // Force consistency: Use VelocityChange for immediate effect
                Vector3 forceVector = direction.normalized * force;
                networkRb.Rigidbody.AddForce(forceVector, ForceMode.VelocityChange);
                
                Debug.Log($"Applied consistent tackle force {force} to {target.name} (NetworkRB)");
                return;
            }

            // Fallback to regular Rigidbody with same force mode
            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 forceVector = direction.normalized * force;
                rb.AddForce(forceVector, ForceMode.VelocityChange);
                
                Debug.Log($"Applied consistent tackle force {force} to {target.name} (Regular RB)");
            }
        }

        private void UpdateCooldown()
        {
            if (mCooldownTimer > 0f)
                mCooldownTimer -= Runner.DeltaTime;
        }

        public void RunnerInvokeEnable(PlayerMovement targetMovement, float delay)
        {
            mCachedTarget = targetMovement;
            Runner.Invoke(nameof(EnableTargetMovement), delay);
        }

        private void EnableTargetMovement()
        {
            if (mCachedTarget != null)
                mCachedTarget.enabled = true;
        }
    }
}