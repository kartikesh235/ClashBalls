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
        Parry = 3,
        Tackle = 4,
        Stunned = 5  // Add stunned state
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
        private IInputService mInput;

        [Networked] private PlayerAnimState NetworkedAnimState { get; set; }
        [Networked] private bool IsStunnedState { get; set; }
        
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
                // If stunned, force stunned animation and ignore all input
                if (IsStunnedState)
                {
                    if (NetworkedAnimState != PlayerAnimState.Stunned)
                    {
                        NetworkedAnimState = PlayerAnimState.Stunned;
                    }
                    
                    // Force idle/stunned pose
                    SetLocomotionState(0f, 0f);
                    mAnim.SetFloat(RunMultiplier, 0f);
                }
                else if (NetworkedAnimState == PlayerAnimState.Locomotion && mInput != null)
                {
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
            // Don't allow state changes if stunned
            if (IsStunnedState && state != PlayerAnimState.Stunned) 
                return;
                
            if (NetworkedAnimState == state) 
                return;
                
            NetworkedAnimState = state;
        }

        public void SetStunned(bool stunned)
        {
            IsStunnedState = stunned;
            
            if (stunned)
            {
                NetworkedAnimState = PlayerAnimState.Stunned;
            }
            else
            {
                NetworkedAnimState = PlayerAnimState.Locomotion;
            }
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
            mAnim.SetInteger(HandState, (int)state);
            if (state == HandSubState.Throw)
            {
                mAnim.SetLayerWeight(1, 1.0f);
                StartCoroutine(ExtraUtils.SetDelay(0.5f/1.5f, () =>
                {
                    StartCoroutine(ExtraUtils.SetValueSmooth( (a)=>
                    {
                        mAnim.SetLayerWeight(1, a);
                    },1f,0.0f,0.2f));
                }));
               
            }
            else if (state == HandSubState.Pickup)
            {
                mAnim.SetLayerWeight(1, 1f);
                StartCoroutine(ExtraUtils.SetDelay(2f/3f, () =>
                {
                    StartCoroutine(ExtraUtils.SetValueSmooth( (a)=>
                    {
                        mAnim.SetLayerWeight(1, a);
                    },1f,0.0f,0.2f));
                }));
            }
        }
    }
}
