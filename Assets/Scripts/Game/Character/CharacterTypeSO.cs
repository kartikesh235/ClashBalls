using UnityEngine;

namespace Game.Character
{
    [CreateAssetMenu(fileName = "CharacterType", menuName = "Game/Character Type")]
    public class CharacterTypeSO : ScriptableObject
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float sprintSpeed = 7f;

        [Header("Stamina")]
        public float maxStamina = 3f;
        public float staminaRegenRate = 1f;
        public float staminaRegenDelay = 1f;
        
        [Header("Dodge")]
        public float dodgeSpeed = 10f;
        public float sprintDodgeMultiplier = 1.5f;
        public float dodgeCooldown = 1f;

        [Header("Throwing")]
        public float baseThrowForce = 10f;
        public float maxChargeThrowMultiplier = 2f;
        public float throwChargeSpeed = 1f;
        public float pickThrowCooldown = 1.5f;

        [Header("Parry")]
        public float parryDuration = 1f;
        public float failStunDuration = 0.5f;
        public float parryThrowCooldown = 1.5f;
        
        [Header("Tackle / Offensive Slide")]
        public float tackleRange = 2f;
        public float tackleForce = 20f;
        public float tackleCooldown = 1.5f;
        public float tackleStunDuration = 1f;

        [Header("Passives")]
        public PassiveType passive;

        public enum PassiveType
        {
            QuickRecovery,
            CounterMaster,
            IronBody
        }
    }
}