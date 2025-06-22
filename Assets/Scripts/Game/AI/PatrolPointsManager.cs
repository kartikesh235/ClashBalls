using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI
{
    public class PatrolPointsManager:MonoBehaviour
    {
        [Header("Patrol Configuration")]
        [SerializeField] private Transform[] mPatrolTransforms;
        [SerializeField] private bool mLoopPatrol = true;
        [SerializeField] private bool mRandomOrder = false;
        
        [Header("Gizmos")]
        [SerializeField] private bool mShowGizmos = true;
        [SerializeField] private Color mGizmoColor = Color.yellow;
        [SerializeField] private float mGizmoRadius = 1f;
        
        public Transform[] PatrolTransforms => mPatrolTransforms;
        public bool LoopPatrol => mLoopPatrol;
        public bool RandomOrder => mRandomOrder;
        public int PatrolCount => mPatrolTransforms?.Length ?? 0;
        
        /// <summary>
        /// Get patrol point position by index
        /// </summary>
        public Vector3 GetPatrolPosition(int index)
        {
            if (mPatrolTransforms == null || mPatrolTransforms.Length == 0)
                return transform.position;
                
            index = Mathf.Clamp(index, 0, mPatrolTransforms.Length - 1);
            return mPatrolTransforms[index] != null ? mPatrolTransforms[index].position : transform.position;
        }
        
        /// <summary>
        /// Get next patrol index based on configuration
        /// </summary>
        public int GetNextPatrolIndex(int currentIndex)
        {
            if (mPatrolTransforms == null || mPatrolTransforms.Length == 0)
                return 0;
                
            if (mRandomOrder)
            {
                // Get random index different from current
                int randomIndex;
                do {
                    randomIndex = Random.Range(0, mPatrolTransforms.Length);
                } while (randomIndex == currentIndex && mPatrolTransforms.Length > 1);
                return randomIndex;
            }
            else
            {
                // Sequential patrol
                int nextIndex = currentIndex + 1;
                if (mLoopPatrol)
                {
                    return nextIndex % mPatrolTransforms.Length;
                }
                else
                {
                    return Mathf.Min(nextIndex, mPatrolTransforms.Length - 1);
                }
            }
        }
        
        /// <summary>
        /// Find closest patrol point to given position
        /// </summary>
        public int FindClosestPatrolIndex(Vector3 position)
        {
            if (mPatrolTransforms == null || mPatrolTransforms.Length == 0)
                return 0;
                
            int closestIndex = 0;
            float closestDistance = float.MaxValue;
            
            for (int i = 0; i < mPatrolTransforms.Length; i++)
            {
                if (mPatrolTransforms[i] == null) continue;
                
                float distance = Vector3.Distance(position, mPatrolTransforms[i].position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
            
            return closestIndex;
        }
        
        private void OnDrawGizmos()
        {
            if (!mShowGizmos || mPatrolTransforms == null) return;
            
            Gizmos.color = mGizmoColor;
            
            // Draw patrol points
            for (int i = 0; i < mPatrolTransforms.Length; i++)
            {
                if (mPatrolTransforms[i] == null) continue;
                
                Vector3 pos = mPatrolTransforms[i].position;
                Gizmos.DrawWireSphere(pos, mGizmoRadius);
                
                // Draw index numbers
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(pos + Vector3.up * (mGizmoRadius + 0.5f), i.ToString());
                #endif
            }
            
            // Draw patrol path
            if (mPatrolTransforms.Length > 1)
            {
                for (int i = 0; i < mPatrolTransforms.Length - 1; i++)
                {
                    if (mPatrolTransforms[i] == null || mPatrolTransforms[i + 1] == null) continue;
                    Gizmos.DrawLine(mPatrolTransforms[i].position, mPatrolTransforms[i + 1].position);
                }
                
                // Draw loop connection
                if (mLoopPatrol && mPatrolTransforms[0] != null && mPatrolTransforms[mPatrolTransforms.Length - 1] != null)
                {
                    Gizmos.DrawLine(mPatrolTransforms[mPatrolTransforms.Length - 1].position, mPatrolTransforms[0].position);
                }
            }
        }
    }
    
}