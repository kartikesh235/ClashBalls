using Fusion;
using Game.GameUI;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Character;

namespace Game.Input
{
    public class MmInputService : NetworkBehaviour, IInputService
    {
        [Header("Mobile UI")]
        public MMTouchJoystick joystick;
        public Button buttonA, buttonB, buttonC, buttonD, buttonE;

        private bool mUIButtonAHeld;
        private bool mUIButtonBHeld;
        private bool mUIButtonCHeld;
        private bool mUIButtonDHeld;
        private bool mUIButtonEHeld;
        private bool mUIButtonAReleased,mUIButtonAWasHeld;

        private readonly float mBufferDuration = 0.1f;
        private float mBufferTimerA, mBufferTimerB, mBufferTimerC, mBufferTimerD, mBufferTimerE;
        private float mBufferTimerARelease;

        private float mCooldownTimerA, mCooldownTimerB, mCooldownTimerC, mCooldownTimerD, mCooldownTimerE;
        private float mCooldownDurationA, mCooldownDurationB, mCooldownDurationC, mCooldownDurationD, mCooldownDurationE;

        public CharacterTypeSO typeData;

        public Vector2 Movement { get; private set; }
        public bool Sprint { get; private set; }
        
        public bool ButtonAReleased { get; private set; }
        public bool ButtonAPressed { get; private set; }
        public bool ButtonAHeld { get; private set; }
        public bool ButtonBPressed { get; private set; }
        public bool ButtonCPressed { get; private set; }
        public bool ButtonDPressed { get; private set; }
        public bool ButtonEPressed { get; private set; }

        private void Initialize(CharacterTypeSO characterType)
        {
            typeData = characterType;
            mCooldownDurationA = typeData.pickThrowCooldown;
            mCooldownDurationB = typeData.dodgeCooldown;
            mCooldownDurationC = typeData.parryCooldown;
            mCooldownDurationD = typeData.tackleCooldown;
            mCooldownDurationE = 1f;
        }

        private void Start()
        {
            if (!HasInputAuthority) return;

            var ui = FindFirstObjectByType<Game2DUI>();
            Initialize(typeData);
            if (ui != null)
            {
                joystick = ui.joystick;
                buttonA = ui.buttonA;
                buttonB = ui.buttonB;
                buttonC = ui.buttonC;
                buttonD = ui.buttonD;
                buttonE = ui.buttonE;

                BindButtonTriggers(buttonA, () => { mUIButtonAHeld = true; mBufferTimerA = mBufferDuration; }, () => mUIButtonAHeld = false);
                BindButtonTriggers(buttonB, () => { mUIButtonBHeld = true; mBufferTimerB = mBufferDuration; }, () => mUIButtonBHeld = false);
                BindButtonTriggers(buttonC, () => { mUIButtonCHeld = true; mBufferTimerC = mBufferDuration; }, () => mUIButtonCHeld = false);
                BindButtonTriggers(buttonD, () => { mUIButtonDHeld = true; mBufferTimerD = mBufferDuration; }, () => mUIButtonDHeld = false);
                BindButtonTriggers(buttonE, () => { mUIButtonEHeld = true; mBufferTimerE = mBufferDuration; }, () => mUIButtonEHeld = false);
            }
        }

        private static void BindButtonTriggers(Button button, System.Action onDown, System.Action onUp)
        {
            if (button == null) return;

            var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => onDown());
            trigger.triggers.Add(down);

            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => onUp());
            trigger.triggers.Add(up);
        }

        private void Update()
        {
            if (!HasInputAuthority) return;

            Movement = joystick != null && joystick.Magnitude > 0.1f ? joystick.RawValue : new Vector2(UnityEngine.Input.GetAxis("Horizontal"), UnityEngine.Input.GetAxis("Vertical"));
            Sprint = Movement.magnitude > 0.8f;

            // Key Down Buffers
            if (UnityEngine.Input.GetKeyDown(KeyCode.A)) mBufferTimerA = mBufferDuration;
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) mBufferTimerB = mBufferDuration;
            if (UnityEngine.Input.GetKeyDown(KeyCode.C)) mBufferTimerC = mBufferDuration;
            if (UnityEngine.Input.GetKeyDown(KeyCode.D)) mBufferTimerD = mBufferDuration;
            if (UnityEngine.Input.GetKeyDown(KeyCode.E)) mBufferTimerE = mBufferDuration;

            // Key Up Buffers (for RELEASE detection)
            if (UnityEngine.Input.GetKeyUp(KeyCode.A)) 
                mBufferTimerARelease = mBufferDuration;
            if (!mUIButtonAHeld && mUIButtonAWasHeld) 
                mBufferTimerARelease = mBufferDuration; // UI release

            // Decrement timers
            mBufferTimerA -= Time.deltaTime;
            mBufferTimerB -= Time.deltaTime;
            mBufferTimerC -= Time.deltaTime;
            mBufferTimerD -= Time.deltaTime;
            mBufferTimerE -= Time.deltaTime;
            mBufferTimerARelease -= Time.deltaTime;
            
            ButtonAHeld = mUIButtonAHeld || UnityEngine.Input.GetKey(KeyCode.A);

            ButtonAPressed = mBufferTimerA > 0f;
            ButtonBPressed = mBufferTimerB > 0f;
            ButtonCPressed = mBufferTimerC > 0f;
            ButtonDPressed = mBufferTimerD > 0f;
            ButtonEPressed = mBufferTimerE > 0f;
            ButtonAReleased = mBufferTimerARelease > 0f;
            
            mCooldownTimerA -= Time.deltaTime;
            mCooldownTimerB -= Time.deltaTime;
            mCooldownTimerC -= Time.deltaTime;
            mCooldownTimerD -= Time.deltaTime;
            mCooldownTimerE -= Time.deltaTime;

            if (ButtonAPressed) mCooldownTimerA = mCooldownDurationA;
            if (ButtonBPressed) mCooldownTimerB = mCooldownDurationB;
            if (ButtonCPressed) mCooldownTimerC = mCooldownDurationC;
            if (ButtonDPressed) mCooldownTimerD = mCooldownDurationD;
            if (ButtonEPressed) mCooldownTimerE = mCooldownDurationE;

           // Game2DUI.SetCooldownMask(Game2DUI.Instance.buttonA,Game2DUI.Instance.buttonAMask, Game2DUI.Instance.cooldownTimerTextA, mCooldownTimerA, mCooldownDurationA);
            Game2DUI.SetCooldownMask(Game2DUI.Instance.buttonB,Game2DUI.Instance.buttonBMask, Game2DUI.Instance.cooldownTimerTextB, mCooldownTimerB, mCooldownDurationB);
            Game2DUI.SetCooldownMask(Game2DUI.Instance.buttonC,Game2DUI.Instance.buttonCMask, Game2DUI.Instance.cooldownTimerTextC, mCooldownTimerC, mCooldownDurationC);
            Game2DUI.SetCooldownMask(Game2DUI.Instance.buttonD,Game2DUI.Instance.buttonDMask, Game2DUI.Instance.cooldownTimerTextD, mCooldownTimerD, mCooldownDurationD);
            Game2DUI.SetCooldownMask(Game2DUI.Instance.buttonE,Game2DUI.Instance.buttonEMask, Game2DUI.Instance.cooldownTimerTextE, mCooldownTimerE, mCooldownDurationE);

            mUIButtonAWasHeld = mUIButtonAHeld;
        }
        
        public override void FixedUpdateNetwork()
        {
            if (!HasInputAuthority && Runner.TryGetInputForPlayer(Object.InputAuthority, out NetworkInputData input))
            {
                Movement = input.Movement;
                Sprint = input.Buttons.IsSet(InputButtons.Sprint);
                ButtonAPressed = input.Buttons.IsSet(InputButtons.ButtonA);
                ButtonAHeld = input.ButtonAHeld;
                ButtonBPressed = input.Buttons.IsSet(InputButtons.ButtonB);
                ButtonCPressed = input.Buttons.IsSet(InputButtons.ButtonC);
                ButtonDPressed = input.Buttons.IsSet(InputButtons.ButtonD);
                ButtonEPressed = input.Buttons.IsSet(InputButtons.ButtonE);
            }
        }

        public void ResetPressedInputs()
        {
            mBufferTimerARelease =mBufferTimerA = mBufferTimerB = mBufferTimerC = mBufferTimerD = mBufferTimerE = 0f;
            ButtonAReleased =ButtonAPressed = ButtonBPressed = ButtonCPressed = ButtonDPressed = ButtonEPressed = false;
        }
    }
}
