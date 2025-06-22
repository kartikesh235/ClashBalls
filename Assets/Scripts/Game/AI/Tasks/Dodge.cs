using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Actions")]
    public class Dodge : Action
    {
        [SharedRequired]
        public SharedBool buttonB;

        public override TaskStatus OnUpdate()
        {
            if (buttonB != null)
            {
                buttonB.Value = true;
                return TaskStatus.Success;
            }
            return TaskStatus.Failure;
        }
    }
}