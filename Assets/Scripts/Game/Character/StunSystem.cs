using System.Collections;
using UnityEngine;
using Fusion;
using Game.AnimationControl;
using Game.Controllers;

namespace Game.Character
{
    public class StunSystem : NetworkBehaviour
    {
        [Header("Stun VFX")]
        public GameObject stunVFX;
        public Transform stunVFXParent; // Usually above character head
        
        [Networked] public bool IsStunned { get; private set; }
        [Networked] public float StunTimeRemaining { get; private set; }
        
        private PlayerMovement mMovement;
        private PlayerAnimation mAnimation;
        private PlayerController mPlayerController;
        private Coroutine mStunCoroutine;

        public override void Spawned()
        {
            mMovement = GetComponent<PlayerMovement>();
            mAnimation = GetComponent<PlayerAnimation>();
            mPlayerController = GetComponent<PlayerController>();
            
            if (stunVFX != null)
                stunVFX.SetActive(false);
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority && IsStunned)
            {
                StunTimeRemaining -= Runner.DeltaTime;
                
                if (StunTimeRemaining <= 0)
                {
                    EndStun();
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ApplyStun(float duration)
        {
            if (IsStunned) return; // Already stunned
            
            StartStun(duration);
        }

        private void StartStun(float duration)
        {
            IsStunned = true;
            StunTimeRemaining = duration;
            
            // Disable movement
            if (mMovement != null)
                mMovement.enabled = false;
            
            // Disable all abilities
            DisableAbilities();
            
            // Force idle animation
            if (mAnimation != null)
                mAnimation.SetStunned(true);
            
            // Show stun VFX
            if (stunVFX != null)
                stunVFX.SetActive(true);
            
            Debug.Log($"{gameObject.name} stunned for {duration} seconds");
        }

        private void EndStun()
        {
            IsStunned = false;
            StunTimeRemaining = 0f;
            
            // Re-enable movement
            if (mMovement != null)
                mMovement.enabled = true;
            
            // Re-enable abilities
            EnableAbilities();
            
            // Release animation lock
            if (mAnimation != null)
                mAnimation.SetStunned(false);
            
            // Hide stun VFX
            if (stunVFX != null)
                stunVFX.SetActive(false);
            
            Debug.Log($"{gameObject.name} stun ended");
        }

        private void DisableAbilities()
        {
            var abilities = GetComponents<Game.Abilities.BaseAbility>();
            foreach (var ability in abilities)
            {
                ability.enabled = false;
            }
        }

        private void EnableAbilities()
        {
            var abilities = GetComponents<Game.Abilities.BaseAbility>();
            foreach (var ability in abilities)
            {
                ability.enabled = true;
            }
        }

        public void ApplyStun(float duration)
        {
            if (HasStateAuthority)
            {
                RPC_ApplyStun(duration);
            }
        }
        public void ForceEndStun()
        {
            if (HasStateAuthority)
            {
                EndStun();
            }
        }
    }
}