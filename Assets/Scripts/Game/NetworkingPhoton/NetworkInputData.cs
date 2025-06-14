using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 Movement;
    public NetworkButtons Buttons;
    public bool ButtonAHeld;
    public bool ButtonAReleased;
}

public enum InputButtons
{
    Sprint,
    ButtonA,
    ButtonB,
    ButtonC,
    ButtonD,
    ButtonE
}