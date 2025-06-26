using UnityEngine;
using System.Collections.Generic;
using Game.Controllers;
using Game.Character;

namespace Game.Managers
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance;
        
        [System.Serializable]
        public class PlayerScore
        {
            public PlayerController player;
            public string playerName;
            public int totalScore;
            public int killCount;
            public bool isNPC;
        }
        
        private Dictionary<PlayerController, PlayerScore> mPlayerScores = new Dictionary<PlayerController, PlayerScore>();
        
        public delegate void ScoreUpdatedDelegate(PlayerController player, int newScore, int killCount);
        public static event ScoreUpdatedDelegate OnScoreUpdated;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                PlayerHealthSystem.OnPlayerDefeated += HandlePlayerDefeated;
            }
        }

        private void OnDestroy()
        {
            PlayerHealthSystem.OnPlayerDefeated -= HandlePlayerDefeated;
        }

        public void RegisterPlayer(PlayerController player, string playerName, bool isNPC)
        {
            if (!mPlayerScores.ContainsKey(player))
            {
                mPlayerScores[player] = new PlayerScore
                {
                    player = player,
                    playerName = playerName,
                    totalScore = 0,
                    killCount = 0,
                    isNPC = isNPC
                };
            }
        }

        public void AddScore(PlayerController player, int damage, string reason = "")
        {
            if (mPlayerScores.ContainsKey(player))
            {
                mPlayerScores[player].totalScore += damage;
                OnScoreUpdated?.Invoke(player, mPlayerScores[player].totalScore, mPlayerScores[player].killCount);
                
                Debug.Log($"{mPlayerScores[player].playerName} scored {damage} points for {reason}. Total: {mPlayerScores[player].totalScore}");
            }
        }

        private void HandlePlayerDefeated(PlayerController attacker, PlayerController victim)
        {
            if (mPlayerScores.ContainsKey(attacker))
            {
                mPlayerScores[attacker].killCount++;
                OnScoreUpdated?.Invoke(attacker, mPlayerScores[attacker].totalScore, mPlayerScores[attacker].killCount);
                
                Debug.Log($"{mPlayerScores[attacker].playerName} defeated {mPlayerScores[victim].playerName}! Kill count: {mPlayerScores[attacker].killCount}");
            }
        }

        public PlayerScore GetPlayerScore(PlayerController player)
        {
            return mPlayerScores.ContainsKey(player) ? mPlayerScores[player] : null;
        }

        public List<PlayerScore> GetAllScores()
        {
            return new List<PlayerScore>(mPlayerScores.Values);
        }

        public List<PlayerScore> GetSortedScores()
        {
            var scores = GetAllScores();
            scores.Sort((a, b) => b.totalScore.CompareTo(a.totalScore));
            return scores;
        }
    }
}