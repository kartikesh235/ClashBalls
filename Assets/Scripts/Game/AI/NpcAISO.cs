using UnityEngine;

namespace Game.AI
{
    [CreateAssetMenu(fileName = "NPCAISettings", menuName = "Game/NPC AI Settings")]
    public class NpcAISO : ScriptableObject
    {
        [Header("Detection Ranges")]
        public float ballDetectionRange = 20f;
        public float enemyDetectionRange = 100f;
        public float dangerDetectionRange = 4f; // Enemy with ball nearby
        public float tackleRange = 3f;
        public float throwRange = 12f;
        public float parryRange = 5f;
        
        [Header("Reaction Settings")]
        public float dodgeChance = 0.7f;
        public float parryChance = 0.3f;
        public float tackleChance = 0.8f;
        
        [Header("Decision Making")]
        public float decisionCooldown = 0.3f;
        public float combatCooldown = 2f;
        
        [Header("Stamina Thresholds")]
        public float lowStaminaThreshold = 0.3f;
        public float restStaminaThreshold = 0.8f;
        public float aggressiveStaminaThreshold = 0.6f;
    }
}