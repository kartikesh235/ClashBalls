using Fusion;
using UnityEngine;
using Game.AnimationControl;
using Game.Controllers;

namespace Game.Abilities
{
    public class ParryAbility : BaseAbility
    {
        private bool mIsParrying;
        private float mTimer;

        public GameObject vfxParry;
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (mIsParrying)
            {
                mTimer -= Runner.DeltaTime;
                if (mTimer <= 0f)
                {
                    mIsParrying = false;
                }
            }
            vfxParry.SetActive(mIsParrying);
        }

        public override void HandleInput()
        {
            if (!HasStateAuthority) return;

            if (Input.ButtonCPressed && !mIsParrying)
            {
                mIsParrying = true;
                mTimer = TypeData.parryDuration;
                
                //GetComponent<PlayerAnimation>().SetState(PlayerAnimState.Parry);
            }
        }

        public bool TryParry()
        {
            if (mIsParrying)
                return true;

            DisableMovement();
            Runner.Invoke(nameof(EnableMovement), TypeData.failStunDuration);
            return false;
        }

        private void DisableMovement()
        {
            var move = GetComponent<PlayerMovement>();
            if (move != null)
                move.enabled = false;
        }

        private void EnableMovement()
        {
            var move = GetComponent<PlayerMovement>();
            if (move != null)
                move.enabled = true;
        }

    }
}
