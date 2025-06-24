using UnityEngine;

namespace Game.Character
{
    [CreateAssetMenu(fileName = "CharacterType", menuName = "Game/Character Type")]
    public class CharacterTypeSO : ScriptableObject
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float sprintSpeed = 7f;

        [Header("Stamina And Health")]
        public float maxHealth = 100f;
        public float ballHitPower = 20f; // point by which health is reduced on ball by this player to other player
        public float maxStamina = 3f;
        public float staminaRegenRate = 1f; // per second
        public float staminaRegenDelay = 1f; // seconds before regen starts
        
        [Header("Dodge")]
        public float dodgeDistance = 10f;
        public float sprintDodgeMultiplier = 1.5f;
        public float dodgeCooldown = 1f;
        public float dodgeStaminaCost = 1f;

        [Header("Throwing")]
        public float baseThrowForce = 10f;
        public float maxChargeThrowMultiplier = 2f;
        public float throwChargeSpeed = 1f;
        public float pickThrowCooldown = 1.5f;

        [Header("Parry")]
        public float parryDuration = 1f;
        public float parryCooldown = 1.5f;
        public float parryStaminaCost = 1f;
        
        [Header("Tackle / Offensive Slide")]
        public float tackeTravelDistance = 2f;
        public float tackleForce = 20f;
        public float tackleCooldown = 1.5f;
        public float tackleStunDuration = 1f;
        public float tackleStaminaCost = 1f;
        public float tackleAttackPower = 40f; // point by which other player health is reduced on successful tackle

        // [Header("Passives")]
        // public PassiveType passive;
        //
        // public enum PassiveType
        // {
        //     QuickRecovery,
        //     CounterMaster,
        //     IronBody
        // }
    }
}