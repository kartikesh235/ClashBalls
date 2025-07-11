using Fusion;
using UnityEngine;
using Game.Input;
using Game.Abilities;
using Game.Character;
using Game.AI;
using Game.Ball;
using Game.Managers;

namespace Game.Controllers
{
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] private CharacterTypeSO characterType;
        private IInputService inputService;
        private CharacterStats stats;
        private BaseAbility[] abilities;

        public Transform ballTransformHolder;
        public BallController ball;
        
        private bool mHasRegisteredWithGameManager = false;
        
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
            
            // Register with GameManager after a short delay
            if (HasStateAuthority && !mHasRegisteredWithGameManager)
            {
                StartCoroutine(RegisterWithGameManagerDelayed());
            }
            
            // Debug verification
            if (inputService != null)
            {
                bool isNPC = inputService is NetworkedNPCControllerNew;
                Debug.Log($"PlayerController {gameObject.name}: Final input service = {inputService.GetType().Name} (IsNPC: {isNPC})");
            }
        }

        private System.Collections.IEnumerator RegisterWithGameManagerDelayed()
        {
            yield return new WaitForSeconds(0.5f);
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterPlayer(this);
                mHasRegisteredWithGameManager = true;
                Debug.Log($"Registered {gameObject.name} with GameManager");
            }
            else
            {
                Debug.LogWarning($"GameManager not found when trying to register {gameObject.name}");
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

        public override void Despawned(NetworkRunner runner, bool hasState)
{
    // Unregister from ScoreManager if we were registered
    if (mHasRegisteredWithGameManager && ScoreManager.Instance != null)
    {
        ScoreManager.Instance.UnregisterPlayer(this);
        Debug.Log($"Unregistering {gameObject.name} from ScoreManager");
    }
}
    }
}