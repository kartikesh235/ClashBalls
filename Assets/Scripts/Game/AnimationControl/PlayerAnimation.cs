using Fusion;
using Game.AI;
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
        
        [SerializeField] private Animator mAnim;
        private IInputService mInput; // Changed from MmInputService to IInputService

        [Networked]
        private PlayerAnimState NetworkedAnimState { get; set; }
        private PlayerAnimState mLastAnimState = PlayerAnimState.Locomotion;

        public override void Spawned()
        {
            // Get input service (either human or NPC) - PRIORITY: NPC first, then human
            mInput = GetComponent<NetworkedNPCControllerNew>() as IInputService ?? 
                     GetComponent<MmInputService>() as IInputService;
                     
            if (mInput == null)
            {
                Debug.LogError($"PlayerAnimation {gameObject.name}: No input service found!");
            }
            else
            {
                bool isNPC = mInput is NetworkedNPCControllerNew;
                Debug.Log($"PlayerAnimation {gameObject.name}: Using {mInput.GetType().Name} (IsNPC: {isNPC})");
            }
            
            mLastAnimState = NetworkedAnimState;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (NetworkedAnimState == PlayerAnimState.Locomotion && mInput != null)
                {
                    // Debug logging for NPCs
                    if (mInput is NetworkedNPCControllerNew && mInput.Movement != Vector2.zero)
                    {
                        Debug.Log($"NPC Animation {gameObject.name}: Movement={mInput.Movement}, Sprint={mInput.Sprint}");
                    }
                    
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
                else if (mInput == null)
                {
                    Debug.LogWarning($"PlayerAnimation {gameObject.name}: Input service is null!");
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
            if (mAnim == null)
            {
                Debug.LogError($"PlayerAnimation {gameObject.name}: Animator is null!");
                return;
            }
            
            // Debug for NPCs
            if (mInput is NetworkedNPCControllerNew && (joystickX != 0 || joystickY != 0))
            {
                Debug.Log($"NPC {gameObject.name}: Setting animation JoystickX={joystickX}, JoystickY={joystickY}");
            }
            
            mAnim.SetFloat(JoystickX, joystickX);
            mAnim.SetFloat(JoystickY, joystickY);
        }
        
        public void SetHandSubState(HandSubState state)
        {
            if (mAnim == null) return;
            
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
