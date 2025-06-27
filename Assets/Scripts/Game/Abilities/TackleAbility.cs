using Fusion;
using Game.AnimationControl;
using Game.Character;
using Game.Controllers;
using Game.Managers;
using UnityEngine;

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

        #region Tackle Logic

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

        #endregion

        #region VFX & Animation

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

        #endregion

        #region Hit Detection

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

                ApplyHitForce(target, direction);
                DisableOpponentMovement(target);
            }
        }

        private void ApplyHitForce(NetworkObject target, Vector3 direction)
        {
            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(direction * TypeData.tackleForce, ForceMode.VelocityChange);
    
            // Apply damage
            var healthSystem = target.GetComponent<PlayerHealthSystem>();
            if (healthSystem != null && healthSystem.CanTakeDamage())
            {
                healthSystem.TakeDamage(TypeData.tackleForce, GetComponent<PlayerController>());
        
                // Add score
                ScoreManager.Instance.AddScore(GetComponent<PlayerController>(), (int)TypeData.tackleForce, "Tackle");
            }
        }
        private void DisableOpponentMovement(NetworkObject target)
        {
            if (target.HasInputAuthority)
                RPC_DisableMovement(target);
        }

        #endregion

        #region Cooldown

        private void UpdateCooldown()
        {
            if (mCooldownTimer > 0f)
                mCooldownTimer -= Runner.DeltaTime;
        }

        #endregion

        #region RPC & Re-enable Movement

        // Add this to your existing TackleAbility in the RPC_DisableMovement method:
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
       // Update the RPC in your existing TackleAbility
        private void RPC_DisableMovement(NetworkObject target)
        {
            var pm = target.GetComponent<PlayerMovement>();
            var parry = target.GetComponent<ParryAbility>();
            var stunSystem = target.GetComponent<StunSystem>();
    
            // Check if target successfully parried
            if (pm == null || parry.IsParrying)
                return;
   
            if (pm == null) 
                return;

            // Apply stun instead of just disabling movement
            if (stunSystem != null)
            {
                stunSystem.ApplyStun(TypeData.tackleStunDuration);
            }
            else
            {
                // Fallback to old system if no stun system
                pm.enabled = false;
                target.GetComponent<TackleAbility>().RunnerInvokeEnable(pm, TypeData.tackleStunDuration);
            }
    
            // Apply damage
            var healthSystem = target.GetComponent<PlayerHealthSystem>();
            if (healthSystem != null && healthSystem.CanTakeDamage())
            {
                healthSystem.TakeDamage(TypeData.tackleForce, GetComponent<PlayerController>());
              //  ScoreManager.Instance.AddScore(GetComponent<PlayerController>(), (int)TypeData.tackleForce, "Tackle");
            }
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

        #endregion
    }
}
