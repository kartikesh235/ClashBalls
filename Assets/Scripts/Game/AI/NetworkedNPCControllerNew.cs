using Fusion;
using UnityEngine;
using Game.Input;
using BehaviorDesigner.Runtime;
using Game.Ball;
using Game.Character;
using Game.Controllers;

namespace Game.AI
{
    public class NetworkedNPCControllerNew : NetworkBehaviour, IInputService
    {
        [Header("NPC Configuration")]
        [SerializeField] private BehaviorTree behaviorTree;
        [SerializeField] private NPCDifficulty difficulty = NPCDifficulty.Medium;
        
        // Remove SerializeField - these will be retrieved from behavior tree
        private SharedVector2 moveDirection;
        private SharedBool shouldSprint;
        private SharedBool buttonA, buttonB, buttonC, buttonD, buttonE;
        private SharedBool buttonAHeld, buttonAReleased;
        
        // Networked properties - synchronized to all clients
        [Networked] public Vector2 NetworkedMovement { get; private set; }
        [Networked] public bool NetworkedSprint { get; private set; }
        [Networked] public bool NetworkedButtonAPressed { get; private set; }
        [Networked] public bool NetworkedButtonBPressed { get; private set; }
        [Networked] public bool NetworkedButtonCPressed { get; private set; }
        [Networked] public bool NetworkedButtonDPressed { get; private set; }
        [Networked] public bool NetworkedButtonEPressed { get; private set; }
        [Networked] public bool NetworkedButtonAHeld { get; private set; }
        [Networked] public bool NetworkedButtonAReleased { get; private set; }
        [Networked] public bool IsNPC { get; private set; } = true;

        // IInputService implementation - returns networked values
        public Vector2 Movement => NetworkedMovement;
        public bool Sprint => NetworkedSprint;
        public bool ButtonAPressed => NetworkedButtonAPressed;
        public bool ButtonAReleased => NetworkedButtonAReleased;
        public bool ButtonAHeld => NetworkedButtonAHeld;
        public bool ButtonBPressed => NetworkedButtonBPressed;
        public bool ButtonCPressed => NetworkedButtonCPressed;
        public bool ButtonDPressed => NetworkedButtonDPressed;
        public bool ButtonEPressed => NetworkedButtonEPressed;

        // Local state for Host only
        private bool[] localButtonStates = new bool[5];
        private bool localButtonAHeld, localButtonAReleased;

        public override void Spawned()
        {
            // Only initialize behavior tree on Host (StateAuthority)
            if (HasStateAuthority)
            {
                InitializeBehaviorTree();
                SetNPCLayer();
                SetupSharedVariables(); // Add this line
                SetupNPCDifficulty();
                Debug.Log($"NPC spawned on Host: {gameObject.name}");
            }
            else
            {
                // Disable behavior tree on clients
                if (behaviorTree != null)
                    behaviorTree.enabled = false;
                Debug.Log($"NPC spawned on Client: {gameObject.name}");
            }
        }

        private void SetNPCLayer()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
    
            if (enemyLayer != -1)
            {
                SetLayerRecursively(gameObject, enemyLayer);
                Debug.Log($"Set Enemy layer for NPC: {gameObject.name}");
            }
        }

        private void SetLayerRecursively(GameObject obj, int layerIndex)
        {
            obj.layer = layerIndex;
    
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layerIndex);
            }
        }
        private void InitializeBehaviorTree()
        {
            if (behaviorTree == null)
            {
                behaviorTree = GetComponent<BehaviorTree>();
            }

            if (behaviorTree != null)
            {
                behaviorTree.EnableBehavior();
            }
        }

        // NEW METHOD: Connect to behavior tree's shared variables
        private void SetupSharedVariables()
        {
            if (behaviorTree == null) return;

            // Get shared variables from behavior tree
            moveDirection = behaviorTree.GetVariable("MoveDirection") as SharedVector2;
            shouldSprint = behaviorTree.GetVariable("ShouldSprint") as SharedBool;
            buttonA = behaviorTree.GetVariable("ButtonA") as SharedBool;
            buttonB = behaviorTree.GetVariable("ButtonB") as SharedBool;
            buttonC = behaviorTree.GetVariable("ButtonC") as SharedBool;
            buttonD = behaviorTree.GetVariable("ButtonD") as SharedBool;
            buttonE = behaviorTree.GetVariable("ButtonE") as SharedBool;
            buttonAHeld = behaviorTree.GetVariable("ButtonAHeld") as SharedBool;
            buttonAReleased = behaviorTree.GetVariable("ButtonAReleased") as SharedBool;

            // Create variables if they don't exist
            if (moveDirection == null)
            {
                moveDirection = new SharedVector2 { Name = "MoveDirection" };
                behaviorTree.SetVariable("MoveDirection", moveDirection);
            }
            
            if (shouldSprint == null)
            {
                shouldSprint = new SharedBool { Name = "ShouldSprint" };
                behaviorTree.SetVariable("ShouldSprint", shouldSprint);
            }

            // Set NPC reference in behavior tree
            var npcControllerVar = behaviorTree.GetVariable("NPCController") as SharedNPCController;
            if (npcControllerVar == null)
            {
                npcControllerVar = new SharedNPCController { Name = "NPCController" };
                behaviorTree.SetVariable("NPCController", npcControllerVar);
            }
            npcControllerVar.Value = this;

            Debug.Log("Shared variables connected successfully");
        }
        
        // Keep this method for manual initialization if needed
        public void InitializeSharedVariables(
            SharedVector2 moveDir, SharedBool sprint,
            SharedBool btnA, SharedBool btnB, SharedBool btnC, SharedBool btnD, SharedBool btnE,
            SharedBool btnAHeld, SharedBool btnAReleased)
        {
            // Only set up on Host
            if (!HasStateAuthority) return;
            
            moveDirection = moveDir;
            shouldSprint = sprint;
            buttonA = btnA;
            buttonB = btnB;
            buttonC = btnC;
            buttonD = btnD;
            buttonE = btnE;
            buttonAHeld = btnAHeld;
            buttonAReleased = btnAReleased;
        }

        private void SetupNPCDifficulty()
        {
            if (behaviorTree == null) return;

            switch (difficulty)
            {
                case NPCDifficulty.Easy:
                    behaviorTree.SetVariable("ReactionTime", (SharedFloat)0.8f);
                    behaviorTree.SetVariable("Accuracy", (SharedFloat)0.6f);
                    break;
                case NPCDifficulty.Medium:
                    behaviorTree.SetVariable("ReactionTime", (SharedFloat)0.5f);
                    behaviorTree.SetVariable("Accuracy", (SharedFloat)0.8f);
                    break;
                case NPCDifficulty.Hard:
                    behaviorTree.SetVariable("ReactionTime", (SharedFloat)0.2f);
                    behaviorTree.SetVariable("Accuracy", (SharedFloat)0.95f);
                    break;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Only process AI logic on Host
            if (HasStateAuthority)
            {
                ProcessAILogic();
                UpdateNetworkedState();
                ClearOneFrameInputs();
            }
        }

        private void ProcessAILogic()
        {
            if (behaviorTree == null || !behaviorTree.enabled) return;

            // Read from behavior tree shared variables
            UpdateLocalStateFromBehaviorTree();
        }

        private void UpdateLocalStateFromBehaviorTree()
        {
            // Update movement
            if (moveDirection != null)
            {
                NetworkedMovement = moveDirection.Value;
                // Debug log to see if we're getting values
                if (moveDirection.Value != Vector2.zero)
                {
                    Debug.Log($"NPC {gameObject.name}: Movement = {moveDirection.Value}");
                }
            }

            // Update sprint
            if (shouldSprint != null)
                NetworkedSprint = shouldSprint.Value;

            // Update button states (one-frame only)
            if (buttonA != null && buttonA.Value) 
            {
                localButtonStates[0] = true;
                buttonA.Value = false; // Reset immediately
            }
            
            if (buttonB != null && buttonB.Value) 
            {
                localButtonStates[1] = true;
                buttonB.Value = false;
            }
            
            if (buttonC != null && buttonC.Value) 
            {
                localButtonStates[2] = true;
                buttonC.Value = false;
            }
            
            if (buttonD != null && buttonD.Value) 
            {
                localButtonStates[3] = true;
                buttonD.Value = false;
            }
            
            if (buttonE != null && buttonE.Value) 
            {
                localButtonStates[4] = true;
                buttonE.Value = false;
            }

            // Handle held and released states
            if (buttonAHeld != null) localButtonAHeld = buttonAHeld.Value;
            
            if (buttonAReleased != null && buttonAReleased.Value) 
            {
                localButtonAReleased = true;
                buttonAReleased.Value = false;
            }
        }

        private void UpdateNetworkedState()
        {
            // Update networked properties that will be sent to clients
            NetworkedButtonAPressed = localButtonStates[0];
            NetworkedButtonBPressed = localButtonStates[1];
            NetworkedButtonCPressed = localButtonStates[2];
            NetworkedButtonDPressed = localButtonStates[3];
            NetworkedButtonEPressed = localButtonStates[4];
            NetworkedButtonAHeld = localButtonAHeld;
            NetworkedButtonAReleased = localButtonAReleased;
        }

        private void ClearOneFrameInputs()
        {
            // Clear one-frame inputs after they've been processed
            for (int i = 0; i < localButtonStates.Length; i++)
            {
                localButtonStates[i] = false;
            }
            localButtonAReleased = false;
        }

        public void SetDifficulty(NPCDifficulty newDifficulty)
        {
            difficulty = newDifficulty;
            if (behaviorTree != null && HasStateAuthority)
            {
                SetupNPCDifficulty();
            }
        }

        // Helper methods for Behavior Designer tasks - only work on Host
        public Vector3 GetClosestBallPosition()
        {
            if (!HasStateAuthority) return transform.position;
            
            #if UNITY_2022_2_OR_NEWER
            var balls = UnityEngine.Object.FindObjectsByType<BallController>(FindObjectsSortMode.None);
            #else
            var balls = UnityEngine.Object.FindObjectsOfType<BallController>();
            #endif
            
            if (balls.Length == 0) return transform.position;

            var closest = balls[0];
            float minDistance = Vector3.Distance(transform.position, closest.transform.position);

            foreach (var ball in balls)
            {
                if (ball == null) continue;
                
                float distance = Vector3.Distance(transform.position, ball.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = ball;
                }
            }

            return closest.transform.position;
        }

        public Vector3 GetClosestPlayerPosition()
        {
            if (!HasStateAuthority) return transform.position;
            
            #if UNITY_2022_2_OR_NEWER
            var players = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            #else
            var players = UnityEngine.Object.FindObjectsOfType<PlayerController>();
            #endif
            
            Vector3 closestPos = transform.position;
            float minDistance = float.MaxValue;

            foreach (var player in players)
            {
                if (player == null || player.gameObject == gameObject) continue;

                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPos = player.transform.position;
                }
            }

            return closestPos;
        }

        // Debug method for Host
        private void OnGUI()
        {
            if (!HasStateAuthority || !Application.isPlaying) return;
    
            GUILayout.BeginArea(new Rect(10, 10, 400, 180));
            GUILayout.Label($"=== NPC {Object.Id} Debug ===");
            GUILayout.Label($"NetworkedMovement: {NetworkedMovement}");
            GUILayout.Label($"MoveDirection Shared: {moveDirection?.Value ?? Vector2.zero}");
            GUILayout.Label($"Sprint: {NetworkedSprint}");
    
            // Check if PlayerMovement exists and is enabled
            var playerMovement = GetComponent<PlayerMovement>();
            GUILayout.Label($"PlayerMovement: {(playerMovement != null ? "EXISTS" : "MISSING")}");
            GUILayout.Label($"PlayerMovement Enabled: {(playerMovement != null ? playerMovement.enabled.ToString() : "N/A")}");
    
            // Check if PlayerController exists
            var playerController = GetComponent<PlayerController>();
            GUILayout.Label($"PlayerController: {(playerController != null ? "EXISTS" : "MISSING")}");
    
            GUILayout.EndArea();
        }
    }

    public enum NPCDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    [System.Serializable]
    public class SharedNPCController : SharedVariable<NetworkedNPCControllerNew>
    {
        public static implicit operator SharedNPCController(NetworkedNPCControllerNew value) 
        { 
            return new SharedNPCController { Value = value }; 
        }
    }
}