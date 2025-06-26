using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Game.Controllers;
using Game.GameUI;

namespace Game.Managers
{
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game Settings")]
        public float matchDuration = 90f;
        public GameObject scoreManagerPrefab;

        [Networked] public float TimeRemaining { get; private set; }
        [Networked] public bool GameStarted { get; private set; }
        [Networked] public bool GameEnded { get; private set; }

        public static System.Action OnGameEnded; // Event for GameEndPanel

        private ScoreManager mScoreManager;
        private List<PlayerController> mRegisteredPlayers = new List<PlayerController>();

        public override void Spawned()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                if (HasStateAuthority)
                {
                    InitializeGame();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeGame()
        {
            TimeRemaining = matchDuration;
            GameStarted = false;
            GameEnded = false;

            // Spawn ScoreManager
            if (scoreManagerPrefab != null)
            {
                var scoreManagerObj = Runner.Spawn(scoreManagerPrefab);
                mScoreManager = scoreManagerObj.GetComponent<ScoreManager>();
            }
            else
            {
                // Fallback - create ScoreManager on this object
                mScoreManager = gameObject.AddComponent<ScoreManager>();
            }

            StartCoroutine(RegisterPlayersAfterDelay());
        }

        private IEnumerator RegisterPlayersAfterDelay()
        {
            yield return new WaitForSeconds(1f);
            RegisterAllPlayers();
            StartGame();
        }

        public void RegisterAllPlayers()
        {
            if (!HasStateAuthority) return;

            var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            
            foreach (var player in allPlayers)
            {
                RegisterPlayer(player);
            }
        }

        public void RegisterPlayer(PlayerController player)
        {
            if (!HasStateAuthority || mRegisteredPlayers.Contains(player)) return;

            mRegisteredPlayers.Add(player);
            
            if (mScoreManager != null)
            {
                mScoreManager.RegisterPlayer(player);
            }

            RPC_UpdatePlayerRegistration(player.Object.Id);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdatePlayerRegistration(NetworkId playerId)
        {
            var playerObj = Runner.FindObject(playerId);
            if (playerObj != null)
            {
                var player = playerObj.GetComponent<PlayerController>();
                if (player != null && !mRegisteredPlayers.Contains(player))
                {
                    mRegisteredPlayers.Add(player);
                }
            }
        }

        public void StartGame()
        {
            if (!HasStateAuthority || GameStarted) return;

            GameStarted = true;
            TimeRemaining = matchDuration;
            
            RPC_GameStarted();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GameStarted()
        {
            if (Game2DUI.Instance != null)
            {
                Game2DUI.Instance.UpdateTimer(TimeRemaining);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !GameStarted || GameEnded) return;

            TimeRemaining -= Runner.DeltaTime;
            
            if (TimeRemaining <= 0f)
            {
                EndGame();
            }

            if (Runner.Tick % 30 == 0)
            {
                UpdateGameUI();
            }
        }

        private void UpdateGameUI()
        {
            RPC_UpdateGameUI(TimeRemaining);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateGameUI(float timeLeft)
        {
            if (Game2DUI.Instance != null)
            {
                Game2DUI.Instance.UpdateTimer(timeLeft);
            }

            if (mScoreManager != null)
            {
                mScoreManager.UpdatePlayerStats();
            }
        }

        private void EndGame()
        {
            if (GameEnded) return;

            GameEnded = true;
            TimeRemaining = 0f;
            
            RPC_GameEnded();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_GameEnded()
        {
            Debug.Log("Game Ended!");
    
            if (mScoreManager != null)
            {
                var leaderboard = mScoreManager.GetLeaderboard();
                Debug.Log("Final Leaderboard:");
                for (int i = 0; i < leaderboard.Count; i++)
                {
                    var entry = leaderboard[i];
                    Debug.Log($"{i + 1}. {entry.playerName} - Score: {entry.playerScore.score}");
                }
            }

            OnGameEnded?.Invoke();
        }

        public void UpdatePlayerStats()
        {
            if (mScoreManager != null)
            {
                mScoreManager.UpdatePlayerStats();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
                Instance = null;
        }
    }
}