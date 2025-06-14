using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using Game.Input;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;

namespace Game.NetworkingPhoton
{   
    public class PhotonLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        public TMP_InputField roomInputField;
        public Button createRoomBtn, joinRoomBtn;
        [SerializeField] private NetworkRunner mRunner;
        public NetworkPrefabRef playerPrefab;
        public NetworkPrefabRef ball;

        private void Awake()
        {
            createRoomBtn.onClick.AddListener(() => StartGame(GameMode.Host, "TestRoom"));
            joinRoomBtn.onClick.AddListener(() => StartGame(GameMode.Client, roomInputField.text));
        }

        [ContextMenu("TestHost")]
        public void TestHost()
        {
            StartGame(GameMode.Host, "TestRoom");
        }
        
        [ContextMenu("TestClient")]
        public void TestClient()
        {
            StartGame(GameMode.Client, "TestRoom");
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

            var scene = SceneRef.FromIndex(1);

            var mArgs = new StartGameArgs()
            {
                GameMode = mode,
                SessionName = roomName,
                SceneManager = sceneManager,
                Scene = scene,
                PlayerCount = 4,
            };

            await mRunner.StartGame(mArgs);

            SceneManager.UnloadSceneAsync(0); // Unload Lobby scene after starting
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner.IsServer)
            {
                var spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint").OrderBy(x => x.name).ToArray();

                int index = runner.LocalPlayer.PlayerId - 1;
                if (index >= spawnPoints.Length) index = 0; // fallback

                Vector3 spawnPosition = spawnPoints[index].transform.position;
                Quaternion spawnRotation = spawnPoints[index].transform.rotation;

                var player = runner.Spawn(playerPrefab, spawnPosition, spawnRotation, runner.LocalPlayer);
                var ballObj = runner.Spawn(ball, spawnPosition + Vector3.up * 2, spawnRotation, null);
                AssignCinemachineCamera(player);
            }
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

        public void OnSceneLoadStart(NetworkRunner runner) {}

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.ActivePlayers.Count() > 4)
                runner.Disconnect(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {}
        public void OnConnectedToServer(NetworkRunner runner) {}
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason) {}
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }

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
            // mCounter = mCounter+1;
            // if(mCounter>5)
            // {    
            //     inputService.ResetPressedInputs(); // Clears buffers immediately after sync
            //     mCounter = 0;
            // }
        }
        private int mCounter = 0;
    }
}
