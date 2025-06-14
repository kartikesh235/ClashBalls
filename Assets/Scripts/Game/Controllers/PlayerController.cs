using Fusion;
using UnityEngine;
using Game.Input;
using Game.Abilities;
using Game.Character;

namespace Game.Controllers
{
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] private CharacterTypeSO characterType;
        [SerializeField] private MmInputService inputService;
        private CharacterStats stats;
        private BaseAbility[] abilities;

        public Transform ballTransformHolder;
        private void Awake()
        {
            stats = GetComponent<CharacterStats>();
            stats.Initialize(characterType);

            abilities = new BaseAbility[]
            {
                gameObject.GetComponent<PickUpAbility>(),
                gameObject.GetComponent<ThrowAbility>(),
                gameObject.GetComponent<DodgeAbility>(),
                gameObject.GetComponent<ParryAbility>(),
                gameObject.GetComponent<TackleAbility>(),
                gameObject.GetComponent<TauntAbility>()
            };

            foreach (var a in abilities)
                a.Initialize(inputService, stats, characterType);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            foreach (var mAbility in abilities)
                mAbility.HandleInput();
        }
    }
}