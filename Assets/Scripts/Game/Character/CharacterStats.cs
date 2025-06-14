using System.Collections;
using UnityEngine;

namespace Game.Character
{
    public class CharacterStats : MonoBehaviour
    {
        public float CurrentStamina { get; private set; }
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
            if (regenCoroutine != null) StopCoroutine(regenCoroutine);
            regenCoroutine = StartCoroutine(RegenDelay());
            return true;
        }

        public void RecoverStamina(float amount)
        {
            CurrentStamina = Mathf.Min(CurrentStamina + amount, typeData.maxStamina);
        }

        private IEnumerator RegenDelay()
        {
            yield return new WaitForSeconds(typeData.staminaRegenDelay);
            while (CurrentStamina < typeData.maxStamina)
            {
                CurrentStamina += typeData.staminaRegenRate * Time.deltaTime;
                yield return null;
            }
        }
    }
}