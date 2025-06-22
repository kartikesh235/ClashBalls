using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using Game.Input;
using Game.AI;
using Game.Character;
using Game.Controllers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;

namespace Game.NetworkingPhoton
{   
    public class EnhancedPhotonLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("UI Elements")]
        public TMP_InputField roomInputField;
        public Button startLobbyBtn, fillNPCBtn, startGameBtn;
        public LobbyNPCConfiguration lobbyConfiguration;
        
        [Header("Network Configuration")]
        [SerializeField] private NetworkRunner mRunner;
        public NetworkPrefabRef playerPrefab;
        public NetworkPrefabRef npcPrefab;
        public NetworkPrefabRef ballPrefab;
        
        [Header("Game State")]
        private bool mIsLobbyActive = false;
        private bool mGameStarted = false;
        private List<NetworkObject> spawnedPlayers = new List<NetworkObject>();
        private List<NetworkObject> spawnedBalls = new List<NetworkObject>();

        private void Awake()
        {
            startLobbyBtn.onClick.AddListener(() => StartLobby());
            fillNPCBtn.onClick.AddListener(() => FillOneNPC());
            startGameBtn.onClick.AddListener(() => StartActualGame());
            
            // Initially disable NPC and start game buttons
            fillNPCBtn.interactable = false;
            startGameBtn.interactable = false;
        }

        private void StartLobby()
        {
            if (mRunner == null)
            {
                StartGame(GameMode.Host, roomInputField.text);
            }
        }

        private void FillOneNPC()
        {
            if (lobbyConfiguration != null)
            {
                lobbyConfiguration.AddOneNPC();
            }
        }

        private void StartActualGame()
        {
            if (mRunner != null && mRunner.IsServer)
            {
                // Load game scene and start actual gameplay
                mGameStarted = true;
                // Use LoadScene instead of SetActiveScene
                mRunner.LoadScene(SceneRef.FromIndex(1));
            }
        }

        async void StartGame(GameMode mode, string roomName)
        {
            if (mRunner == null)
            {
                mRunner = gameObject.AddComponent<NetworkRunner>();
            }

            DontDestroyOnLoad(mRunner.gameObject);
            mRunner.ProvideInput = true;
            mRunner.AddCallbacks(this);

            var sceneManager = mRunner.GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
                sceneManager = mRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();

            var mArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                SceneManager = sceneManager,
                Scene = SceneRef.FromIndex(0), // Stay in lobby scene initially
                PlayerCount = 4,
            };

            await mRunner.StartGame(mArgs);
            
            // Enable lobby functionality
            mIsLobbyActive = true;
            fillNPCBtn.interactable = true;
            startGameBtn.interactable = true;
            startLobbyBtn.interactable = false;
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            // Use IsServer instead of IsHost
            if (!runner.IsServer) return;

            if (mGameStarted )
            {
                SpawnAllPlayersAndNPCs(runner);
                SpawnAllBalls(runner);
            }
            StartCoroutine(ExtraUtils.SetDelay(3f, () =>
            {
              
            }));
            // Use CurrentScene instead of currentScene
        }

        private void SpawnAllPlayersAndNPCs(NetworkRunner runner)
{
    var spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint").OrderBy(x => x.name).ToArray();
    
    if (spawnPoints.Length < 4)
    {
        Debug.LogError("Need exactly 4 spawn points! Found: " + spawnPoints.Length);
        return;
    }

    var playerSlots = lobbyConfiguration.GetFinalPlayerSlots();
    
    for (int i = 0; i < 4; i++)
    {
        Vector3 spawnPosition = spawnPoints[i].transform.position;
        Quaternion spawnRotation = spawnPoints[i].transform.rotation;
        
        var slotInfo = playerSlots[i];
        
        if (slotInfo.slotType == PlayerSlotType.Human && slotInfo.playerRef.HasValue)
        {
            // Spawn human player
            var playerObj = runner.Spawn(playerPrefab, spawnPosition, spawnRotation, slotInfo.playerRef.Value);
            spawnedPlayers.Add(playerObj);
            
            // Assign camera to local player
            if (slotInfo.playerRef.Value == runner.LocalPlayer)
            {
                AssignCinemachineCamera(playerObj);
            }
        }
        else if (slotInfo.slotType == PlayerSlotType.NPC)
        {
            // Spawn NPC
            var npcObj = runner.Spawn(npcPrefab, spawnPosition, spawnRotation, null);
            
            // IMPORTANT: Disable human input IMMEDIATELY after spawn
            var humanInput = npcObj.GetComponent<MmInputService>();
            if (humanInput != null)
            {
                humanInput.enabled = false;
                Debug.Log($"Disabled MmInputService on {npcObj.name}");
            }
            
            // Configure NPC
            var npcController = npcObj.GetComponent<NetworkedNPCControllerNew>();
            if (npcController != null)
            {
                npcController.SetDifficulty(lobbyConfiguration.NPCDifficulty);
            }
            
            // Force PlayerController to reinitialize with correct input service
            var playerController = npcObj.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // Wait one frame then reinitialize
                StartCoroutine(ReinitializePlayerController(playerController));
            }
            
            spawnedPlayers.Add(npcObj);
        }
    }
    
    Debug.Log($"Spawned {spawnedPlayers.Count} total players/NPCs");
}

// Add this coroutine method
private System.Collections.IEnumerator ReinitializePlayerController(PlayerController playerController)
{
    yield return null; // Wait one frame
    
    // Force reinitialize input service
    var npcInput = playerController.GetComponent<NetworkedNPCControllerNew>();
    if (npcInput != null)
    {
        playerController.SetInputService(npcInput);
        Debug.Log($"Reinitialized {playerController.name} with NPC input service");
    }
}
        private void SpawnAllBalls(NetworkRunner runner)
        {
            var ballSpawnPoints = GameObject.FindGameObjectsWithTag("BallSpawnPoint").OrderBy(x => x.name).ToArray();
            
            if (ballSpawnPoints.Length < 4)
            {
                Debug.LogError("Need exactly 4 ball spawn points! Found: " + ballSpawnPoints.Length);
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 spawnPosition = ballSpawnPoints[i].transform.position;
                Quaternion spawnRotation = ballSpawnPoints[i].transform.rotation;
                
                var ballObj = runner.Spawn(ballPrefab, spawnPosition, spawnRotation, null);
                spawnedBalls.Add(ballObj);
            }
            
            Debug.Log($"Spawned {spawnedBalls.Count} balls");
        }

        private void AssignCinemachineCamera(NetworkObject player)
        {
            var vcam = FindFirstObjectByType<CinemachineCamera>();
            if (vcam != null)
            {
                vcam.Follow = player.transform;
                vcam.LookAt = player.transform;
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // Don't allow joining if game has started
            if (mGameStarted)
            {
                Debug.Log($"Player {player.PlayerId} tried to join after game started - disconnecting");
                runner.Disconnect(player);
                return;
            }

            // Don't allow more than 4 players total
            if (runner.ActivePlayers.Count() > 4)
            {
                Debug.Log($"Too many players - disconnecting {player.PlayerId}");
                runner.Disconnect(player);
                return;
            }
            
            // Add player to lobby
            if (lobbyConfiguration != null)
            {
                lobbyConfiguration.AddHumanPlayer(player, $"Player {player.PlayerId}");
            }
            
            Debug.Log($"Player {player.PlayerId} joined lobby");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            // Don't allow leaving after game started (or handle it differently)
            if (mGameStarted)
            {
                Debug.Log($"Player {player.PlayerId} left during game");
                // Could implement reconnection logic here
                return;
            }
            
            // Remove player from lobby
            if (lobbyConfiguration != null)
            {
                lobbyConfiguration.RemoveHumanPlayer(player);
            }
            
            Debug.Log($"Player {player.PlayerId} left lobby");
        }

        // Remaining INetworkRunnerCallbacks implementation...
        public void OnSceneLoadStart(NetworkRunner runner) {}
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnConnectedToServer(NetworkRunner runner) {}
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason) {}
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
        public void OnDisconnectedFromServer(NetworkRunner runner) {}
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();
            var inputService = FindFirstObjectByType<MmInputService>();

            if (inputService != null)
            {
                data.Movement = inputService.Movement;
                data.ButtonAHeld = inputService.ButtonAHeld;
                data.ButtonAReleased = inputService.ButtonAReleased;
                
                data.Buttons.Set(InputButtons.Sprint, inputService.Sprint);
                data.Buttons.Set(InputButtons.ButtonA, inputService.ButtonAPressed);
                data.Buttons.Set(InputButtons.ButtonB, inputService.ButtonBPressed);
                data.Buttons.Set(InputButtons.ButtonC, inputService.ButtonCPressed);
                data.Buttons.Set(InputButtons.ButtonD, inputService.ButtonDPressed);
                data.Buttons.Set(InputButtons.ButtonE, inputService.ButtonEPressed);
            }
            input.Set(data);
        }
    }
}