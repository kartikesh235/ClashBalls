using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class AbilityButton : MonoBehaviour, IPointerDownHandler
{
    public Image radialFill;
    public float cooldownTime = 5f;
    private float timer;
    private bool isCooldown;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isCooldown)
        {
            TriggerAbility();
            StartCoroutine(CooldownRoutine());
        }
    }

    private void TriggerAbility()
    {
        // Signal actual ability handler
    }

    private IEnumerator CooldownRoutine()
    {
        isCooldown = true;
        timer = cooldownTime;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            radialFill.fillAmount = timer / cooldownTime;
            yield return null;
        }
        radialFill.fillAmount = 0f;
        isCooldown = false;
    }
}