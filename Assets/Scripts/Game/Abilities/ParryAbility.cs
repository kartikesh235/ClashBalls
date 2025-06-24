using Fusion;
using UnityEngine;
using Game.AnimationControl;
using Game.Controllers;

namespace Game.Abilities
{
    public class ParryAbility : BaseAbility
    {
        [Networked] public bool IsParrying { get; private set; }
        [Networked] private float ParryTimer { get; set; }

        public GameObject vfxParry;

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (IsParrying)
            {
                ParryTimer -= Runner.DeltaTime;
                if (ParryTimer <= 0f)
                {
                    IsParrying = false;
                }
            }
            
            // Update VFX on all clients
            if (vfxParry != null)
                vfxParry.SetActive(IsParrying);
        }

        public override void HandleInput()
        {
            if (!HasStateAuthority) return;

            if (Input.ButtonCPressed && !IsParrying)
            {
                IsParrying = true;
                ParryTimer = TypeData.parryDuration;
                
                GetComponent<PlayerAnimation>().SetState(PlayerAnimState.Parry);
                StartCoroutine(ExtraUtils.SetDelay(0.5f, () =>
                {
                    GetComponent<PlayerAnimation>().SetState(PlayerAnimState.Locomotion);
                }));
            }
        }

        public bool TryParry()
        {
            if (IsParrying)
                return true;
            
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