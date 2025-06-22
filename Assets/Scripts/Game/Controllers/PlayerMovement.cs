using Fusion;
using Fusion.Addons.Physics;
using Game.AI;
using UnityEngine;
using Game.Input;
using Game.Character;

namespace Game.Controllers
{
    [RequireComponent(typeof(NetworkRigidbody3D))]
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private CharacterTypeSO characterType;
        private IInputService inputService;
        private NetworkRigidbody3D _rb;

        private void OnDisable()
        {
            Debug.LogError($"PlayerMovement disabled on {gameObject.name}");
        }
        public override void Spawned()
        {
            _rb = GetComponent<NetworkRigidbody3D>();
            
            // Get input service (either human or NPC)
            inputService = GetComponent<MmInputService>() as IInputService ?? 
                          GetComponent<NetworkedNPCControllerNew>() as IInputService;
                          
            if (inputService == null)
            {
                Debug.LogError($"PlayerMovement {gameObject.name}: No input service found!");
            }
            else
            {
                bool isNPC = inputService is NetworkedNPCControllerNew;
                Debug.Log($"PlayerMovement {gameObject.name}: Using {inputService.GetType().Name} (IsNPC: {isNPC})");
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            
            if (inputService == null)
            {
                Debug.LogWarning($"PlayerMovement {gameObject.name}: Input service is null!");
                return;
            }

            Vector3 dir = new Vector3(inputService.Movement.x, 0f, inputService.Movement.y).normalized;
            float speed = inputService.Sprint ? characterType.sprintSpeed : characterType.moveSpeed;
            
            // Debug log for NPCs
            if (inputService is NetworkedNPCControllerNew && inputService.Movement != Vector2.zero)
            {
                Debug.Log($"NPC {gameObject.name}: Movement={inputService.Movement}, Dir={dir}, Speed={speed}");
            }
            
            _rb.Rigidbody.linearVelocity = new Vector3(dir.x * speed, _rb.Rigidbody.linearVelocity.y, dir.z * speed);

            if (dir.sqrMagnitude > 0.01f)
                transform.forward = dir;
        }
    }
}