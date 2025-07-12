using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Character;
using Game.GameUI;
using Game.Abilities;
using Game.Controllers;
using Game.AI;
using Game.Input;
using Game.Ball;
using Game.AnimationControl;
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
        public float deathStunDuration = 1.5f; // Time to play death animation before healing
        
        [Header("Fall Detection")]
        public float fallThreshold = -2f;
        public float fallCheckInterval = 0.5f;
        
        [Header("Healing Position")]
        public Transform healingPosition;

        public List<Transform> healingPositions;
        
        [Header("Fallback Spawn")]
        public Vector3 fallbackSpawnPosition = Vector3.zero;
        
        [Networked] public float CurrentHealth { get; private set; }
        [Networked] public bool IsHealing { get; private set; }
        [Networked] public bool HasRespawnParry { get; private set; }
        [Networked] public bool IsFalling { get; private set; }
        [Networked] public bool IsDeathStunned { get; private set; }
        [Networked] private Vector3 PendingTeleportPosition { get; set; }
        [Networked] private NetworkBool HasPendingTeleport { get; set; }
        [Networked] private NetworkBool ForceResetAnimation { get; set; }
        [Networked] private NetworkBool ForceResetInput { get; set; }
        
        public float HealthRatio => CurrentHealth / maxHealth;
        
        private CharacterStats mStats;
        private Game3DUI mUI3D;
        private PlayerMovement mMovement;
        private ParryAbility mParryAbility;
        private PlayerController mPlayerController;
        private NetworkedNPCControllerNew mNPCController;
        private MmInputService mHumanInputService;
        private PlayerAnimation mPlayerAnimation;
        private BehaviorDesigner.Runtime.BehaviorTree mBehaviorTree;
        private NetworkRigidbody3D mNetworkRigidbody;
        private StunSystem mStunSystem;
        private Vector3 mOriginalPosition;
        private Coroutine mHealingCoroutine;
        private Coroutine mRespawnParryCoroutine;
        private Coroutine mFallCheckCoroutine;
        private Coroutine mDeathStunCoroutine;
        private bool mWasNPCDumbBeforeHealing;
        private Transform[] mCachedSpawnPoints;
        private Transform[] mCachedBallSpawnPoints;
        
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
            mPlayerAnimation = GetComponent<PlayerAnimation>();
            mBehaviorTree = GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
            mNetworkRigidbody = GetComponent<NetworkRigidbody3D>();
            mStunSystem = GetComponent<StunSystem>();
            
            InitializeSpawnSystem();
            
            // Start fall detection
            if (HasStateAuthority)
            {
                mFallCheckCoroutine = StartCoroutine(FallDetectionCoroutine());
            }

            gameObject.GetComponent<Rigidbody>().linearDamping = 6;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (HasPendingTeleport)
                {
                    PerformTeleport(PendingTeleportPosition);
                    HasPendingTeleport = false;
                }
                
                if (ForceResetInput)
                {
                    ForceInputReset();
                    ForceResetInput = false;
                }
                
                if (ForceResetAnimation)
                {
                    ForceAnimationReset();
                    ForceResetAnimation = false;
                }
                
               
            }
        }

        private void ForceInputReset()
        {
            // Reset joystick for human players
            if (mHumanInputService != null && Object.HasInputAuthority)
            {
                if (Game2DUI.Instance != null && Game2DUI.Instance.joystick != null)
                {
                    // Force reset joystick position
                    Game2DUI.Instance.joystick.SetNeutralPosition();
                    Game2DUI.Instance.joystick.transform.localPosition = Vector3.zero;
                }
                
                // Clear all input buffers
                mHumanInputService.ResetPressedInputs();
                mHumanInputService.ResetMovement();
            }
            
            // Clear NPC input
            if (mNPCController != null)
            {
                mNPCController.ClearAllInputs();
            }
        }

        private void PerformTeleport(Vector3 targetPosition)
        {
            // Clear all physics state before teleport
            ClearAllPhysicsState();
            
            if (mNetworkRigidbody != null)
            {
                mNetworkRigidbody.Teleport(targetPosition, transform.rotation);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        private void ClearAllPhysicsState()
        {
            if (!HasStateAuthority) return;
            
            // Temporarily set kinematic to clear forces
            if (mNetworkRigidbody != null && mNetworkRigidbody.Rigidbody != null)
            {
                var rb = mNetworkRigidbody.Rigidbody;
                bool wasKinematic = rb.isKinematic;
                
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.ResetInertiaTensor();
                rb.isKinematic = wasKinematic;
                
                // Clear interpolation data
                mNetworkRigidbody.Rigidbody.position = transform.position;
                mNetworkRigidbody.Rigidbody.rotation = transform.rotation;
            }
        }

        private void ForceAnimationReset()
        {
            if (mPlayerAnimation != null)
            {
                mPlayerAnimation.ResetToIdleState();
            }
        }

        private IEnumerator FallDetectionCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(fallCheckInterval);
                
                if (!IsHealing && !IsFalling && !IsDeathStunned && transform.position.y < fallThreshold)
                {
                    Debug.Log($"{gameObject.name} is falling below threshold. Triggering respawn.");
                    IsFalling = true;
                    RPC_StartFallRespawn();
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_StartFallRespawn()
        {
            StartFallRespawn();
        }

        private void StartFallRespawn()
        {
            ForceThrowHeldBall();
            ResetCharacterCompletely();
            
            if (HasStateAuthority)
            {
                StartCoroutine(FallRespawnProcess());
            }
        }

        private IEnumerator FallRespawnProcess()
        {
            yield return new WaitForFixedUpdate();
            
            bool respawnSuccess = SetRespawnPositionNetworked();
            
            if (!respawnSuccess)
            {
                RespawnAtFallbackPosition();
            }
            
            yield return new WaitForSeconds(0.2f);
            
            IsFalling = false;
            EnablePlayerControl();
            StartRespawnParry();
        }

        private void InitializeSpawnSystem()
        {
            // Cache spawn points on spawn
            StartCoroutine(CacheSpawnPointsWithRetry());
            StartCoroutine(CacheBallSpawnPointsWithRetry());
            SetAllHealingZone();
            // Find healing position
            if (healingPosition == null)
            {
                var healingZone = GameObject.FindGameObjectWithTag("HealingZone");
                if (healingZone != null)
                {
                    healingPosition = GetRandomHealingPosition();
                }
            }
        }

        private void SetAllHealingZone()
        {
            healingPositions = new List<Transform>();
            var healingZoneObjects = GameObject.FindGameObjectsWithTag("HealingZone");
            foreach (var zone in healingZoneObjects)
            {
                var healingTransform = zone.transform;
                if (healingTransform != null)
                {
                    healingPositions.Add(healingTransform);
                }
            }
        }
        
        Transform GetRandomHealingPosition()
        {
            if (healingPositions.Count == 0)
            {
                Debug.LogWarning("No healing positions available, using fallback position.");
                return null;
            }
            
            int randomIndex = Random.Range(0, healingPositions.Count);
            return healingPositions[randomIndex];
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

        private IEnumerator CacheBallSpawnPointsWithRetry()
        {
            int retryCount = 0;
            const int maxRetries = 10;
            
            while (mCachedBallSpawnPoints == null || mCachedBallSpawnPoints.Length == 0)
            {
                var ballSpawnPointObjects = GameObject.FindGameObjectsWithTag("BallSpawnPoint");
                
                if (ballSpawnPointObjects.Length > 0)
                {
                    mCachedBallSpawnPoints = new Transform[ballSpawnPointObjects.Length];
                    for (int i = 0; i < ballSpawnPointObjects.Length; i++)
                    {
                        mCachedBallSpawnPoints[i] = ballSpawnPointObjects[i].transform;
                    }
                    
                    Debug.Log($"Cached {mCachedBallSpawnPoints.Length} ball spawn points");
                    break;
                }
                
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    Debug.LogWarning($"Failed to find ball spawn points after {maxRetries} retries.");
                    break;
                }
                
                yield return new WaitForSeconds(0.5f);
            }
        }

        public void TakeDamage(float damage, PlayerController attacker = null)
        {
            if (!HasStateAuthority || IsHealing || HasRespawnParry || IsFalling || IsDeathStunned) return;
            
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(0, CurrentHealth);
            UpdateHealthUI();
            
            if (CurrentHealth <= 0)
            {
                NetworkId attackerId = attacker != null ? attacker.Object.Id : default(NetworkId);
                RPC_StartDeathSequence(attackerId);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_StartDeathSequence(NetworkId attackerId)
        {
            if (IsHealing || IsDeathStunned) return;
            
            PlayerController attacker = null;
            if (attackerId.IsValid)
            {
                var attackerObj = Runner.FindObject(attackerId);
                if (attackerObj != null)
                    attacker = attackerObj.GetComponent<PlayerController>();
            }
            
            StartDeathSequence(attacker);
        }

        private void StartDeathSequence(PlayerController attacker)
        {
            IsDeathStunned = true;
            
            // Play stun animation
            if (mPlayerAnimation != null)
            {
                mPlayerAnimation.SetStunned(true);
            }
            
            // Trigger defeated event
            if (attacker != null)
            {
                OnPlayerDefeated?.Invoke(attacker, mPlayerController);
            }
            
            // Force throw ball immediately
            ForceThrowHeldBall();
            
            // Disable movement but keep animation playing
            if (mMovement != null)
                mMovement.enabled = false;
            
            if (HasStateAuthority)
            {
                mDeathStunCoroutine = StartCoroutine(DeathStunProcess());
            }
        }

        private IEnumerator DeathStunProcess()
        {
            // Play death stun for specified duration
            yield return new WaitForSeconds(deathStunDuration);
            
            IsDeathStunned = false;
            
            // Now start the healing process
            RPC_StartHealing();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_StartHealing()
        {
            StartHealing();
        }

        private void StartHealing()
        {
            if (IsHealing) return;
            
            IsHealing = true;
            mOriginalPosition = transform.position;
            
            ResetCharacterCompletely();
            
            // Only StateAuthority should handle position and healing logic
            if (HasStateAuthority)
            {
                // Clear any ongoing stun
                if (mStunSystem != null && mStunSystem.IsStunned)
                {
                    mStunSystem.ForceEndStun();
                }
                
                // Schedule animation reset
                ForceResetAnimation = true;
                ForceResetInput = true;
                
                TeleportToHealingZone();
                mHealingCoroutine = StartCoroutine(HealingProcess());
            }
        }

        private void ForceThrowHeldBall()
        {
            var pickupAbility = GetComponent<PickUpAbility>();
            var throwAbility = GetComponent<ThrowAbility>();
            
            if (pickupAbility != null && pickupAbility.HasBall)
            {
                // Find held ball and drop it
                var ballController = mPlayerController.ball;
                if (ballController != null )
                {
                    
                    if (ballController != null && HasStateAuthority)
                    {
                        // Force throw the ball
                        Vector3 throwDirection = transform.forward + Vector3.up * 0.3f;
                        throwDirection.Normalize();
                        
                        // Force throw the ball
                        ballController.Throw(throwDirection, 10f, gameObject);
                        
                        // Schedule ball respawn
                        StartCoroutine(RespawnBallAfterDelay(ballController, 2f));
                        
                        Debug.Log($"Force threw ball from {gameObject.name}");
                    }
                }
            }
            
            // Force clear ability states
            if (pickupAbility != null)
            {
                pickupAbility.OnBallThrown();
            }
            
            if (throwAbility != null)
            {
                throwAbility.SetHeldBall(null);
            }
        }

        private IEnumerator RespawnBallAfterDelay(BallController ball, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (ball != null && mCachedBallSpawnPoints != null && mCachedBallSpawnPoints.Length > 0)
            {
                var randomBallSpawn = mCachedBallSpawnPoints[Random.Range(0, mCachedBallSpawnPoints.Length)];
                if (randomBallSpawn != null)
                {
                    RPC_RespawnBall(ball.Object.Id, randomBallSpawn.position);
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_RespawnBall(NetworkId ballId, Vector3 spawnPosition)
        {
            var ballObj = Runner.FindObject(ballId);
            if (ballObj != null)
            {
                var ballController = ballObj.GetComponent<BallController>();
                if (ballController != null)
                {
                    // Reset ball to spawn position
                    ballController.transform.position = spawnPosition;
                    ballController.transform.rotation = Quaternion.identity;
                    
                    var rb = ballController.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    
                    ballController.hasHitGround = false;
                    
                    Debug.Log($"Respawned ball at {spawnPosition}");
                }
            }
        }

        private void ResetCharacterCompletely()
        {
            // Reset rigidbody
            if (HasStateAuthority)
            {
                ClearAllPhysicsState();
            }
            
            // Reset animation to idle
            if (mPlayerAnimation != null)
            {
                mPlayerAnimation.SetState(PlayerAnimState.Locomotion);
                mPlayerAnimation.SetStunned(false);
            }
            
            // Disable all control
            DisablePlayerControl();
            
            // Reset abilities
            ResetAllAbilities();
            
            // Reset stamina
            if (mStats != null && HasStateAuthority)
            {
                mStats.RecoverStamina(mStats.MaxStamina);
            }
        }

        private void ResetAllAbilities()
        {
            var abilities = GetComponents<BaseAbility>();
            foreach (var ability in abilities)
            {
                ability.enabled = false;
            }
        }

        private void DisablePlayerControl()
        {
            // Store NPC state before disabling
            if (mNPCController != null)
            {
                mWasNPCDumbBeforeHealing = mNPCController.IsDumbNPC;
                mNPCController.IsDumbNPC = true;
            }
            
            // Disable movement
            if (mMovement != null) 
                mMovement.enabled = false;
            
            // Disable human input service
            if (mHumanInputService != null)
            {
                mHumanInputService.enabled = false;
            }
            
            // Disable UI buttons for local player
            if (Object.HasInputAuthority && !mPlayerController.IsNPC())
            {
                RPC_DisableUIButtons();
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
            
            // Restore NPC state
            if (mNPCController != null)
            {
                mNPCController.IsDumbNPC = mWasNPCDumbBeforeHealing;
                
                // Force restart behavior tree if it's an NPC
                if (mBehaviorTree != null && !mWasNPCDumbBeforeHealing)
                {
                    StartCoroutine(RestartNPCBehaviorTree());
                }
            }
            
            // Re-initialize abilities with character type data
            var abilities = GetComponents<BaseAbility>();
            foreach (var ability in abilities)
            {
                ability.enabled = true;
                if (ability != null && mPlayerController != null)
                {
                    ability.Initialize(
                        mPlayerController.GetComponent<IInputService>(), 
                        mStats, 
                        mPlayerController.GetCharacterTypeSO()
                    );
                }
            }
            
            // Re-enable UI buttons for local player
            if (Object.HasInputAuthority && !mPlayerController.IsNPC())
            {
                RPC_EnableUIButtons();
            }
        }

        private IEnumerator RestartNPCBehaviorTree()
        {
            yield return new WaitForSeconds(0.2f);
            
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
                PendingTeleportPosition = healingPosition.position;
                HasPendingTeleport = true;
                
                Debug.Log($"Scheduled teleport for {gameObject.name} to healing zone: {healingPosition.position}");
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
            
            // Force reset animation periodically during healing
            float animResetInterval = 0.5f;
            float animResetTimer = 0f;
            
            while (healTime > 0)
            {
                if (mUI3D != null)
                    mUI3D.ShowHealCountdown(healTime);
                
                animResetTimer += Time.deltaTime;
                if (animResetTimer >= animResetInterval)
                {
                    ForceResetAnimation = true;
                    ForceResetInput = true;
                    animResetTimer = 0f;
                }
                
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
            StartCoroutine(CompleteHealingProcess());
        }

        private IEnumerator CompleteHealingProcess()
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
                bool respawnSuccess = SetRespawnPositionNetworked();
                
                if (!respawnSuccess)
                {
                    Debug.LogError($"Failed to respawn {gameObject.name}, trying fallback");
                    RespawnAtFallbackPosition();
                }
            }
            
            yield return new WaitForSeconds(0.2f);
            
            EnablePlayerControl();
            StartRespawnParry();
        }

        private bool SetRespawnPositionNetworked()
        {
            if (!HasStateAuthority) return false;
            
            Vector3? targetPosition = null;
            Quaternion targetRotation = Quaternion.identity;
            
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
                    targetPosition = randomSpawn.position;
                    targetRotation = randomSpawn.rotation;
                }
            }
            
            if (!targetPosition.HasValue)
            {
                var spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");
                if (spawnPointObjects.Length > 0)
                {
                    var randomSpawn = spawnPointObjects[Random.Range(0, spawnPointObjects.Length)];
                    targetPosition = randomSpawn.transform.position;
                    targetRotation = randomSpawn.transform.rotation;
                }
            }
            
            if (targetPosition.HasValue)
            {
                PendingTeleportPosition = targetPosition.Value;
                HasPendingTeleport = true;
                
                Debug.Log($"Scheduled respawn for {gameObject.name} at: {targetPosition.Value}");
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
            
            PendingTeleportPosition = fallbackPos;
            HasPendingTeleport = true;
            
            Debug.LogWarning($"Scheduled fallback respawn for {gameObject.name} at: {fallbackPos}");
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

        public bool CanTakeDamage()
        {
            return !IsHealing && !HasRespawnParry && !IsFalling && !IsDeathStunned;
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

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (mFallCheckCoroutine != null)
            {
                StopCoroutine(mFallCheckCoroutine);
            }
        }
    }
}