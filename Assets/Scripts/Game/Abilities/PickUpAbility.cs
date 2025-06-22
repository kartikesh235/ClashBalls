using Fusion;
using Game.AnimationControl;
using UnityEngine;
using Game.Ball;

namespace Game.Abilities
{
    public class PickUpAbility : BaseAbility
    {
        [Networked] public bool HasBall { get; private set; }

        public override void HandleInput()  
        {
            if (!HasStateAuthority || !Input.ButtonAPressed || HasBall) return;

            float radius = 4f;
            var physicsScene = Runner.GetPhysicsScene();
            var results = new Collider[8];

            int hits = physicsScene.OverlapSphere(transform.position, radius, results, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits; i++)
            {
                var ball = results[i].GetComponentInParent<BallController>();
                if (ball != null && ball.CanPickUp())
                {
                    GetComponent<PlayerAnimation>().SetHandSubState(HandSubState.Pickup);
                    ball.PickUp(gameObject);
                    HasBall = true;
                    
                    var throwAbility = GetComponent<ThrowAbility>();
                    if (throwAbility != null)
                    {
                        throwAbility.SetHeldBall(ball);
                    }
                    break;
                }
            }
        }

        public void OnBallThrown()
        {
            HasBall = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 4f);
        }
    }
}