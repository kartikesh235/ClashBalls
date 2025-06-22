using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Actions")]
    public class Tackle : Action
    {
        [SharedRequired]
        public SharedBool buttonD;
        
        [RequiredField]
        public SharedVector3 targetPosition;

        public override TaskStatus OnUpdate()
        {
            // Face the target before tackling
            if (targetPosition != null && targetPosition.Value != Vector3.zero)
            {
                Vector3 direction = (targetPosition.Value - transform.position).normalized;
                transform.forward = direction;
            }
            
            if (buttonD != null)
            {
                buttonD.Value = true;
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }
    }
}