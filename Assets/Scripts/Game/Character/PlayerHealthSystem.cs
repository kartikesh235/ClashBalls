using System.Collections;
using UnityEngine;
using Game.Character;
using Game.GameUI;
using Game.Abilities;
using Game.Controllers;

namespace Game.Character
{
    public class PlayerHealthSystem : MonoBehaviour
    {
        [Header("Health Settings")]
        public float maxHealth = 100f;
        public float healingDuration = 5f;
        public float respawnParryDuration = 2f;
        
        [Header("Healing Position")]
        public Transform healingPosition;
        
        public float CurrentHealth { get; private set; }
        public float HealthRatio => CurrentHealth / maxHealth;
        public bool IsHealing { get; private set; }
        public bool HasRespawnParry { get; private set; }
        
        private CharacterStats mStats;
        private Game3DUI mUI3D;
        private PlayerMovement mMovement;
        private ParryAbility mParryAbility;
        private PlayerController mPlayerController;
        private Vector3 mOriginalPosition;
        private Coroutine mHealingCoroutine;
        private Coroutine mRespawnParryCoroutine;
        
        public delegate void PlayerDefeatedDelegate(PlayerController attacker, PlayerController victim);
        public static event PlayerDefeatedDelegate OnPlayerDefeated;

        private void Start()
        {
            CurrentHealth = maxHealth;
            mStats = GetComponent<CharacterStats>();
            mUI3D = GetComponentInChildren<Game3DUI>();
            mMovement = GetComponent<PlayerMovement>();
            mParryAbility = GetComponent<ParryAbility>();
            mPlayerController = GetComponent<PlayerController>();
            
            // Find healing position if not assigned
            if (healingPosition == null)
            {
                var healingZone = GameObject.FindGameObjectWithTag("HealingZone");
                if (healingZone != null)
                    healingPosition = healingZone.transform;
            }
        }

        public void TakeDamage(float damage, PlayerController attacker = null)
        {
            if (IsHealing || HasRespawnParry) return;
            
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(0, CurrentHealth);
            
            if (CurrentHealth <= 0)
            {
                StartHealing(attacker);
            }
        }

        private void StartHealing(PlayerController attacker)
        {
            if (IsHealing) return;
            
            IsHealing = true;
            mOriginalPosition = transform.position;
            
            // Notify score system
            if (attacker != null)
            {
                OnPlayerDefeated?.Invoke(attacker, mPlayerController);
            }
            
            // Disable movement and abilities
            if (mMovement != null) mMovement.enabled = false;
            DisableAllAbilities();
            
            // Teleport to healing position
            if (healingPosition != null)
            {
                transform.position = healingPosition.position;
                transform.rotation = healingPosition.rotation;
            }

            if (mHealingCoroutine != null)
            {
                StopCoroutine(mHealingCoroutine);
            }
            
            // Start healing process
            mHealingCoroutine = StartCoroutine(HealingProcess());
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
            
            // Healing complete
            CompleteHealing();
        }

        private void CompleteHealing()
        {
            IsHealing = false;
            CurrentHealth = maxHealth;
            
            // Hide healing effects
            if (mUI3D != null)
            {
                mUI3D.ShowHealingEffects(false);
                mUI3D.HideHealCountdown();
            }
            
            // Respawn at spawn point
            RespawnAtSpawnPoint();
            
            // Re-enable movement and abilities
            if (mMovement != null) mMovement.enabled = true;
            EnableAllAbilities();
            
            // Grant respawn parry
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
            }
        }

        private void StartRespawnParry()
        {
            HasRespawnParry = true;
            mRespawnParryCoroutine = StartCoroutine(RespawnParryDuration());
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
    }
}