using Fusion;
using UnityEngine;
using Game.Ball;
using Game.AnimationControl;
using Game.GameUI;
using Game.AI;

namespace Game.Abilities
{
    public class ThrowAbility : BaseAbility
    {
        private BallController mHeldBall;
        private float mCharge;
        private float mHoldDelayTimer;
        private bool mIsNPC;

        [Networked] private TickTimer ThrowCooldown { get; set; }

        private const float PickupThrowDelay = 0.5f;
        private const float ThrowCooldownDuration = 1f; // 1 second cooldown

        public override void Spawned()
        {
            base.Spawned();
            mIsNPC = GetComponent<NetworkedNPCControllerNew>() != null;
        }

        public override void HandleInput()
        {
            if (!HasStateAuthority || mHeldBall == null) return;

            // Check if still in cooldown
            if (!ThrowCooldown.ExpiredOrNotRunning(Runner))
            {
                return; // Exit early if still in cooldown
            }

            if (mHoldDelayTimer > 0f)
            {
                mHoldDelayTimer -= Runner.DeltaTime;
                return;
            }

            if (mIsNPC)
            {
                HandleNPCThrow();
            }
            else
            {
                HandleHumanThrow();
            }
        }

        private void HandleNPCThrow()
        {
            if (Input.ButtonAPressed)
            {
                float force = TypeData.baseThrowForce * 1.5f;
                
                // NPCs execute directly since Host has state authority
                ExecuteThrow(force);
            }
        }

        private void HandleHumanThrow()
        {
            if (Input.ButtonAHeld)
            {
                mCharge += TypeData.throwChargeSpeed * Runner.DeltaTime;
                mCharge = Mathf.Min(mCharge, TypeData.maxChargeThrowMultiplier);

                float displayValue = TypeData.baseThrowForce * (1f + mCharge);
                
                // Only update UI for local player
                if (Object.HasInputAuthority && Game2DUI.Instance != null)
                {
                    Game2DUI.Instance.SetThrowPower(displayValue);
                }
            }

            if (Input.ButtonAReleased)
            {
                float force = TypeData.baseThrowForce * (1f + mCharge);
                
                // Humans use RPC
                RPC_ThrowBall(force);
                
                // Only update UI for local player
                if (Object.HasInputAuthority && Game2DUI.Instance != null)
                {
                    Game2DUI.Instance.SetThrowPower(0);
                }
            }
        }

        public void SetHeldBall(BallController ball)
        {
            mHeldBall = ball;
            mHoldDelayTimer = 0.5f;
            mCharge = 0f;

            if (!mIsNPC && Object.HasInputAuthority && Game2DUI.Instance != null)
            {
                float minForce = 0;
                float maxForce = TypeData.baseThrowForce * TypeData.maxChargeThrowMultiplier;
                Game2DUI.Instance.ConfigureThrowSlider(minForce, maxForce);
                Game2DUI.Instance.SetThrowPower(TypeData.baseThrowForce);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_ThrowBall(float force)
        {
            ExecuteThrow(force);
        }

        public void ExecuteThrow(float force)
        {
            if (mHeldBall == null) return;

            // Set throw cooldown - prevents HandleInput from working for 1 second
            ThrowCooldown = TickTimer.CreateFromSeconds(Runner, ThrowCooldownDuration);

            // Get player's velocity
            var rb = GetComponent<Rigidbody>();
            Vector3 playerVelocity = rb != null ? rb.linearVelocity : Vector3.zero;

            // Add player velocity to throw direction
            Vector3 finalThrowDirection = transform.forward;
            Vector3 finalForce = finalThrowDirection * force + playerVelocity;

            // Apply throw
            GetComponent<PlayerAnimation>().SetHandSubState(HandSubState.Throw);
            StartCoroutine(ExtraUtils.SetDelay(0.46f/1.5f, () =>
            {
                if (mHeldBall != null) // Check if ball still exists
                {
                    mHeldBall.Throw(finalForce.normalized, finalForce.magnitude, gameObject);
                }
                
                mCharge = 0f;
                mHeldBall = null;
                
                var pickupAbility = GetComponent<PickUpAbility>();
                if (pickupAbility != null)
                {
                    pickupAbility.OnBallThrown();
                }
            }));
        }

        // Optional: Method to check if ability is on cooldown (for UI/debugging)
        public bool IsOnCooldown()
        {
            return !ThrowCooldown.ExpiredOrNotRunning(Runner);
        }

        // Optional: Get remaining cooldown time (for UI)
        public float GetCooldownTimeRemaining()
        {
            if (ThrowCooldown.ExpiredOrNotRunning(Runner))
                return 0f;
            
            return (float)ThrowCooldown.RemainingTime(Runner);
        }

    }
}