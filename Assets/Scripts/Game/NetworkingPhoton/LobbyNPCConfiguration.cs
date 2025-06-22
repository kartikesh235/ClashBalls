using Fusion;
using Game.AI;
using Game.UI;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Game.NetworkingPhoton
{
    public enum PlayerSlotType
    {
        Empty,
        Human,
        NPC
    }
    public class LobbyNPCConfiguration : MonoBehaviour
    {
        [Header("Lobby UI")]
        public TMP_Dropdown difficultyDropdown;
        public TMP_Text lobbyCodeText;
        
        [Header("Player Slots Display")]
        public Transform playerSlotsParent;
        public GameObject playerSlotPrefab;
        
        private PlayerSlotUI[] playerSlots = new PlayerSlotUI[4];
        private SlotInfo[] slotInfos = new SlotInfo[4];
        
        public NPCDifficulty NPCDifficulty { get; private set; } = NPCDifficulty.Medium;

        [System.Serializable]
        public struct SlotInfo
        {
            public PlayerSlotType slotType;
            public PlayerRef? playerRef;
            public string displayName;
        }

        private void Start()
        {
            InitializeUI();
            SetupPlayerSlots();
            InitializeSlots();
        }

        private void InitializeUI()
        {
            // Setup difficulty dropdown
            difficultyDropdown.options.Clear();
            difficultyDropdown.options.Add(new TMP_Dropdown.OptionData("Easy"));
            difficultyDropdown.options.Add(new TMP_Dropdown.OptionData("Medium"));
            difficultyDropdown.options.Add(new TMP_Dropdown.OptionData("Hard"));
            difficultyDropdown.value = 1; // Medium
            difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
            
            // Set lobby code (you can get this from NetworkRunner)
            lobbyCodeText.text = "Lobby Code: ABCD123";
        }

        private void SetupPlayerSlots()
        {
            // Clear existing slots
            foreach (Transform child in playerSlotsParent)
            {
                if(child.GameObject().activeSelf)
                    DestroyImmediate(child.gameObject);
            }

            // Create 4 player slots
            for (int i = 0; i < 4; i++)
            {
                GameObject slotObj = Instantiate(playerSlotPrefab, playerSlotsParent);
                slotObj.SetActive(true);
                PlayerSlotUI slot = slotObj.GetComponent<PlayerSlotUI>();
                if (slot == null)
                {
                    slot = slotObj.AddComponent<PlayerSlotUI>();
                }
                
                slot.Initialize(i + 1);
                playerSlots[i] = slot;
            }
        }

        private void InitializeSlots()
        {
            for (int i = 0; i < 4; i++)
            {
                slotInfos[i] = new SlotInfo
                {
                    slotType = PlayerSlotType.Empty,
                    playerRef = null,
                    displayName = "Waiting..."
                };
                UpdateSlotDisplay(i);
            }
        }

        private void OnDifficultyChanged(int value)
        {
            NPCDifficulty = (NPCDifficulty)value;
        }

        public void AddOneNPC()
        {
            // Find first empty slot and add NPC
            for (int i = 0; i < 4; i++)
            {
                if (slotInfos[i].slotType == PlayerSlotType.Empty)
                {
                    slotInfos[i] = new SlotInfo
                    {
                        slotType = PlayerSlotType.NPC,
                        playerRef = null,
                        displayName = $"NPC {GetNPCCount() + 1}"
                    };
                    UpdateSlotDisplay(i);
                    Debug.Log($"Added NPC to slot {i + 1}");
                    return;
                }
            }
            
            Debug.Log("No empty slots available for NPC");
        }

        public void AddHumanPlayer(PlayerRef playerRef, string playerName)
        {
            // Find first empty slot and add human player
            for (int i = 0; i < 4; i++)
            {
                if (slotInfos[i].slotType == PlayerSlotType.Empty)
                {
                    slotInfos[i] = new SlotInfo
                    {
                        slotType = PlayerSlotType.Human,
                        playerRef = playerRef,
                        displayName = playerName
                    };
                    UpdateSlotDisplay(i);
                    Debug.Log($"Added human player {playerName} to slot {i + 1}");
                    return;
                }
            }
            
            Debug.LogWarning("No empty slots available for human player");
        }

        public void RemoveHumanPlayer(PlayerRef playerRef)
        {
            for (int i = 0; i < 4; i++)
            {
                if (slotInfos[i].slotType == PlayerSlotType.Human && 
                    slotInfos[i].playerRef.HasValue && 
                    slotInfos[i].playerRef.Value == playerRef)
                {
                    slotInfos[i] = new SlotInfo
                    {
                        slotType = PlayerSlotType.Empty,
                        playerRef = null,
                        displayName = "Waiting..."
                    };
                    UpdateSlotDisplay(i);
                    Debug.Log($"Removed human player from slot {i + 1}");
                    return;
                }
            }
        }

        private void UpdateSlotDisplay(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < playerSlots.Length)
            {
                var info = slotInfos[slotIndex];
                playerSlots[slotIndex].SetSlotType(info.slotType, info.displayName);
            }
        }

        private int GetNPCCount()
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if (slotInfos[i].slotType == PlayerSlotType.NPC)
                    count++;
            }
            return count;
        }

        public int GetTotalPlayersCount()
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if (slotInfos[i].slotType != PlayerSlotType.Empty)
                    count++;
            }
            return count;
        }

        public SlotInfo[] GetFinalPlayerSlots()
        {
            return slotInfos;
        }

        public bool CanStartGame()
        {
            return GetTotalPlayersCount() >= 2; // At least 2 players to start
        }
    }
}