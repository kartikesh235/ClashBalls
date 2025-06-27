using System.Collections;
using Fusion;
using Game.Controllers;
using Game.GameUI;
using UnityEngine;

namespace Game.Character
{
    public class CharacterStats : NetworkBehaviour
    {
        public float CurrentStamina { get; private set; }
        public float MaxStamina => typeData?.maxStamina ?? 3f;
        public float StaminaRatio => CurrentStamina / MaxStamina;
        
        private CharacterTypeSO typeData;
        private Coroutine regenCoroutine;
        
        public float currentStamina => CurrentStamina;

        public void Initialize(CharacterTypeSO data)
        {
            typeData = data;
            CurrentStamina = typeData.maxStamina;
        }

        public bool ConsumeStamina(float amount)
        {
            if (CurrentStamina < amount) return false;
            CurrentStamina -= amount;
            UpdateStaminaUI();
            if (regenCoroutine != null) StopCoroutine(regenCoroutine);
            regenCoroutine = StartCoroutine(RegenDelay());
            return true;
        }

        public void RecoverStamina(float amount)
        {
            CurrentStamina = Mathf.Min(CurrentStamina + amount, typeData.maxStamina);
            UpdateStaminaUI();
        }

        public bool HasStamina(float amount)
        {
            return CurrentStamina >= amount;
        }

        private IEnumerator RegenDelay()
        {
            yield return new WaitForSeconds(typeData.staminaRegenDelay);
            while (CurrentStamina < typeData.maxStamina)
            {
                CurrentStamina += typeData.staminaRegenRate * Time.deltaTime;
                UpdateStaminaUI();
                yield return null;
            }
        }
        
        private void UpdateStaminaUI()
        {
            if (Game2DUI.Instance != null)
            {
                // Check if this is the local player
                if (Object != null && Object.HasInputAuthority && !GetComponent<PlayerController>().IsNPC())
                {
                    Game2DUI.Instance.UpdateLocalPlayerStamina(CurrentStamina, MaxStamina);
                }
            }
        }
    }
}