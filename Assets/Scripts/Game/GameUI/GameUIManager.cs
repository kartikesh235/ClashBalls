using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GameUIManager : MonoBehaviour
{
    [Header("Joystick")]
    public GameObject joystickRoot;

    [Header("Ability Buttons")]
    public AbilityButton[] abilityButtons;

    [Header("Bars & Indicators")]
    public Slider staminaBar;
    public Slider healthBar;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI gameTimerText;

    [Header("Bounce FX")]
    public Animator staminaFX;
    public Animator healthFX;

    private float gameDuration;
    private float currentTime;

    public void Setup(float duration)
    {
        gameDuration = duration;
        currentTime = duration;
    }

    private void Update()
    {
        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            gameTimerText.text = Mathf.CeilToInt(currentTime).ToString();
        }
    }

    public void UpdateStamina(float current, float max)
    {
        float target = current / max;
        StartCoroutine(SmoothFill(staminaBar, target));
        staminaFX.SetTrigger("Bounce");
    }

    public void UpdateHealth(float current, float max)
    {
        float target = current / max;
        StartCoroutine(SmoothFill(healthBar, target));
        healthFX.SetTrigger("Bounce");
    }

    public void UpdateScore(int score)
    {
        scoreText.text = score.ToString();
    }

    private IEnumerator SmoothFill(Slider bar, float target)
    {
        float start = bar.value;
        float time = 0;
        while (time < 0.2f)
        {
            time += Time.deltaTime;
            bar.value = Mathf.Lerp(start, target, time / 0.2f);
            yield return null;
        }
        bar.value = target;
    }
}