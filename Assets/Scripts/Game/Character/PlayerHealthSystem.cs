using System.Collections;
using UnityEngine;
using Game.Character;
using Game.GameUI;
using Game.Abilities;
using Game.Controllers;
using Game.AI;
using Game.Input;
using Fusion;
using Fusion.Addons.Physics;

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
        
        [Header("Fallback Spawn")]
        public Vector3 fallbackSpawnPosition = Vector3.zero;
        
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
        private MmInputService mHumanInputService;
        private BehaviorDesigner.Runtime.BehaviorTree mBehaviorTree;
        private NetworkRigidbody3D mNetworkRigidbody;
        private Vector3 mOriginalPosition;
        private Coroutine mHealingCoroutine;
        private Coroutine mRespawnParryCoroutine;
        private bool mWasNPCDumbBeforeHealing;
        private Transform[] mCachedSpawnPoints;
        
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
            mHumanInputService = GetComponent<MmInputService>();
            mBehaviorTree = GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
            mNetworkRigidbody = GetComponent<NetworkRigidbody3D>();
            
            InitializeSpawnSystem();
        }

        private void InitializeSpawnSystem()
        {
            // Cache spawn points on spawn
            StartCoroutine(CacheSpawnPointsWithRetry());
            
            // Find healing position
            if (healingPosition == null)
            {
                var healingZone = GameObject.FindGameObjectWithTag("HealingZone");
                if (healingZone != null)
                    healingPosition = healingZone.transform;
            }
        }

        private IEnumerator CacheSpawnPointsWithRetry()
        {
            int retryCount = 0;
            const int maxRetries = 10;
            
            while (mCachedSpawnPoints == null || mCachedSpawnPoints.Length == 0)
            {
                var spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");
                
                if (spawnPointObjects.Length > 0)
                {
                    mCachedSpawnPoints = new Transform[spawnPointObjects.Length];
                    for (int i = 0; i < spawnPointObjects.Length; i++)
                    {
                        mCachedSpawnPoints[i] = spawnPointObjects[i].transform;
                    }
                    
                    Debug.Log($"Cached {mCachedSpawnPoints.Length} spawn points for {gameObject.name}");
                    break;
                }
                
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    Debug.LogWarning($"Failed to find spawn points after {maxRetries} retries. Using fallback position.");
                    break;
                }
                
                yield return new WaitForSeconds(0.5f);
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
            
            // Only StateAuthority should handle position and healing logic
            if (HasStateAuthority)
            {
                TeleportToHealingZone();
                mHealingCoroutine = StartCoroutine(HealingProcess());
            }
        }

        private void DisablePlayerControl()
        {
            // Store NPC state before disabling
            if (mNPCController != null)
            {
                mWasNPCDumbBeforeHealing = mNPCController.IsDumbNPC;
                mNPCController.IsDumbNPC = true; // Make NPC dumb to stop all AI
            }
            
            // Disable movement
            if (mMovement != null) 
                mMovement.enabled = false;
            
            // Disable human input service
            if (mHumanInputService != null)
            {
                mHumanInputService.enabled = false;
            }
            
            // Disable abilities
            DisableAllAbilities();
            
            // Disable UI buttons for local player
            if (Object.HasInputAuthority && !mPlayerController.IsNPC())
            {
                RPC_DisableUIButtons();
            }
            
            // Clear rigidbody velocity on state authority
            if (HasStateAuthority)
            {
                ClearRigidbodyVelocity();
            }
        }

        private void EnablePlayerControl()
        {
            // Re-enable movement
            if (mMovement != null) 
                mMovement.enabled = true;
            
            // Re-enable human input service
            if (mHumanInputService != null)
            {
                mHumanInputService.enabled = true;
            }
            
            // Restore NPC state and re-enable AI
            if (mNPCController != null)
            {
                mNPCController.IsDumbNPC = mWasNPCDumbBeforeHealing;
                
                // Force restart behavior tree if it's an NPC
                if (mBehaviorTree != null && !mWasNPCDumbBeforeHealing)
                {
                    StartCoroutine(RestartNPCBehaviorTree());
                }
            }
            
            // Re-enable abilities
            EnableAllAbilities();
            
            // Re-enable UI buttons for local player
            if (Object.HasInputAuthority && !mPlayerController.IsNPC())
            {
                RPC_EnableUIButtons();
            }
        }

        private IEnumerator RestartNPCBehaviorTree()
        {
            // Wait a frame to ensure position is properly set
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            
            if (mBehaviorTree != null)
            {
                mBehaviorTree.enabled = false;
                yield return new WaitForFixedUpdate();
                mBehaviorTree.enabled = true;
                mBehaviorTree.EnableBehavior();
                
                Debug.Log($"Restarted behavior tree for NPC: {gameObject.name}");
            }
        }

        private void TeleportToHealingZone()
        {
            if (healingPosition != null && HasStateAuthority)
            {
                Vector3 targetPos = healingPosition.position;
                Quaternion targetRot = healingPosition.rotation;
                
                // Immediate position set for StateAuthority
                transform.position = targetPos;
                transform.rotation = targetRot;
                ClearRigidbodyVelocity();
                
                // RPC to sync position to all clients
                RPC_SyncPosition(targetPos, targetRot);
                
                Debug.Log($"Teleported {gameObject.name} to healing zone: {targetPos}");
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        private void RPC_SyncPosition(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        private void ClearRigidbodyVelocity()
        {
            if (!HasStateAuthority) return;
            
            // Clear NetworkRigidbody3D if available
            if (mNetworkRigidbody != null && mNetworkRigidbody.Rigidbody != null)
            {
                mNetworkRigidbody.Rigidbody.linearVelocity = Vector3.zero;
                mNetworkRigidbody.Rigidbody.angularVelocity = Vector3.zero;
            }
            
            // Fallback to regular Rigidbody
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_DisableUIButtons()
        {
            if (Game2DUI.Instance != null)
            {
                Game2DUI.Instance.joystick.gameObject.SetActive(false);
                Game2DUI.Instance.buttonA.interactable = false;
                Game2DUI.Instance.buttonB.interactable = false;
                Game2DUI.Instance.buttonC.interactable = false;
                Game2DUI.Instance.buttonD.interactable = false;
                Game2DUI.Instance.buttonE.interactable = false;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_EnableUIButtons()
        {
            if (Game2DUI.Instance != null)
            {
                Game2DUI.Instance.joystick.gameObject.SetActive(true);
                Game2DUI.Instance.buttonA.interactable = true;
                Game2DUI.Instance.buttonB.interactable = true;
                Game2DUI.Instance.buttonC.interactable = true;
                Game2DUI.Instance.buttonD.interactable = true;
                Game2DUI.Instance.buttonE.interactable = true;
            }
        }

        private IEnumerator HealingProcess()
        {
            float healTime = healingDuration;
            
            // Show healing effects
            if (mUI3D != null)
                mUI3D.ShowHealingEffects(true);
            
            // Continuously clear velocity during healing to prevent drift
            while (healTime > 0)
            {
                // Clear velocity every few frames to prevent any movement
                if (Runner.Tick % 10 == 0)
                {
                    ClearRigidbodyVelocity();
                }
                
                // Update countdown
                if (mUI3D != null)
                    mUI3D.ShowHealCountdown(healTime);
                
                healTime -= Time.deltaTime;
                yield return null;
            }
            
            // Add delay before respawn to ensure healing is complete
            yield return new WaitForSeconds(0.1f);
            
            RPC_CompleteHealing();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_CompleteHealing()
        {
            StartCoroutine(CompleteHealingDelayed());
        }

        private IEnumerator CompleteHealingDelayed()
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
            
            // Wait a frame before respawning
            yield return new WaitForFixedUpdate();
            
            // Respawn at spawn point (only on StateAuthority)
            if (HasStateAuthority)
            {
                bool respawnSuccess = RespawnAtSpawnPoint();
                
                if (!respawnSuccess)
                {
                    Debug.LogError($"Failed to respawn {gameObject.name}, trying fallback");
                    RespawnAtFallbackPosition();
                }
            }
            
            // Wait another frame before enabling control
            yield return new WaitForFixedUpdate();
            
            EnablePlayerControl();
            StartRespawnParry();
        }

        private bool RespawnAtSpawnPoint()
        {
            if (!HasStateAuthority) return false;
            
            // Try cached spawn points first
            if (mCachedSpawnPoints != null && mCachedSpawnPoints.Length > 0)
            {
                var validSpawnPoints = new System.Collections.Generic.List<Transform>();
                
                // Filter out null spawn points
                foreach (var spawnPoint in mCachedSpawnPoints)
                {
                    if (spawnPoint != null)
                        validSpawnPoints.Add(spawnPoint);
                }
                
                if (validSpawnPoints.Count > 0)
                {
                    var randomSpawn = validSpawnPoints[Random.Range(0, validSpawnPoints.Count)];
                    SetRespawnPosition(randomSpawn.position, randomSpawn.rotation);
                    
                    Debug.Log($"Respawned {gameObject.name} at cached spawn point: {randomSpawn.position}");
                    return true;
                }
            }
            
            // Fallback: Search for spawn points again
            var spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");
            if (spawnPointObjects.Length > 0)
            {
                var randomSpawn = spawnPointObjects[Random.Range(0, spawnPointObjects.Length)];
                SetRespawnPosition(randomSpawn.transform.position, randomSpawn.transform.rotation);
                
                Debug.Log($"Respawned {gameObject.name} at fresh spawn point: {randomSpawn.transform.position}");
                return true;
            }
            
            return false;
        }

        private void RespawnAtFallbackPosition()
        {
            if (!HasStateAuthority) return;
            
            // Use fallback position with slight randomization
            Vector3 fallbackPos = fallbackSpawnPosition + new Vector3(
                Random.Range(-2f, 2f), 
                0, 
                Random.Range(-2f, 2f)
            );
            
            SetRespawnPosition(fallbackPos, Quaternion.identity);
            Debug.LogWarning($"Used fallback respawn for {gameObject.name} at: {fallbackPos}");
        }

        private void SetRespawnPosition(Vector3 position, Quaternion rotation)
        {
            // Immediate position set for StateAuthority
            transform.position = position;
            transform.rotation = rotation;
            ClearRigidbodyVelocity();
            
            // RPC to sync position to all clients
            RPC_SyncPosition(position, rotation);
            
            // Validate position was set
            StartCoroutine(ValidateRespawnPosition(position));
        }

        private IEnumerator ValidateRespawnPosition(Vector3 expectedPosition)
        {
            yield return new WaitForFixedUpdate();
            
            float distance = Vector3.Distance(transform.position, expectedPosition);
            if (distance > 1f)
            {
                Debug.LogWarning($"Respawn position validation failed for {gameObject.name}. Expected: {expectedPosition}, Actual: {transform.position}, Distance: {distance}");
                
                // Try to correct position one more time
                if (HasStateAuthority)
                {
                    transform.position = expectedPosition;
                    ClearRigidbodyVelocity();
                    RPC_SyncPosition(expectedPosition, transform.rotation);
                }
            }
            else
            {
                Debug.Log($"Respawn position validated for {gameObject.name} at: {transform.position}");
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