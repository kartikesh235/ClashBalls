using System.Collections;
using UnityEngine;
using Game.GameUI;
using Game.Managers;

namespace Game.Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        [Header("Game Settings")]
        public float matchDuration = 90f;
        
        [Header("UI References")]
        [SerializeField] private Game2DUI mUI;
        [SerializeField] private GameEndPanel mGameEndPanel;

        private float mCurrentTime;
        private bool mGameEnded = false;
        
        public delegate void GameEndedDelegate();
        public static event GameEndedDelegate OnGameEnded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                mCurrentTime = matchDuration;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (mUI == null)
                mUI = FindObjectOfType<Game2DUI>();
                
            if (mGameEndPanel == null)
                mGameEndPanel = FindObjectOfType<GameEndPanel>();
            
            StartCoroutine(GameTimer());
        }

        private IEnumerator GameTimer()
        {
            while (mCurrentTime > 0 && !mGameEnded)
            {
                mCurrentTime -= Time.deltaTime;
                
                // Update UI timer
                if (mUI != null)
                    mUI.UpdateTimer(mCurrentTime);
                
                yield return null;
            }
            
            // Time's up!
            EndGame();
        }

        public void EndGame()
        {
            if (mGameEnded) return;
            
            mGameEnded = true;
            Debug.Log("Game Ended!");
            
            // Notify all systems
            OnGameEnded?.Invoke();
            
            // Show game end panel
            if (mGameEndPanel != null)
            {
                mGameEndPanel.ShowGameEndResults();
            }
        }

        public void UpdatePlayerStats(Fusion.NetworkRunner runner, Fusion.PlayerRef player, float health, float maxHealth, float stamina, float maxStamina, int score)
        {
            if (player == runner.LocalPlayer)
            {
                if (mUI != null)
                {
                    mUI.SetHealth(health, maxHealth);
                    mUI.SetStamina(stamina, maxStamina);
                    mUI.UpdateScore(score);
                }
            }
        }

        public float GetTimeLeft()
        {
            return mCurrentTime;
        }

        public bool IsGameEnded()
        {
            return mGameEnded;
        }
    }
}