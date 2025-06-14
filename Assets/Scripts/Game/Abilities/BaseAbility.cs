using Fusion;
using Game.Character;
using MoreMountains.TopDownEngine;
using Game.Input;
namespace Game.Abilities
{
    public abstract class BaseAbility : NetworkBehaviour
    {
        protected IInputService Input;
        protected CharacterStats Stats;
        protected CharacterTypeSO TypeData;
        public  void Initialize(IInputService inputService, CharacterStats stats, CharacterTypeSO typeData)
        {
            Input     = inputService;
            Stats     = stats;
            TypeData  = typeData;
        }
       
        public abstract void HandleInput();
    }
}