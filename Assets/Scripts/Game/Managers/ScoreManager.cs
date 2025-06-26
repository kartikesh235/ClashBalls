using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fusion;
using Game.Controllers;
using Game.GameUI;
using Game.Character;

namespace Game.Managers
{
    public class ScoreManager : NetworkBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        // Event for UI updates
        public static System.Action<PlayerController, int, int> OnScoreUpdated;

        [System.Serializable]
        public struct PlayerScore : INetworkStruct
        {
            public int score;
            public int kills;
            public int deaths;
            public int playerRefId;
        }

        // Public struct for UI compatibility
        [System.Serializable]
        public struct PlayerScoreUI
        {
            public string playerName;
            public int totalScore;
            public int killCount;
            public int deathCount;
            public bool isNPC;
            public PlayerRef playerRef;
        }

        [Networked, Capacity(4)] 
        public NetworkDictionary<PlayerRef, PlayerScore> PlayerScores => default;

        private Dictionary<PlayerRef, PlayerController> mRegisteredPlayers = new Dictionary<PlayerRef, PlayerController>();
        private Dictionary<PlayerRef, string> mPlayerNames = new Dictionary<PlayerRef, string>();

        public override void Spawned()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            PlayerHealthSystem.OnPlayerDefeated += OnPlayerDefeated;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            PlayerHealthSystem.OnPlayerDefeated -= OnPlayerDefeated;
            
            if (Instance == this)
                Instance = null;
        }

        public void RegisterPlayer(PlayerController player)
        {
            if (!HasStateAuthority) return;

            var playerRef = player.Object.InputAuthority;
            
            if (!mRegisteredPlayers.ContainsKey(playerRef))
            {
                mRegisteredPlayers[playerRef] = player;
                
                string playerName = player.IsNPC() ? $"NPC {playerRef.PlayerId}" : $"Player {playerRef.PlayerId}";
                mPlayerNames[playerRef] = playerName;
                
                var newScore = new PlayerScore
                {
                    score = 0,
                    kills = 0,
                    deaths = 0,
                    playerRefId = playerRef.PlayerId
                };
                
                PlayerScores.Set(playerRef, newScore);
                RPC_UpdatePlayerName(playerRef, playerName);
                UpdateAllUI();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdatePlayerName(PlayerRef playerRef, string playerName)
        {
            mPlayerNames[playerRef] = playerName;
        }

        public void UnregisterPlayer(PlayerRef playerRef)
        {
            if (!HasStateAuthority) return;

            if (mRegisteredPlayers.ContainsKey(playerRef))
            {
                mRegisteredPlayers.Remove(playerRef);
                mPlayerNames.Remove(playerRef);
                PlayerScores.Remove(playerRef);
                UpdateAllUI();
            }
        }

        private void OnPlayerDefeated(PlayerController attacker, PlayerController victim)
        {
            if (!HasStateAuthority) return;

            var attackerRef = attacker.Object.InputAuthority;
            var victimRef = victim.Object.InputAuthority;

            // Update attacker's score
            if (PlayerScores.TryGet(attackerRef, out var attackerScore))
            {
                attackerScore.kills++;
                attackerScore.score += 100;
                PlayerScores.Set(attackerRef, attackerScore);
                
                // Trigger UI update event
                OnScoreUpdated?.Invoke(attacker, attackerScore.score, attackerScore.kills);
            }

            // Update victim's deaths
            if (PlayerScores.TryGet(victimRef, out var victimScore))
            {
                victimScore.deaths++;
                PlayerScores.Set(victimRef, victimScore);
            }

            UpdateAllUI();
        }

        public void AddScore(PlayerController player, int points, string reason)
        {
            if (!HasStateAuthority) return;

            var playerRef = player.Object.InputAuthority;
            
            if (PlayerScores.TryGet(playerRef, out var playerScore))
            {
                playerScore.score += points;
                PlayerScores.Set(playerRef, playerScore);
                
                // Trigger UI update event
                OnScoreUpdated?.Invoke(player, playerScore.score, playerScore.kills);
                UpdateAllUI();
            }
        }

        public int GetPlayerScore(PlayerRef playerRef)
        {
            if (PlayerScores.TryGet(playerRef, out var playerScore))
            {
                return playerScore.score;
            }
            return 0;
        }

        public int GetPlayerKills(PlayerRef playerRef)
        {
            if (PlayerScores.TryGet(playerRef, out var playerScore))
            {
                return playerScore.kills;
            }
            return 0;
        }

        public string GetPlayerName(PlayerRef playerRef)
        {
            if (mPlayerNames.TryGetValue(playerRef, out var name))
            {
                return name;
            }
            return $"Player {playerRef.PlayerId}";
        }

        // Method for Game2DUI - returns PlayerScoreUI list
        public List<PlayerScoreUI> GetAllScores()
        {
            var allScores = new List<PlayerScoreUI>();

            foreach (var kvp in PlayerScores)
            {
                string playerName = GetPlayerName(kvp.Key);
                bool isNPC = mRegisteredPlayers.ContainsKey(kvp.Key) && mRegisteredPlayers[kvp.Key].IsNPC();
                
                allScores.Add(new PlayerScoreUI
                {
                    playerName = playerName,
                    totalScore = kvp.Value.score,
                    killCount = kvp.Value.kills,
                    deathCount = kvp.Value.deaths,
                    isNPC = isNPC,
                    playerRef = kvp.Key
                });
            }

            return allScores;
        }

        // Method for GameEndPanel - returns PlayerScoreUI list sorted by score
        public List<PlayerScoreUI> GetSortedScores()
        {
            var scores = GetAllScores();
            return scores.OrderByDescending(x => x.totalScore).ToList();
        }

        public List<(PlayerRef playerRef, PlayerScore playerScore, string playerName)> GetLeaderboard()
        {
            var leaderboard = new List<(PlayerRef playerRef, PlayerScore playerScore, string playerName)>();
    
            foreach (var kvp in PlayerScores)
            {
                string playerName = GetPlayerName(kvp.Key);
                leaderboard.Add((kvp.Key, kvp.Value, playerName));
            }
    
            return leaderboard.OrderByDescending(x => x.playerScore.score).ToList();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateUI()
        {
            UpdateLocalUI();
        }

        private void UpdateAllUI()
        {
            if (HasStateAuthority)
            {
                RPC_UpdateUI();
            }
        }

        private void UpdateLocalUI()
        {
            var ui = Game2DUI.Instance;
            if (ui == null) return;

            var localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                var localRef = localPlayer.Object.InputAuthority;
                ui.UpdateScore(GetPlayerScore(localRef));
                ui.UpdateKillCounter(GetPlayerKills(localRef)); // Fixed method name
            }

            // Update all players info
            var allScores = GetAllScores();
            ui.UpdateAllPlayersInfo(allScores);
        }
        

        private PlayerController FindLocalPlayer()
        {
            var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            return allPlayers.FirstOrDefault(p => p.Object.HasInputAuthority);
        }

        public void UpdatePlayerStats()
        {
            UpdateLocalUI();
        }
    }
}