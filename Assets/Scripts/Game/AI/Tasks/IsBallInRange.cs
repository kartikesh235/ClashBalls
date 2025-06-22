using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Conditions")]
    public class IsBallInRange : Conditional
    {
        public SharedFloat pickupRange = 4f;
        public SharedVector3 ballPosition;

        public override TaskStatus OnUpdate()
        {
            if (ballPosition.Value == Vector3.zero)
                return TaskStatus.Failure;

            float distance = Vector3.Distance(transform.position, ballPosition.Value);
            return distance <= pickupRange.Value ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}