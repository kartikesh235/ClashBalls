using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Actions")]
    public class PickUpBall : Action
    {
        [SharedRequired] // This must link to behavior tree's "ButtonA" shared variable
        public SharedBool buttonA;
        
        public override TaskStatus OnUpdate()
        {
            // Set shared variable to trigger button press
            // NPCController will read this and clear it after one frame
            buttonA.Value = true;
            return TaskStatus.Success;
        }
    }
}