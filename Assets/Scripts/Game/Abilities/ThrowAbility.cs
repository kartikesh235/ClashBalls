using Fusion;
using UnityEngine;
using Game.Ball;
using Game.AnimationControl;
using Game.GameUI;

namespace Game.Abilities
{
    public class ThrowAbility : BaseAbility
    {
        private BallController mHeldBall;
        private float mCharge;
        private float mHoldDelayTimer;

        private const float PickupThrowDelay = 0.5f;

        public override void HandleInput()
        {
            if (!HasStateAuthority || mHeldBall == null) return;

            if (mHoldDelayTimer > 0f)
            {
                mHoldDelayTimer -= Runner.DeltaTime;
                return;
            }

            if (Input.ButtonAHeld)
            {
                mCharge += TypeData.throwChargeSpeed * Runner.DeltaTime;
                mCharge = Mathf.Min(mCharge, TypeData.maxChargeThrowMultiplier);

                float displayValue = TypeData.baseThrowForce * (1f + mCharge);
                Game2DUI.Instance.SetThrowPower(displayValue);
            }

            if (Input.ButtonAReleased)
            {
                float force =  TypeData.baseThrowForce * (1f + mCharge);

                RPC_ThrowBall(force);

                mCharge = 0f;
                mHeldBall = null;

                Game2DUI.Instance.SetThrowPower(0);
            }
        }

        public void SetHeldBall(BallController ball)
        {
            mHeldBall = ball;
            mHoldDelayTimer = 0.5f;
            mCharge = 0f;

            // Configure slider and set it to base value
            float minForce = 0;
            float maxForce = TypeData.baseThrowForce * TypeData.maxChargeThrowMultiplier;

            Game2DUI.Instance.ConfigureThrowSlider(minForce, maxForce);
            Game2DUI.Instance.SetThrowPower(TypeData.baseThrowForce);
        }


        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_ThrowBall(float force)
        {
            if (mHeldBall == null) return;

            // Get player's velocity
            var rb = GetComponent<Rigidbody>();
            Vector3 playerVelocity = rb != null ? rb.linearVelocity : Vector3.zero;

            // Add player velocity to throw direction
            Vector3 finalThrowDirection = transform.forward;
            Vector3 finalForce = finalThrowDirection * force + playerVelocity;

            // Apply throw
            mHeldBall.Throw(finalForce.normalized, finalForce.magnitude, gameObject);
            GetComponent<PlayerAnimation>().SetHandSubState(HandSubState.Throw);
        }

    }
}