using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Game.Ball;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Detection")]
    public class FindClosestBall : Action
    {
        [SharedRequired]
        public SharedVector3 ballPosition;
        
        [RequiredField]
        public SharedFloat maxDetectionRange = 20f;

        public override TaskStatus OnUpdate()
        {
            // Use static method from UnityEngine.Object
#if UNITY_2022_2_OR_NEWER
            var balls = UnityEngine.Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
#else
            var balls = UnityEngine.Object.FindObjectsOfType<BallController>();
#endif
            
            if (balls.Length == 0)
                return TaskStatus.Failure;

            BallController closestBall = null;
            float closestDistance = float.MaxValue;

            foreach (var ball in balls)
            {
                if (ball == null) continue;
                
                float distance = Vector3.Distance(transform.position, ball.transform.position);
                if (distance < closestDistance && distance <= maxDetectionRange.Value)
                {
                    closestDistance = distance;
                    closestBall = ball;
                }
            }

            if (closestBall != null)
            {
                ballPosition.Value = closestBall.transform.position;
                return TaskStatus.Success;
            }

            return TaskStatus.Failure;
        }
    }
}