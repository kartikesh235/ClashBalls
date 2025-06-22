using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace Game.AI.Tasks
{
    [TaskCategory("Game/Actions")]
    public class ThrowBallAtTarget : Action
    {
        [RequiredField]
        public SharedVector3 targetPosition;
        
        [SharedRequired] // Links to "ButtonAHeld" shared variable
        public SharedBool buttonAHeld;
        
        [SharedRequired] // Links to "ButtonAReleased" shared variable  
        public SharedBool buttonAReleased;
        
        [RequiredField]
        public SharedFloat chargeTime = 1f;
        
        private float currentChargeTime;
        private bool isCharging;

        public override void OnStart()
        {
            currentChargeTime = 0f;
            isCharging = true;
            
            // Start charging the throw
            buttonAHeld.Value = true;
        }

        public override TaskStatus OnUpdate()
        {
            if (isCharging)
            {
                currentChargeTime += Time.deltaTime;
                
                if (currentChargeTime >= chargeTime.Value)
                {
                    // Aim towards target and release
                    if (targetPosition.Value != Vector3.zero)
                    {
                        Vector3 direction = (targetPosition.Value - transform.position).normalized;
                        transform.forward = direction;
                    }
                    
                    // Release the throw
                    buttonAHeld.Value = false;
                    buttonAReleased.Value = true;
                    isCharging = false;
                    return TaskStatus.Success;
                }
                
                return TaskStatus.Running;
            }

            return TaskStatus.Success;
        }

        public override void OnEnd()
        {
            // Ensure we don't leave buttons in pressed state
            buttonAHeld.Value = false;
            buttonAReleased.Value = false;
        }
    }

}