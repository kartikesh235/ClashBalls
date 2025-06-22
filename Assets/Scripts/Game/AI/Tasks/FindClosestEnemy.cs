using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Game.Controllers;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Detection")]
    public class FindClosestEnemy : Action
    {
        [SharedRequired]
        public SharedVector3 enemyPosition;
        
        [RequiredField]
        public SharedFloat maxDetectionRange = 15f;

        public override TaskStatus OnUpdate()
        {
            // Use static method from UnityEngine.Object
#if UNITY_2022_2_OR_NEWER
            var players = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
#else
            var players = UnityEngine.Object.FindObjectsOfType<PlayerController>();
#endif
            
            PlayerController closestEnemy = null;
            float closestDistance = float.MaxValue;

            foreach (var player in players)
            {
                if (player == null || player.gameObject == gameObject) continue; // Skip self

                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < closestDistance && distance <= maxDetectionRange.Value)
                {
                    closestDistance = distance;
                    closestEnemy = player;
                }
            }

            if (closestEnemy != null)
            {
                enemyPosition.Value = closestEnemy.transform.position;
                return TaskStatus.Success;
            }

            return TaskStatus.Failure;
        }
    }
}