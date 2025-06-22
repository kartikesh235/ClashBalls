using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Game.Abilities;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Conditions")]
    public class HasBall : Conditional
    {
        public override TaskStatus OnUpdate()
        {
            var pickupAbility = GetComponent<PickUpAbility>();
            if (pickupAbility != null && pickupAbility.HasBall)
            {
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }
    }
}