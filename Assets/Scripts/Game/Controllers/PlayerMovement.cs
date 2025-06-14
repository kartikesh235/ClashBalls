using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using Game.Input;
using Game.Character;

namespace Game.Controllers
{
    [RequireComponent(typeof(NetworkRigidbody3D))]
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private CharacterTypeSO characterType;
        [SerializeField] private MmInputService inputService;

        private NetworkRigidbody3D _rb;

        private void OnDisable()
        {
            Debug.LogError("PlayerMovement disabled");
        }
        public override void Spawned()
        {
            _rb = GetComponent<NetworkRigidbody3D>();
            inputService = GetComponent<MmInputService>();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            Vector3 dir = new Vector3(inputService.Movement.x, 0f, inputService.Movement.y).normalized;
            float speed = inputService.Sprint ? characterType.sprintSpeed : characterType.moveSpeed;
            _rb.Rigidbody.linearVelocity = new Vector3(dir.x * speed, _rb.Rigidbody.linearVelocity.y, dir.z * speed);

            if (dir.sqrMagnitude > 0.01f)
                transform.forward = dir;
        }
    }
}