using UnityEngine;
namespace Game.Input
{
    public interface IInputService
    {
        Vector2 Movement { get; }
        bool Sprint { get; }
        bool ButtonAPressed { get; }
        bool ButtonAReleased { get; }
        bool ButtonAHeld { get; }
        bool ButtonBPressed { get; }
        bool ButtonCPressed { get; }
        bool ButtonDPressed { get; }
        bool ButtonEPressed { get; }
    }
}