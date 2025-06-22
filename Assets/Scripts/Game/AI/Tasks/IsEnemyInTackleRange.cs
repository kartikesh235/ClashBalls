using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Conditions")]
    public class IsEnemyInTackleRange : Conditional
    {
        public SharedVector3 enemyPosition;
        public SharedFloat tackleRange = 3f;

        public override TaskStatus OnUpdate()
        {
            if (enemyPosition.Value == Vector3.zero)
                return TaskStatus.Failure;

            float distance = Vector3.Distance(transform.position, enemyPosition.Value);
            return distance <= tackleRange.Value ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}