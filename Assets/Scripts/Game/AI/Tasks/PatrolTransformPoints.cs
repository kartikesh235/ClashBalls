using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI.Tasks
{
    // Patrol using transforms from PatrolPoints script
    [TaskCategory("Game/Movement")]
    public class PatrolTransformPoints : Action
    {
        [RequiredField]
        [BehaviorDesigner.Runtime.Tasks.Tooltip("Reference to PatrolPoints script in the scene")]
        public SharedGameObject patrolPointsObject;
        
        [SharedRequired]
        public SharedVector3 currentTarget;
        
        [SharedRequired]
        public SharedVector2 moveDirection;
        
        [RequiredField]
        public SharedFloat patrolSpeed = 0.5f;
        
        [RequiredField]
        [BehaviorDesigner.Runtime.Tasks.Tooltip("Distance to patrol point before moving to next")]
        public SharedFloat reachDistance = 2f;
        
        private PatrolPointsManager mPatrolPoints;
        private int mCurrentPatrolIndex;
        private bool mInitialized = false;

        public override void OnStart()
        {
            if (!mInitialized)
            {
                // Get PatrolPoints component
                if (patrolPointsObject != null && patrolPointsObject.Value != null)
                {
                    mPatrolPoints = patrolPointsObject.Value.GetComponent<PatrolPointsManager>();
                }
                
                // Fallback: search for PatrolPoints in scene
                if (mPatrolPoints == null)
                {
                    mPatrolPoints = UnityEngine.Object.FindObjectOfType<PatrolPointsManager>();
                }
                
                if (mPatrolPoints == null)
                {
                    Debug.LogWarning($"PatrolTransformPoints: No PatrolPoints script found for {gameObject.name}");
                    return;
                }
                
                if (mPatrolPoints.PatrolCount == 0)
                {
                    Debug.LogWarning($"PatrolTransformPoints: No patrol points defined in PatrolPoints script");
                    return;
                }
                
                // Start at closest patrol point
                mCurrentPatrolIndex = mPatrolPoints.FindClosestPatrolIndex(transform.position);
                mInitialized = true;
            }
            
            // Set initial target
            if (mPatrolPoints != null && currentTarget != null)
            {
                currentTarget.Value = mPatrolPoints.GetPatrolPosition(mCurrentPatrolIndex);
            }
        }

        public override TaskStatus OnUpdate()
        {
            if (mPatrolPoints == null || mPatrolPoints.PatrolCount == 0)
                return TaskStatus.Failure;
                
            if (currentTarget == null || moveDirection == null)
                return TaskStatus.Failure;
                
            float distance = Vector3.Distance(transform.position, currentTarget.Value);
            
            // Check if we've reached current patrol point
            if (distance <= reachDistance.Value)
            {
                // Move to next patrol point
                mCurrentPatrolIndex = mPatrolPoints.GetNextPatrolIndex(mCurrentPatrolIndex);
                currentTarget.Value = mPatrolPoints.GetPatrolPosition(mCurrentPatrolIndex);
            }

            // Set movement direction towards current target
            Vector3 direction = (currentTarget.Value - transform.position).normalized;
            moveDirection.Value = new Vector2(direction.x, direction.z) * 7;

            return TaskStatus.Running;
        }

        public override void OnReset()
        {
            mInitialized = false;
            mCurrentPatrolIndex = 0;
        }
    }
}