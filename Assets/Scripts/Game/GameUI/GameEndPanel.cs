using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Game.Managers;

namespace Game.GameUI
{
    public class GameEndPanel : MonoBehaviour
    {
        [Header("Panel References")]
        public GameObject gameEndPanel;
        public Button goToLobbyButton;
        
        [Header("Results Display")]
        public TMP_Text gameEndTitleText;
        public Transform playerResultsContainer;
        public GameObject playerResultPrefab;
        
        [Header("Winner Display")]
        public TMP_Text winnerText;
        
        private List<GameObject> mSpawnedResultItems = new List<GameObject>();

        private void Start()
        {
            gameEndPanel.SetActive(false);
            goToLobbyButton.onClick.AddListener(GoToLobby);
            
            // Subscribe to game end event
            GameManager.OnGameEnded += ShowGameEndResults;
        }

        private void OnDestroy()
        {
            GameManager.OnGameEnded -= ShowGameEndResults;
        }

        public void ShowGameEndResults()
        {
            gameEndPanel.SetActive(true);
            DisplayResults();
            Time.timeScale = 0f; // Pause game
        }

        private void DisplayResults()
        {
            // Clear previous results
            ClearPreviousResults();
            
            // Get all player scores sorted by highest score
            var sortedScores = ScoreManager.Instance.GetSortedScores();
            
            // Display winner
            if (sortedScores.Count > 0)
            {
                var winner = sortedScores[0];
                string winnerName = winner.isNPC ? $"[NPC] {winner.playerName}" : winner.playerName;
                winnerText.text = $"WINNER: {winnerName}";
            }
            
            // Display all player results
            for (int i = 0; i < sortedScores.Count; i++)
            {
                CreatePlayerResultItem(sortedScores[i], i + 1);
            }
        }

        private void CreatePlayerResultItem(ScoreManager.PlayerScoreUI playerScore, int rank)
        {
            GameObject resultItem = Instantiate(playerResultPrefab, playerResultsContainer);
            resultItem.gameObject.SetActive(true);
            mSpawnedResultItems.Add(resultItem);
            
            // Get components from prefab
            var rankText = resultItem.transform.Find("ChildT").transform.Find("RankText").GetComponent<TMP_Text>();
            var nameText = resultItem.transform.Find("ChildT").transform.Find("NameText").GetComponent<TMP_Text>();
            var scoreText = resultItem.transform.Find("ChildT").transform.Find("Score").gameObject.transform.Find("ScoreText").GetComponent<TMP_Text>();
            var killsText = resultItem.transform.Find("ChildT").transform.Find("KillsText").GetComponent<TMP_Text>();
         //   var playerTypeIcon = resultItem.transform.Find("PlayerTypeIcon").GetComponent<Image>();
            
            // Set rank with medal emojis
            string rankDisplay = rank switch
            {
                1 => "1",
                2 => "2", 
                3 => "3",
                _ => $"#{rank}"
            };
            rankText.text = rankDisplay;
            
            // Set player name
            string displayName = playerScore.isNPC ? $"[NPC] {playerScore.playerName}" : playerScore.playerName;
            nameText.text = displayName;
            
            // Set score and kills
            scoreText.text = $"{playerScore.totalScore}";
            killsText.text = $"Kills: {playerScore.killCount}";
            
            // Set player type icon color
            // if (playerTypeIcon != null)
            // {
            //     playerTypeIcon.color = playerScore.isNPC ? Color.blue : Color.green;
            // }
            
            // Add rank-based background color
            var background = resultItem.GetComponent<Image>();
            if (background != null)
            {
                background.color = rank switch
                {
                    1 => new Color(1f, 0.84f, 0f, 0.3f), // Gold
                    2 => new Color(0.75f, 0.75f, 0.75f, 0.3f), // Silver
                    3 => new Color(0.8f, 0.5f, 0.2f, 0.3f), // Bronze
                    _ => new Color(1f, 1f, 1f, 0.1f) // Default
                };
            }
        }

        private void ClearPreviousResults()
        {
            foreach (var item in mSpawnedResultItems)
            {
                if (item != null)
                    DestroyImmediate(item);
            }
            mSpawnedResultItems.Clear();
        }

        private void GoToLobby()
        {
            Time.timeScale = 1f; // Resume normal time
            
            // Clean up current game state
            CleanupGameState();
            
            // Load lobby scene
            SceneManager.LoadScene(0); // Assuming lobby is scene 0
        }

        private void CleanupGameState()
        {
            // Reset score manager
            if (ScoreManager.Instance != null)
            {
                Destroy(ScoreManager.Instance.gameObject);
            }
            
            // Clean up network runner if exists
            var networkRunner = FindObjectOfType<Fusion.NetworkRunner>();
            if (networkRunner != null)
            {
                networkRunner.Shutdown();
            }
            
            // Clear any static references
            Game2DUI.Instance?.gameObject.SetActive(false);
        }
    }
}