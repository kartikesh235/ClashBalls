using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Actions")]
    public class RandomCombatAction : Action
    {
        [SharedRequired]
        public SharedBool buttonB; // Dodge
        
        [SharedRequired]
        public SharedBool buttonD; // Tackle
        
        [RequiredField]
        public SharedFloat randomActionChance = 0.3f;
        
        [RequiredField]
        public SharedFloat actionCooldown = 2f;
        
        private float mLastActionTime;

        public override TaskStatus OnUpdate()
        {
            if (Time.time - mLastActionTime < actionCooldown.Value)
                return TaskStatus.Failure;

            if (Random.value > randomActionChance.Value)
                return TaskStatus.Failure;

            mLastActionTime = Time.time;

            // 50/50 chance between dodge and tackle
            if (Random.value < 0.5f)
            {
                buttonB.Value = true; // Dodge
            }
            else
            {
                buttonD.Value = true; // Tackle
            }

            return TaskStatus.Success;
        }
    }
}