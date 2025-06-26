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
            public bool isNPC;
            public int objectId; // For NPCs, we'll use Object.Id
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
            public int objectId;
        }

        [Networked, Capacity(8)] // Increased capacity for multiple NPCs
        public NetworkDictionary<int, PlayerScore> PlayerScores => default; // Using int as key instead of PlayerRef

        private Dictionary<int, PlayerController> mRegisteredPlayers = new Dictionary<int, PlayerController>();
        private Dictionary<int, string> mPlayerNames = new Dictionary<int, string>();

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

            int playerId = GetUniquePlayerId(player);
            
            if (!mRegisteredPlayers.ContainsKey(playerId))
            {
                mRegisteredPlayers[playerId] = player;
                
                string playerName;
                bool isNPC = player.IsNPC();
                
                if (isNPC)
                {
                    // Create unique NPC names
                    int npcCount = mRegisteredPlayers.Values.Count(p => p.IsNPC()) + 1;
                    playerName = $"NPC {npcCount}";
                }
                else
                {
                    playerName = $"Player {player.Object.InputAuthority.PlayerId}";
                }
                
                mPlayerNames[playerId] = playerName;
                
                var newScore = new PlayerScore
                {
                    score = 0,
                    kills = 0,
                    deaths = 0,
                    playerRefId = isNPC ? 0 : player.Object.InputAuthority.PlayerId,
                    isNPC = isNPC,
                    objectId = (int)player.Object.Id.Raw
                };
                
                PlayerScores.Set(playerId, newScore);
                RPC_UpdatePlayerName(playerId, playerName);
                UpdateAllUI();
                
                Debug.Log($"Registered player: {playerName} with ID: {playerId} (IsNPC: {isNPC})");
            }
        }

        private int GetUniquePlayerId(PlayerController player)
        {
            if (player.IsNPC())
            {
                // For NPCs, use their Object.Id.Raw as unique identifier
                return (int)player.Object.Id.Raw;
            }
            else
            {
                // For humans, use their PlayerId but offset to avoid conflicts with NPC Object IDs
                return player.Object.InputAuthority.PlayerId + 1000000; // Large offset
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdatePlayerName(int playerId, string playerName)
        {
            mPlayerNames[playerId] = playerName;
        }

        public void UnregisterPlayer(PlayerController player)
        {
            if (!HasStateAuthority) return;

            int playerId = GetUniquePlayerId(player);
            
            if (mRegisteredPlayers.ContainsKey(playerId))
            {
                mRegisteredPlayers.Remove(playerId);
                mPlayerNames.Remove(playerId);
                PlayerScores.Remove(playerId);
                UpdateAllUI();
            }
        }

        private void OnPlayerDefeated(PlayerController attacker, PlayerController victim)
        {
            if (!HasStateAuthority) return;

            int attackerId = GetUniquePlayerId(attacker);
            int victimId = GetUniquePlayerId(victim);

            if (PlayerScores.TryGet(attackerId, out var attackerScore))
            {
                attackerScore.kills++;
                attackerScore.score += 100;
                PlayerScores.Set(attackerId, attackerScore);
                
                // Trigger UI update event
                OnScoreUpdated?.Invoke(attacker, attackerScore.score, attackerScore.kills);
            }

            if (PlayerScores.TryGet(victimId, out var victimScore))
            {
                victimScore.deaths++;
                PlayerScores.Set(victimId, victimScore);
            }

            UpdateAllUI();
        }

        public void AddScore(PlayerController player, int points, string reason)
        {
            if (!HasStateAuthority) return;

            int playerId = GetUniquePlayerId(player);
            
            if (PlayerScores.TryGet(playerId, out var playerScore))
            {
                playerScore.score += points;
                PlayerScores.Set(playerId, playerScore);
                
                // Trigger UI update event
                OnScoreUpdated?.Invoke(player, playerScore.score, playerScore.kills);
                UpdateAllUI();
            }
        }

        public int GetPlayerScore(PlayerController player)
        {
            int playerId = GetUniquePlayerId(player);
            if (PlayerScores.TryGet(playerId, out var playerScore))
            {
                return playerScore.score;
            }
            return 0;
        }

        public int GetPlayerKills(PlayerController player)
        {
            int playerId = GetUniquePlayerId(player);
            if (PlayerScores.TryGet(playerId, out var playerScore))
            {
                return playerScore.kills;
            }
            return 0;
        }

        public string GetPlayerName(int playerId)
        {
            if (mPlayerNames.TryGetValue(playerId, out var name))
            {
                return name;
            }
            return $"Unknown Player {playerId}";
        }

        // Method for Game2DUI - returns PlayerScoreUI list
        public List<PlayerScoreUI> GetAllScores()
        {
            var allScores = new List<PlayerScoreUI>();

            foreach (var kvp in PlayerScores)
            {
                string playerName = GetPlayerName(kvp.Key);
                
                allScores.Add(new PlayerScoreUI
                {
                    playerName = playerName,
                    totalScore = kvp.Value.score,
                    killCount = kvp.Value.kills,
                    deathCount = kvp.Value.deaths,
                    isNPC = kvp.Value.isNPC,
                    objectId = kvp.Value.objectId
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
                ui.UpdateScore(GetPlayerScore(localPlayer));
                ui.UpdateKillCounter(GetPlayerKills(localPlayer));
            }

            // Update all players info
            var allScores = GetAllScores();
            ui.UpdateAllPlayersInfo(allScores);
        }

        private PlayerController FindLocalPlayer()
        {
            var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            return allPlayers.FirstOrDefault(p => p.Object.HasInputAuthority && !p.IsNPC());
        }

        public void UpdatePlayerStats()
        {
            UpdateLocalUI();
        }
    }
}