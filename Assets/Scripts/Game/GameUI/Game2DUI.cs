using System.Collections;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.GameUI
{
    public class Game2DUI : MMSingleton<Game2DUI>
    {
        public Slider healthSlider;
        public Slider staminaSlider;
        public Slider throwSlider;
        [SerializeField] private float mThrowSliderSmoothSpeed = 10f; // Adjustable in inspector


        [Header("Mobile UI")]
        public MMTouchJoystick joystick;
        public Button buttonA, buttonB, buttonC, buttonD, buttonE;

        public TMP_Text timerText;
        public Button startButton;
        public TMP_Text scoreText;
        public TMP_Text killsText;

        [Header("Masked UI")] 
        public Image buttonAMask;
        public Image buttonBMask;
        public Image buttonCMask;
        public Image buttonDMask;
        public Image buttonEMask;
        
        public TMP_Text cooldownTimerTextA;
        public TMP_Text cooldownTimerTextB;
        public TMP_Text cooldownTimerTextC;
        public TMP_Text cooldownTimerTextD;
        public TMP_Text cooldownTimerTextE;

        [Header("All Player Stats")]
        public TMP_Text username1Text;
        public TMP_Text username2Text;
        public TMP_Text username3Text;
        public TMP_Text username4Text;

        public TMP_Text score1Text;
        public TMP_Text score2Text;
        public TMP_Text score3Text;
        public TMP_Text score4Text;

        public TMP_Text myPlayerUsernameText;

        private void Start()
        {
            SetHealth(1f, 1f);
            SetStamina(1f, 1f);
            SetThrowPower(0f, 1f);
            UpdateScore(0);
            UpdateKills(0);
        }

        public void SetHealth(float current, float max)
        {
            healthSlider.value = Mathf.Clamp01(current / max);
        }

        public void SetStamina(float current, float max)
        {
            staminaSlider.value = Mathf.Clamp01(current / max);
        }

        public void SetThrowPower(float current, float max)
        {
            throwSlider.value = Mathf.Clamp01(current / max);
        }

        public void UpdateScore(int score)
        {
            scoreText.text = $"Score: {score}";
        }

        public void UpdateKills(int kills)
        {
            killsText.text = $"Kills: {kills}";
        }

        public void UpdateTimer(float timeLeft)
        {
            int minutes = Mathf.FloorToInt(timeLeft / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        public static void SetCooldownMask(Button button,Image mask, TMP_Text text, float cooldownRemaining, float totalCooldown)
        {
            if (mask == null || text == null) return;

            if (cooldownRemaining > 0f)
            {
                button.GetComponent<EventTrigger>().enabled = false;
                float ratio = Mathf.Clamp01(cooldownRemaining / totalCooldown);
                mask.fillAmount = ratio;
                mask.gameObject.SetActive(true);
                text.text = Mathf.CeilToInt(cooldownRemaining)+"s";
            }
            else
            {
                button.GetComponent<EventTrigger>().enabled = true;
                mask.gameObject.SetActive(false);
                text.text = "";
            }
        }

        public void SetPlayerStats(int index, string username, int score)
        {
            switch (index)
            {
                case 0:
                    username1Text.text = username;
                    score1Text.text = score.ToString();
                    break;
                case 1:
                    username2Text.text = username;
                    score2Text.text = score.ToString();
                    break;
                case 2:
                    username3Text.text = username;
                    score3Text.text = score.ToString();
                    break;
                case 3:
                    username4Text.text = username;
                    score4Text.text = score.ToString();
                    break;
            }
        }
        public void ConfigureThrowSlider(float min, float max)
        {
            throwSlider.minValue = min;
            throwSlider.maxValue = max;
        }

        private Coroutine mThrowSliderCoroutine;

        public void SetThrowPower(float targetValue)
        {
            targetValue = Mathf.Clamp(targetValue, throwSlider.minValue, throwSlider.maxValue);

            if (mThrowSliderCoroutine != null)
            {
                StopCoroutine(mThrowSliderCoroutine);
            }

            mThrowSliderCoroutine = StartCoroutine(SmoothSetThrowSlider(targetValue));
        }
        

        private IEnumerator SmoothSetThrowSlider(float target)
        {
            while (!Mathf.Approximately(throwSlider.value, target))
            {
                throwSlider.value = Mathf.MoveTowards(
                    throwSlider.value,
                    target,
                    mThrowSliderSmoothSpeed * Time.deltaTime
                );

                yield return null; // wait until next frame
            }

            throwSlider.value = target;
            mThrowSliderCoroutine = null;
        }

        public void SetMyUsername(string username)
        {
            myPlayerUsernameText.text = username;
        }
    }
}