using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Game.AI;
using Game.Ball;

namespace Game.AI.Tasks
{
    // Task to move towards a target position
    [TaskCategory("Game/Movement")]
    public class MoveTowards : Action
    {
        [RequiredField]
        public SharedVector3 targetPosition;
        
        [RequiredField] 
        public SharedFloat moveSpeed = 1f;
        
        [RequiredField]
        public SharedFloat stoppingDistance = 1f;
        
        [SharedRequired] // This links to the behavior tree's shared variable
        public SharedVector2 moveDirection;
        
        [SharedRequired]
        public SharedBool shouldSprint;

        private Transform myTransform;

        public override void OnAwake()
        {
            myTransform = transform;
        }

        public override TaskStatus OnUpdate()
        {
            if (targetPosition.Value == Vector3.zero)
            {
                moveDirection.Value = Vector2.zero;
                return TaskStatus.Failure;
            }

            Vector3 direction = (targetPosition.Value - myTransform.position);
            float distance = direction.magnitude;

            if (distance <= stoppingDistance.Value)
            {
                moveDirection.Value = Vector2.zero;
                shouldSprint.Value = false;
                return TaskStatus.Success;
            }

            direction.Normalize();
            Vector2 move2D = new Vector2(direction.x, direction.z) * 7;
            
            // Update shared variables - these will be read by NPCController
            moveDirection.Value = move2D;
            shouldSprint.Value = distance > 5f; // Sprint if far away
            
            return TaskStatus.Running;
        }
    }
}