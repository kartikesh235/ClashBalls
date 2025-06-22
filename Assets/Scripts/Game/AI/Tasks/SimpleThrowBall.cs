using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Actions")]
    public class SimpleThrowBall : Action
    {
        [RequiredField]
        public SharedVector3 targetPosition;
        
        [SharedRequired]
        public SharedBool buttonA;

        public override TaskStatus OnUpdate()
        {
            // Aim towards target
            if (targetPosition.Value != Vector3.zero)
            {
                Vector3 direction = (targetPosition.Value - transform.position).normalized;
                transform.forward = direction;
            }
            
            // Simple button press to throw
            buttonA.Value = true;
            return TaskStatus.Success;
        }
    }
}