using Fusion;
using UnityEngine;
using Game.Input;
using Game.Abilities;
using Game.Character;
using Game.AI;

namespace Game.Controllers
{
    public class PlayerController : NetworkBehaviour
    {
         [SerializeField] private CharacterTypeSO characterType;
    private IInputService inputService;
    private CharacterStats stats;
    private BaseAbility[] abilities;

    public Transform ballTransformHolder;
    
    private void Awake()
    {
        stats = GetComponent<CharacterStats>();
        stats.Initialize(characterType);

        // PRIORITY: Check for NPC controller FIRST, then human input
        inputService = GetComponent<NetworkedNPCControllerNew>() as IInputService ?? 
                      GetComponent<MmInputService>() as IInputService;
        
        abilities = new BaseAbility[]
        {
            gameObject.GetComponent<PickUpAbility>(),
            gameObject.GetComponent<ThrowAbility>(),
            gameObject.GetComponent<DodgeAbility>(),
            gameObject.GetComponent<ParryAbility>(),
            gameObject.GetComponent<TackleAbility>(),
            gameObject.GetComponent<TauntAbility>()
        };

        InitializeAbilities();
    }


    public override void Spawned()
    {
        // Re-initialize after network spawn
        if (inputService == null || inputService is MmInputService)
        {
            // Force check for NPC controller first
            var npcInput = GetComponent<NetworkedNPCControllerNew>();
            var humanInput = GetComponent<MmInputService>();
            
            // If NPC controller exists and human input is disabled, use NPC
            if (npcInput != null && (humanInput == null || !humanInput.enabled))
            {
                inputService = npcInput;
                Debug.Log($"PlayerController {gameObject.name}: Using NPC input service");
            }
            else if (humanInput != null && humanInput.enabled)
            {
                inputService = humanInput;
                Debug.Log($"PlayerController {gameObject.name}: Using human input service");
            }
            
            InitializeAbilities();
        }
        
        // Debug verification
        if (inputService != null)
        {
            bool isNPC = inputService is NetworkedNPCControllerNew;
            Debug.Log($"PlayerController {gameObject.name}: Final input service = {inputService.GetType().Name} (IsNPC: {isNPC})");
        }
    }

        private void InitializeAbilities()
        {
            if (inputService != null && stats != null && characterType != null)
            {
                foreach (var ability in abilities)
                {
                    if (ability != null)
                        ability.Initialize(inputService, stats, characterType);
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (inputService != null)
            {
                foreach (var ability in abilities)
                {
                    if (ability != null)
                        ability.HandleInput();
                }
            }
        }

        public CharacterTypeSO GetCharacterTypeSO()
        {
            return characterType;
        }
       
        public bool IsNPC()
        {
            return GetComponent<NetworkedNPCControllerNew>() != null;
        }

        public void SetInputService(IInputService newInputService)
        {
            inputService = newInputService;
            InitializeAbilities();
        }
    }
}