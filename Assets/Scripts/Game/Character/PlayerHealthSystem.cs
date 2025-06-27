using System.Collections;
using UnityEngine;
using Game.Character;
using Game.GameUI;
using Game.Abilities;
using Game.Controllers;
using Game.AI;
using Fusion;

namespace Game.Character
{
    public class PlayerHealthSystem : NetworkBehaviour
    {
        [Header("Health Settings")]
        public float maxHealth = 100f;
        public float healingDuration = 5f;
        public float respawnParryDuration = 2f;
        
        [Header("Healing Position")]
        public Transform healingPosition;
        
        [Networked] public float CurrentHealth { get; private set; }
        [Networked] public bool IsHealing { get; private set; }
        [Networked] public bool HasRespawnParry { get; private set; }
        
        public float HealthRatio => CurrentHealth / maxHealth;
        
        private CharacterStats mStats;
        private Game3DUI mUI3D;
        private PlayerMovement mMovement;
        private ParryAbility mParryAbility;
        private PlayerController mPlayerController;
        private NetworkedNPCControllerNew mNPCController;
        private BehaviorDesigner.Runtime.BehaviorTree mBehaviorTree;
        private Vector3 mOriginalPosition;
        private Coroutine mHealingCoroutine;
        private Coroutine mRespawnParryCoroutine;
        
        public delegate void PlayerDefeatedDelegate(PlayerController attacker, PlayerController victim);
        public static event PlayerDefeatedDelegate OnPlayerDefeated;

        public override void Spawned()
        {
            CurrentHealth = maxHealth;
            mStats = GetComponent<CharacterStats>();
            mUI3D = GetComponentInChildren<Game3DUI>();
            mMovement = GetComponent<PlayerMovement>();
            mParryAbility = GetComponent<ParryAbility>();
            mPlayerController = GetComponent<PlayerController>();
            mNPCController = GetComponent<NetworkedNPCControllerNew>();
            mBehaviorTree = GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
            
            if (healingPosition == null)
            {
                var healingZone = GameObject.FindGameObjectWithTag("HealingZone");
                if (healingZone != null)
                    healingPosition = healingZone.transform;
            }
        }

        public void TakeDamage(float damage, PlayerController attacker = null)
        {
            if (!HasStateAuthority || IsHealing || HasRespawnParry) return;
            
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(0, CurrentHealth);
            UpdateHealthUI();
            if (CurrentHealth <= 0)
            {
                NetworkId attackerId = attacker != null ? attacker.Object.Id : default(NetworkId);
                RPC_StartHealing(attackerId);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_StartHealing(NetworkId attackerId)
        {
            if (IsHealing) return;
            
            PlayerController attacker = null;
            if (attackerId.IsValid)
            {
                var attackerObj = Runner.FindObject(attackerId);
                if (attackerObj != null)
                    attacker = attackerObj.GetComponent<PlayerController>();
            }
            
            StartHealing(attacker);
        }

        private void StartHealing(PlayerController attacker)
        {
            if (IsHealing) return;
            
            IsHealing = true;
            mOriginalPosition = transform.position;
            
            if (attacker != null)
            {
                OnPlayerDefeated?.Invoke(attacker, mPlayerController);
            }
            
            DisablePlayerControl();
            TeleportToHealingZone();
            
            if (HasStateAuthority)
            {
                mHealingCoroutine = StartCoroutine(HealingProcess());
            }
        }

        private void DisablePlayerControl()
        {
            // Disable movement
            if (mMovement != null) 
                mMovement.enabled = false;
            
            // Disable NPC behavior tree
            if (mBehaviorTree != null)
                mBehaviorTree.enabled = false;
            
            // Disable abilities
            DisableAllAbilities();
            
            // Stop NPC input
            if (mNPCController != null)
            {
                mNPCController.enabled = false;
            }
        }

        private void EnablePlayerControl()
        {
            // Re-enable movement
            if (mMovement != null) 
                mMovement.enabled = true;
            
            // Re-enable NPC behavior tree
            if (mBehaviorTree != null)
                mBehaviorTree.enabled = true;
            
            // Re-enable abilities
            EnableAllAbilities();
            
            // Re-enable NPC input
            if (mNPCController != null)
            {
                mNPCController.enabled = true;
            }
        }

        private void TeleportToHealingZone()
        {
            if (healingPosition != null)
            {
                transform.position = healingPosition.position;
                transform.rotation = healingPosition.rotation;
                
                // Clear any residual velocity
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        private IEnumerator HealingProcess()
        {
            float healTime = healingDuration;
            
            // Show healing effects
            if (mUI3D != null)
                mUI3D.ShowHealingEffects(true);
            
            while (healTime > 0)
            {
                // Update countdown
                if (mUI3D != null)
                    mUI3D.ShowHealCountdown(healTime);
                
                healTime -= Time.deltaTime;
                yield return null;
            }
            
            RPC_CompleteHealing();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_CompleteHealing()
        {
            CompleteHealing();
        }

        private void CompleteHealing()
        {
            IsHealing = false;
            CurrentHealth = maxHealth;
            UpdateHealthUI();
            // Hide healing effects
            if (mUI3D != null)
            {
                mUI3D.ShowHealingEffects(false);
                mUI3D.HideHealCountdown();
            }
            
            // Respawn at spawn point
            RespawnAtSpawnPoint();
            EnablePlayerControl();
            StartRespawnParry();
        }

        private void RespawnAtSpawnPoint()
        {
            var spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
            if (spawnPoints.Length > 0)
            {
                var randomSpawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
                transform.position = randomSpawn.transform.position;
                transform.rotation = randomSpawn.transform.rotation;
                
                // Clear any residual velocity after respawn
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        private void StartRespawnParry()
        {
            HasRespawnParry = true;
            if (HasStateAuthority)
            {
                mRespawnParryCoroutine = StartCoroutine(RespawnParryDuration());
            }
        }

        private IEnumerator RespawnParryDuration()
        {
            yield return new WaitForSeconds(respawnParryDuration);
            HasRespawnParry = false;
        }

        private void DisableAllAbilities()
        {
            var abilities = GetComponents<BaseAbility>();
            foreach (var ability in abilities)
            {
                ability.enabled = false;
            }
        }

        private void EnableAllAbilities()
        {
            var abilities = GetComponents<BaseAbility>();
            foreach (var ability in abilities)
            {
                ability.enabled = true;
            }
        }

        public bool CanTakeDamage()
        {
            return !IsHealing && !HasRespawnParry;
        }
        private void UpdateHealthUI()
        {
            if (Game2DUI.Instance != null)
            {
                // Check if this is the local player
                if (Object != null && Object.HasInputAuthority && !mPlayerController.IsNPC())
                {
                    Game2DUI.Instance.UpdateLocalPlayerHealth(CurrentHealth, maxHealth);
                }
            }
        }
    }
}