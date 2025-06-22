using Fusion;
using Game.Input;
using UnityEngine;

namespace Game.AnimationControl
{
    public enum PlayerAnimState
    {
        Locomotion = 0,
        DodgeLeft = 1,
        DodgeRight = 2,
        Parry = 3, // shield
        Tackle = 4, // dash
    }

    public enum HandSubState
    {
        Throw = 1,
        Pickup = 2,
    }
    public class PlayerAnimation : NetworkBehaviour
    {
        private static readonly int State = Animator.StringToHash("State");
        private static readonly int HandState = Animator.StringToHash("HandState");
        private static readonly int JoystickX = Animator.StringToHash("JoystickX");
        private static readonly int JoystickY = Animator.StringToHash("JoystickY");
        private static readonly int RunMultiplier = Animator.StringToHash("RunMultiplier");
        
        [SerializeField]private Animator mAnim;
        private MmInputService mInput;

        [Networked]
        private PlayerAnimState NetworkedAnimState { get; set; }
        private PlayerAnimState mLastAnimState = PlayerAnimState.Locomotion;

        public override void Spawned()
        {
            
            mInput = GetComponent<MmInputService>();
            mLastAnimState = NetworkedAnimState;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (NetworkedAnimState == PlayerAnimState.Locomotion)
                {
                    if (mInput.Sprint)
                    {
                        mAnim.SetFloat(RunMultiplier, 1.5f);
                    }
                    else
                    {
                        mAnim.SetFloat(RunMultiplier, 1.0f);
                    }
                    SetLocomotionState(mInput.Movement.x, mInput.Movement.y);
                }
            }

            if (mLastAnimState != NetworkedAnimState)
            {
                mAnim.SetInteger(State, (int)NetworkedAnimState);
                mLastAnimState = NetworkedAnimState;
            }
        }

        public void SetState(PlayerAnimState state)
        {
            if (NetworkedAnimState == state) 
                return;
            NetworkedAnimState = state;
        }

        private void SetLocomotionState(float joystickX, float joystickY)
        {
            mAnim.SetFloat(JoystickX, joystickX);
            mAnim.SetFloat(JoystickY, joystickY);
        }
        
        public void SetHandSubState(HandSubState state)
        {
            mAnim.SetInteger(HandState, (int)state);
            if (state == HandSubState.Throw)
            {
                StartCoroutine(ExtraUtils.SetValueSmoothAfterADelay(0.5f, (a)=>
                {
                    mAnim.SetLayerWeight(1, a);
                },1f,0.0f,1f));
            }
        }
    }
}
