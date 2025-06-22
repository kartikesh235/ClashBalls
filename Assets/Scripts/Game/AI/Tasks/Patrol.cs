using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Movement")]
    public class PatrolManualPoints : Action
    {
        [RequiredField]
        [UnityEngine.Tooltip("Define patrol points in world space coordinates")]
        public Vector3[] manualPatrolPoints = new Vector3[4];
        
        [SharedRequired]
        public SharedVector3 currentTarget;
        
        [SharedRequired]
        public SharedVector2 moveDirection;
        
        [RequiredField]
        public SharedFloat patrolSpeed = 0.5f;
        
        private int mCurrentPatrolIndex;

        public override void OnStart()
        {
            if (manualPatrolPoints == null || manualPatrolPoints.Length == 0)
            {
                Debug.LogWarning("No manual patrol points defined!");
                return;
            }
            
            mCurrentPatrolIndex = 0;
            if (currentTarget != null)
                currentTarget.Value = manualPatrolPoints[mCurrentPatrolIndex];
        }

        public override TaskStatus OnUpdate()
        {
            if (currentTarget == null || manualPatrolPoints == null || manualPatrolPoints.Length == 0)
                return TaskStatus.Failure;
                
            float distance = Vector3.Distance(transform.position, currentTarget.Value);
            
            if (distance < 2f)
            {
                // Move to next patrol point
                mCurrentPatrolIndex = (mCurrentPatrolIndex + 1) % manualPatrolPoints.Length;
                currentTarget.Value = manualPatrolPoints[mCurrentPatrolIndex];
            }

            // Set movement direction
            if (moveDirection != null)
            {
                Vector3 direction = (currentTarget.Value - transform.position).normalized;
                moveDirection.Value = new Vector2(direction.x, direction.z) * patrolSpeed.Value;
            }

            return TaskStatus.Running;
        }

        public override void OnReset()
        {
            mCurrentPatrolIndex = 0;
        }
    }
}