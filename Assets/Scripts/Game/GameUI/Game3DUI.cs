using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Character;
using Game.Controllers;

namespace Game.GameUI
{
    public class Game3DUI : MonoBehaviour
    {
        [Header("UI Components")]
        public Slider healthSlider;
        public Slider staminaSlider;
        public TMP_Text healthText;
        public TMP_Text staminaText;
        public TMP_Text attackPowerText;
        public TMP_Text healCountdownText;
        
        [Header("Healing Effects")]
        public GameObject healingParticles;
        
        private CharacterStats mStats;
        private PlayerHealthSystem mHealthSystem;
        private CharacterTypeSO mCharacterType;
        private Camera mMainCamera;

        private void Start()
        {
            mMainCamera = Camera.main;
            mStats = GetComponentInParent<CharacterStats>();
            mHealthSystem = GetComponentInParent<PlayerHealthSystem>();
            mCharacterType = GetComponentInParent<PlayerController>().GetCharacterTypeSO();
            
            // Initialize UI
            if (mCharacterType != null)
            {
                float tackleAttackPower = mCharacterType.tackleForce;
                attackPowerText.text = $"ATK: {tackleAttackPower:F0}";
            }
            
            // Hide heal countdown initially
            if (healCountdownText != null)
                healCountdownText.gameObject.SetActive(false);
                
            if (healingParticles != null)
                healingParticles.SetActive(false);
        }

        private void Update()
        {
           
            UpdateHealthUI();
            UpdateStaminaUI();
        }

        private void LateUpdate()
        {
            LookAtCamera();
        }

        private void LookAtCamera()
        {
            if (mMainCamera != null)
            {
                transform.LookAt(mMainCamera.transform);
                transform.Rotate(0, 180, 0);
            }
        }

        private void UpdateHealthUI()
        {
            if (mHealthSystem != null && healthSlider != null)
            {
                healthSlider.value = mHealthSystem.HealthRatio;
                healthText.text = $"{mHealthSystem.CurrentHealth:F0} / {mHealthSystem.maxHealth:F0}";
            }
        }

        private void UpdateStaminaUI()
        {
            if (mStats != null && staminaSlider != null)
            {
                staminaSlider.value = mStats.StaminaRatio;
                staminaText.text = $"{mStats.CurrentStamina:F0} / {mStats.MaxStamina:F0}";
            }
        }

        public void ShowHealCountdown(float timeLeft)
        {
            if (healCountdownText != null)
            {
                healCountdownText.gameObject.SetActive(true);
                healCountdownText.text = $"Healing: {timeLeft:F1}s";
            }
        }

        public void HideHealCountdown()
        {
            if (healCountdownText != null)
                healCountdownText.gameObject.SetActive(false);
        }

        public void ShowHealingEffects(bool show)
        {
            if (healingParticles != null)
                healingParticles.SetActive(show);
        }
    }
}