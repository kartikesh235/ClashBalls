using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Game.Controllers;

namespace Game.AI.Tasks
{
    // Smart condition for dodge behavior - checks if enemy is threatening
    [TaskCategory("Game/Conditions")]
    public class IsEnemyThreatening : Conditional
    {
        [RequiredField]
        [BehaviorDesigner.Runtime.Tasks.Tooltip("Maximum range an enemy can throw a ball")]
        public SharedFloat ballThrowRange = 12f;
        
        [RequiredField]
        [BehaviorDesigner.Runtime.Tasks.Tooltip("How accurately enemy must be facing us (0 = any direction, 1 = directly at us)")]
        public SharedFloat facingThreshold = 0.3f;
        
        [BehaviorDesigner.Runtime.Tasks.Tooltip("Optional: Store the threatening enemy position")]
        public SharedVector3 threateningEnemyPosition;

        public override TaskStatus OnUpdate()
        {
            #if UNITY_2022_2_OR_NEWER
            var players = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            #else
            var players = UnityEngine.Object.FindObjectsOfType<PlayerController>();
            #endif

            foreach (var player in players)
            {
                if (player == null || player.gameObject == gameObject) continue; // Skip self

                // Check if enemy is within ball throw range
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance > ballThrowRange.Value) continue;

                // Check if enemy is facing towards us
                Vector3 enemyToUs = (transform.position - player.transform.position).normalized;
                Vector3 enemyForward = player.transform.forward;
                
                float facingDot = Vector3.Dot(enemyForward, enemyToUs);
                
                // If enemy is facing us (dot product above threshold)
                if (facingDot >= facingThreshold.Value)
                {
                    // Store threatening enemy position if requested
                    if (threateningEnemyPosition != null)
                        threateningEnemyPosition.Value = player.transform.position;
                    
                    return TaskStatus.Success; // Enemy is threatening!
                }
            }

            return TaskStatus.Failure; // No threatening enemies
        }
    }
}