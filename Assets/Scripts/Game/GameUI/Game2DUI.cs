using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Fusion;
using Game.Managers;
using Game.Controllers;

namespace Game.GameUI
{
    public class Game2DUI : MMSingleton<Game2DUI>
    {
        [Header("Player Status (Left Side)")]
        public Slider healthSlider;
        public Slider staminaSlider;
        public TMP_Text healthText;
        public TMP_Text staminaText;
        public TMP_Text playerNameText;

        [Header("Game Info (Middle)")]
        public TMP_Text killCounterText;
        public TMP_Text timerText;
        public TMP_Text scoreText;

        [Header("All Players Info (Right Side)")]
        public TMP_Text[] playerNamesTexts = new TMP_Text[4];
        public TMP_Text[] playerScoresTexts = new TMP_Text[4];
        public GameObject[] playerInfoPanels = new GameObject[4];

        [Header("Mobile UI")]
        public MMTouchJoystick joystick;
        public Button buttonA, buttonB, buttonC, buttonD, buttonE;

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

        [Header("Throw Power")]
        public Slider throwSlider;
        [SerializeField] private float mThrowSliderSmoothSpeed = 10f;

        private Coroutine mThrowSliderCoroutine;
        private List<ScoreManager.PlayerScoreUI> mAllPlayerScores = new List<ScoreManager.PlayerScoreUI>();
        private PlayerController mLocalPlayer; // Cache local player reference

        private void Start()
        {
            InitializeUI();
            ScoreManager.OnScoreUpdated += UpdatePlayerScores;
            
            // Find and cache local player
            StartCoroutine(FindLocalPlayerCoroutine());
        }

        private IEnumerator FindLocalPlayerCoroutine()
        {
            // Wait a bit for network to initialize
            yield return new WaitForSeconds(3f);
            
            while (mLocalPlayer == null)
            {
                FindAndCacheLocalPlayer();
                yield return new WaitForSeconds(0.5f);
            }
            
            Debug.Log($"Local player found and cached: {mLocalPlayer.name}");
        }

        private void FindAndCacheLocalPlayer()
        {
            var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.Object != null && player.Object.HasInputAuthority && !player.IsNPC())
                {
                    mLocalPlayer = player;
                    UpdatePlayerNameUI();
                    break;
                }
            }
        }

        private void UpdatePlayerNameUI()
        {
            if (mLocalPlayer != null && playerNameText != null)
            {
                playerNameText.text = ScoreManager.Instance.myPlayerName;
            }
        }

        private void OnDestroy()
        {
            ScoreManager.OnScoreUpdated -= UpdatePlayerScores;
        }

        private void InitializeUI()
        {
            SetHealth(100, 100);
            SetStamina(3, 3);
            SetThrowPower(0f, 1f);
            UpdateScore(0);
            UpdateKillCounter(0);
            
            // Initialize player info panels
            for (int i = 0; i < playerInfoPanels.Length; i++)
            {
                if (playerInfoPanels[i] != null)
                    playerInfoPanels[i].SetActive(false);
            }
        }

        // Helper method to check if caller is local player
        private bool IsLocalPlayer(NetworkObject caller)
        {
            if (caller == null)
            {
                Debug.LogWarning("Game2DUI.IsLocalPlayer: caller is null");
                return false;
            }
            
            // Check if it's our cached local player
            if (mLocalPlayer != null && caller.gameObject == mLocalPlayer.gameObject)
            {
                return true;
            }
            
            // For NPCs, never update local UI
            var npcController = caller.GetComponent<Game.AI.NetworkedNPCControllerNew>();
            if (npcController != null) 
            {
                Debug.Log($"Game2DUI.IsLocalPlayer: {caller.name} is NPC, ignoring");
                return false;
            }
            
            // For human players, check if they have input authority
            bool hasInputAuth = caller.HasInputAuthority;
            Debug.Log($"Game2DUI.IsLocalPlayer: {caller.name} HasInputAuthority: {hasInputAuth}");
            return hasInputAuth;
        }

        #region Player Status UI (Left Side)
        public void SetHealth(float current, float max, NetworkObject caller = null)
        {
            // If no caller specified, always update (for backward compatibility)
            if (caller == null)
            {
                healthSlider.value = Mathf.Clamp01(current / max);
                healthText.text = current+"/"+max;
                Debug.Log($"Health updated directly: {current}/{max} = {healthSlider.value}");
                return;
            }
            
            if (!IsLocalPlayer(caller)) 
            {
                Debug.Log($"SetHealth ignored for {caller.name} - not local player");
                return;
            }
            
            healthSlider.value = Mathf.Clamp01(current / max);
            healthText.text = current+"/"+max;
            Debug.Log($"Health updated for local player: {current}/{max} = {healthSlider.value}");
        }

        public void SetStamina(float current, float max, NetworkObject caller = null)
        {
            // If no caller specified, always update (for backward compatibility)
            if (caller == null)
            {
                staminaSlider.value = Mathf.Clamp01(current / max);
                staminaText.text = (int)current + "/" + (int)max;
                Debug.Log($"Stamina updated directly: {current}/{max} = {staminaSlider.value}");
                return;
            }
            
            if (!IsLocalPlayer(caller))
            {
                Debug.Log($"SetStamina ignored for {caller.name} - not local player");
                return;
            }
            
            staminaSlider.value = Mathf.Clamp01(current / max);
            staminaText.text = (int)current + "/" + (int)max;
            Debug.Log($"Stamina updated for local player: {current}/{max} = {staminaSlider.value}");
        }

        public void SetPlayerName(string playerName, NetworkObject caller = null)
        {
            if (caller != null && !IsLocalPlayer(caller)) return;
            
            playerNameText.text = playerName;
        }
        #endregion

        #region Game Info UI (Middle)
        public void UpdateKillCounter(int kills, NetworkObject caller = null)
        {
            if (caller != null && !IsLocalPlayer(caller)) return;
            
            killCounterText.text = $"Kills: {kills}";
        }

        public void UpdateTimer(float timeLeft)
        {
            int minutes = Mathf.FloorToInt(timeLeft / 60f);
            int seconds = Mathf.FloorToInt(timeLeft % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        public void UpdateScore(int score, NetworkObject caller = null)
        {
            if (caller != null && !IsLocalPlayer(caller)) return;
            
            scoreText.text = $"Score: {score}";
        }
        #endregion

        #region All Players Info UI (Right Side)
        public void UpdateAllPlayersInfo(List<ScoreManager.PlayerScoreUI> allScores)
        {
            mAllPlayerScores = allScores;
            
            // Sort by score (highest first)
            var sortedScores = new List<ScoreManager.PlayerScoreUI>(allScores);
            sortedScores.Sort((a, b) => b.totalScore.CompareTo(a.totalScore));
            
            for (int i = 0; i < playerInfoPanels.Length; i++)
            {
                if (i < sortedScores.Count)
                {
                    // Show player info
                    playerInfoPanels[i].SetActive(true);
                    
                    var playerScore = sortedScores[i];
                    string displayName = playerScore.isNPC ? $"[NPC] {playerScore.playerName}" : playerScore.playerName;
                    
                    playerNamesTexts[i].text = displayName;
                    playerScoresTexts[i].text = playerScore.totalScore.ToString();
                }
                else
                {
                    // Hide unused panels
                    playerInfoPanels[i].SetActive(false);
                }
            }
        }

        private void UpdatePlayerScores(PlayerController player, int newScore, int killCount)
        {
            // Update the specific player's data
            if (ScoreManager.Instance != null)
            {
                var allScores = ScoreManager.Instance.GetAllScores();
                UpdateAllPlayersInfo(allScores);
                
                // Update local player's kill counter if this is the local player
                if (mLocalPlayer != null && player == mLocalPlayer)
                {
                    UpdateKillCounter(killCount);
                    UpdateScore(newScore);
                }
            }
        }
        #endregion

        #region Throw Power UI
        public void SetThrowPower(float current, float max, NetworkObject caller = null)
        {
            if (caller != null && !IsLocalPlayer(caller)) return;
            
            throwSlider.value = Mathf.Clamp01(current / max);
        }

        public void SetThrowPower(float targetValue, NetworkObject caller = null)
        {
            if (caller != null && !IsLocalPlayer(caller)) return;
            
            targetValue = Mathf.Clamp(targetValue, throwSlider.minValue, throwSlider.maxValue);

            if (mThrowSliderCoroutine != null)
            {
                StopCoroutine(mThrowSliderCoroutine);
            }

            mThrowSliderCoroutine = StartCoroutine(SmoothSetThrowSlider(targetValue));
        }

        public void ConfigureThrowSlider(float min, float max, NetworkObject caller = null)
        {
            if (caller != null && !IsLocalPlayer(caller)) return;
            
            throwSlider.minValue = min;
            throwSlider.maxValue = max;
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

                yield return null;
            }

            throwSlider.value = target;
            mThrowSliderCoroutine = null;
        }
        #endregion

        #region Cooldown UI
        public static void SetCooldownMask(Button button, Image mask, TMP_Text text, float cooldownRemaining, float totalCooldown)
        {
            if (mask == null || text == null) return;

            if (cooldownRemaining > 0f)
            {
                var eventTrigger = button.GetComponent<EventTrigger>();
                if (eventTrigger != null) eventTrigger.enabled = false;
                
                float ratio = Mathf.Clamp01(cooldownRemaining / totalCooldown);
                mask.fillAmount = ratio;
                mask.gameObject.SetActive(true);
                text.text = Mathf.CeilToInt(cooldownRemaining) + "s";
            }
            else
            {
                var eventTrigger = button.GetComponent<EventTrigger>();
                if (eventTrigger != null) eventTrigger.enabled = true;
                
                mask.gameObject.SetActive(false);
                text.text = "";
            }
        }
        #endregion

        #region Direct Update Methods (for local player)
        // Add these methods for direct updates from health/stamina systems
        public void UpdateLocalPlayerHealth(float current, float max)
        {
            healthSlider.value = Mathf.Clamp01(current / max);
            healthText.text = current+"/"+max;
            
            Debug.Log($"Direct health update: {current}/{max} = {healthSlider.value}");
        }

        public void UpdateLocalPlayerStamina(float current, float max)
        {
            staminaSlider.value = Mathf.Clamp01(current / max);
            staminaText.text = (int)current + "/" + (int)max;
            Debug.Log($"Direct stamina update: {current}/{max} = {staminaSlider.value}");
        }
        #endregion

        #region Backward Compatibility Methods
        public void SetHealth(float current, float max)
        {
            healthSlider.value = Mathf.Clamp01(current / max);
            healthText.text = current+"/"+max;
        }

        public void SetStamina(float current, float max)
        {
            staminaSlider.value = Mathf.Clamp01(current / max);
            staminaText.text = (int)current + "/" + (int)max;
        }

        public void SetThrowPower(float current, float max)
        {
            throwSlider.value = Mathf.Clamp01(current / max);
        }

        public void UpdateScore(int score)
        {
            scoreText.text = $"Score: {score}";
        }

        public void UpdateKillCounter(int kills)
        {
            killCounterText.text = $"Kills: {kills}";
        }
        #endregion
    }
}