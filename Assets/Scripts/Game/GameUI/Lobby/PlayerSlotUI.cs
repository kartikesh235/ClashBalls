using Game.NetworkingPhoton;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.UI;

namespace Game.UI
{
    public class PlayerSlotUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_Text slotNumberText;
        public TMP_Text playerNameText;
        public Image slotBackground;
        // public Image playerIcon;
        
        [Header("Slot Colors")]
        public Color emptySlotColor = Color.gray;
        public Color humanPlayerColor = Color.green;
        public Color npcPlayerColor = Color.blue;
        
        // [Header("Player Icons")]
        // public Sprite humanPlayerIcon;
        // public Sprite npcPlayerIcon;
        // public Sprite emptySlotIcon;
        
        public int SlotNumber { get; private set; }
        public PlayerSlotType SlotType { get; private set; }
        
        public void Initialize(int slotNumber)
        {
            SlotNumber = slotNumber;
            slotNumberText.text = $"Slot {slotNumber}";
            SetSlotType(PlayerSlotType.Empty, "Waiting...");
        }
        
        public void SetSlotType(PlayerSlotType slotType, string displayName)
        {
            SlotType = slotType;
            playerNameText.text = displayName;
            
            switch (slotType)
            {
                case PlayerSlotType.Empty:
                    slotBackground.color = emptySlotColor;
                    // playerIcon.sprite = emptySlotIcon;
                    break;
                    
                case PlayerSlotType.Human:
                    slotBackground.color = humanPlayerColor;
                    // playerIcon.sprite = humanPlayerIcon;
                    break;
                    
                case PlayerSlotType.NPC:
                    slotBackground.color = npcPlayerColor;
                    // playerIcon.sprite = npcPlayerIcon;
                    break;
            }
        }
    }
}
