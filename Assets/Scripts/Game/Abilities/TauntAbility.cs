using Fusion;
using UnityEngine;
using Game.AnimationControl;

namespace Game.Abilities
{
    public class TauntAbility : BaseAbility
    {
        public override void HandleInput()
        {
            if (!HasStateAuthority) return;
            if (Input.ButtonEPressed)
            {
              //  GetComponent<PlayerAnimation>().SetState(PlayerAnimState.Taunt);
                Stats.RecoverStamina(TypeData.staminaRegenRate * 5);
            }
        }
    }
}
