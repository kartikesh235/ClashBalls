using Fusion;
using Game.AnimationControl;
using Game.Input;
using UnityEngine;
using Game.Controllers;

namespace Game.Abilities
{
    public class DodgeAbility : BaseAbility
    {
        [SerializeField] private float mDodgeDistance = 2f;        // Max dodge distance
        [SerializeField] private float mDodgeDuration = 0.2f;      // Duration to complete dodge
        [SerializeField] private LayerMask mWallMask;

        private Rigidbody mRigidbody;
        private float mDodgeCooldownTimer;
        private float mDodgeTimer;
        private bool mIsDodging;
        private Vector3 mDodgeDirection;
        private float mRequiredAcceleration;
        private bool mIsNPC;

        public GameObject vfxRight;
        public GameObject vfxLeft;

        public override void Spawned()
        {
            base.Spawned();
            mRigidbody = GetComponent<Rigidbody>();
            mRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            mRigidbody.linearDamping = 0f;
            mRigidbody.angularDamping = 0f;
            mRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            mDodgeDistance = TypeData.dodgeDistance;
            
            // Check if this is an NPC
            mIsNPC = GetComponent<Game.AI.NetworkedNPCControllerNew>() != null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // Cooldown timer
            if (mDodgeCooldownTimer > 0f)
                mDodgeCooldownTimer -= Runner.DeltaTime;

            if (mIsDodging)
            {
                mDodgeTimer += Runner.DeltaTime;

                // Apply continuous acceleration
                mRigidbody.AddForce(mDodgeDirection * mRequiredAcceleration, ForceMode.Acceleration);

                // End dodge when time elapsed
                if (mDodgeTimer >= mDodgeDuration)
                {
                    mIsDodging = false;
                    mRigidbody.linearVelocity = Vector3.zero;
                    GetComponent<PlayerAnimation>().SetState(PlayerAnimState.Locomotion);
                }
            }
        }

        public override void HandleInput()
        {
            if (!HasStateAuthority || !Input.ButtonBPressed || mIsDodging || mDodgeCooldownTimer > 0f)
                return;

            if (!Stats.ConsumeStamina(TypeData.dodgeStaminaCost))
                return;

            // Determine dodge direction based on input or random
            var dodgeDir = Random.value < 0.5f ? -transform.right : transform.right;
            dodgeDir = transform.forward;
            // Raycast with small offset to avoid self-hit
            Vector3 origin = transform.position + dodgeDir * 0.1f;
            var physicsScene = Runner.GetPhysicsScene();
            float travelDistance;
            // if (physicsScene.Raycast(origin, dodgeDir, out var hit, mDodgeDistance, mWallMask))
            // {
            //     travelDistance = hit.distance;
            // }
            // else
            // {
            //    
            // }

            travelDistance = mDodgeDistance;
            // Set cooldown
            mDodgeCooldownTimer = TypeData.dodgeCooldown;

            // if joysticks are not at 0,0, multiply the travelDistance with the TypeData.sprintDodgeMultiplier
            if (Input.Movement != Vector2.zero)
            {
                travelDistance *= TypeData.sprintDodgeMultiplier;
            }
            
            // For NPCs, execute directly since Host has state authority
            // For humans, use RPC
            if (mIsNPC)
            {
                ExecuteDodge(dodgeDir, travelDistance);
            }
            else
            {
                RPC_DoDodge(dodgeDir, travelDistance);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_DoDodge(Vector3 direction, float travelDistance)
        {
            ExecuteDodge(direction, travelDistance);
        }

        private void ExecuteDodge(Vector3 direction, float travelDistance)
        {
            mIsDodging = true;
            mDodgeTimer = 0f;
            mDodgeDirection = direction;
            
            

            // Compute acceleration to cover distance in given duration: d = 0.5 * a * t^2 => a = 2*d / t^2
            mRequiredAcceleration = 2f * travelDistance / (mDodgeDuration * mDodgeDuration);

            // Play dodge animation
            GetComponent<PlayerAnimation>().SetState(
                direction.x > 0f ? PlayerAnimState.DodgeRight : PlayerAnimState.DodgeLeft
            );
            
            bool isLocalRight = Vector3.Dot(direction, transform.right) > 0f;
            if (isLocalRight)
            {
                vfxRight.SetActive(true);
            }
            else
            {
                vfxLeft.SetActive(true);
            }

            StartCoroutine(ExtraUtils.SetDelay(1f, () =>
            {
                vfxRight.SetActive(false);
                vfxLeft.SetActive(false);
            }));
        }
    }
}
