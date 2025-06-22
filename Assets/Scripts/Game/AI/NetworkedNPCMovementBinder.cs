using UnityEngine;
using BehaviorDesigner.Runtime;
using Game.AI;
using Fusion;

namespace Game.AI
{
    /// <summary>
    /// Networked version of NPCMovementBinder that only operates on Host
    /// </summary>
    public class NetworkedNPCMovementBinder : NetworkBehaviour
    {
        [Header("Behavior Tree References")]
        [SerializeField] private BehaviorTree mBehaviorTree;
        [SerializeField] private NetworkedNPCControllerNew mNPCControllerNew;
        
        [Header("Shared Variable Names")]
        [SerializeField] private string mMoveDirectionVarName = "MoveDirection";
        [SerializeField] private string mShouldSprintVarName = "ShouldSprint";
        [SerializeField] private string mButtonAVarName = "ButtonA";
        [SerializeField] private string mButtonBVarName = "ButtonB";
        [SerializeField] private string mButtonCVarName = "ButtonC";
        [SerializeField] private string mButtonDVarName = "ButtonD";
        [SerializeField] private string mButtonEVarName = "ButtonE";
        [SerializeField] private string mButtonAHeldVarName = "ButtonAHeld";
        [SerializeField] private string mButtonAReleasedVarName = "ButtonAReleased";
        
        // Cached shared variables for performance
        private SharedVector2 mMoveDirection;
        private SharedBool mShouldSprint;
        private SharedBool mButtonA, mButtonB, mButtonC, mButtonD, mButtonE;
        private SharedBool mButtonAHeld, mButtonAReleased;
        
        private void Awake()
        {
            if (mBehaviorTree == null)
                mBehaviorTree = GetComponent<BehaviorTree>();
                
            if (mNPCControllerNew == null)
                mNPCControllerNew = GetComponent<NetworkedNPCControllerNew>();
        }
        
        public override void Spawned()
        {
            // Only setup on Host
            if (HasStateAuthority)
            {
                SetupSharedVariables();
                Debug.Log($"NPCMovementBinder initialized on Host for {gameObject.name}");
            }
            else
            {
                // Disable this component on clients
                enabled = false;
                Debug.Log($"NPCMovementBinder disabled on Client for {gameObject.name}");
            }
        }
        
        private void SetupSharedVariables()
        {
            if (mBehaviorTree == null || !HasStateAuthority) return;
            
            // Get or create shared variables
            mMoveDirection = GetOrCreateSharedVariable<SharedVector2>(mMoveDirectionVarName);
            mShouldSprint = GetOrCreateSharedVariable<SharedBool>(mShouldSprintVarName);
            mButtonA = GetOrCreateSharedVariable<SharedBool>(mButtonAVarName);
            mButtonB = GetOrCreateSharedVariable<SharedBool>(mButtonBVarName);
            mButtonC = GetOrCreateSharedVariable<SharedBool>(mButtonCVarName);
            mButtonD = GetOrCreateSharedVariable<SharedBool>(mButtonDVarName);
            mButtonE = GetOrCreateSharedVariable<SharedBool>(mButtonEVarName);
            mButtonAHeld = GetOrCreateSharedVariable<SharedBool>(mButtonAHeldVarName);
            mButtonAReleased = GetOrCreateSharedVariable<SharedBool>(mButtonAReleasedVarName);
            
            // Set reference to NPC Controller in behavior tree
            var npcControllerVar = GetOrCreateSharedVariable<SharedNPCController>("NPCController");
            npcControllerVar.Value = mNPCControllerNew;
            
            // Initialize NPC Controller with shared variables
            mNPCControllerNew.InitializeSharedVariables(
                mMoveDirection, mShouldSprint,
                mButtonA, mButtonB, mButtonC, mButtonD, mButtonE,
                mButtonAHeld, mButtonAReleased
            );
            
            Debug.Log($"Shared variables setup complete for NPC: {gameObject.name}");
        }
        
        private T GetOrCreateSharedVariable<T>(string variableName) where T : SharedVariable, new()
        {
            var variable = mBehaviorTree.GetVariable(variableName) as T;
            if (variable == null)
            {
                variable = new T { Name = variableName };
                mBehaviorTree.SetVariable(variableName, variable);
                Debug.Log($"Created shared variable: {variableName} ({typeof(T).Name})");
            }
            return variable;
        }
        
        /// <summary>
        /// Manual control methods - only work on Host
        /// </summary>
        public void SetMovement(Vector2 movement, bool sprint = false)
        {
            if (!HasStateAuthority) return;
            
            if (mMoveDirection != null)
                mMoveDirection.Value = movement;
                
            if (mShouldSprint != null)
                mShouldSprint.Value = sprint;
        }
        
        public void PressButton(int buttonIndex)
        {
            if (!HasStateAuthority) return;
            
            switch (buttonIndex)
            {
                case 0: if (mButtonA != null) mButtonA.Value = true; break;
                case 1: if (mButtonB != null) mButtonB.Value = true; break;
                case 2: if (mButtonC != null) mButtonC.Value = true; break;
                case 3: if (mButtonD != null) mButtonD.Value = true; break;
                case 4: if (mButtonE != null) mButtonE.Value = true; break;
            }
        }
        
        public void SetButtonAHeld(bool held)
        {
            if (!HasStateAuthority) return;
            
            if (mButtonAHeld != null)
                mButtonAHeld.Value = held;
        }
        
        public void ReleaseButtonA()
        {
            if (!HasStateAuthority) return;
            
            if (mButtonAReleased != null)
                mButtonAReleased.Value = true;
        }
    }
}