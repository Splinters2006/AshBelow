using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Slopgame
{
    public enum GameAction { MoveUp, MoveDown, MoveLeft, MoveRight, Attack, Special, Dodge, AbilityQ, AbilityE, Interact, Mechanic }

    /// <summary>
    /// Rebindable controls. Every action has a primary and an alternate binding, each a keyboard key or mouse button
    /// stored as a <see cref="KeyCode"/> (Mouse0 is the left button) and saved in PlayerPrefs.
    /// </summary>
    public static class KeyBindings
    {
        public const int Slots = 2;
        public static readonly GameAction[] Actions = (GameAction[])Enum.GetValues(typeof(GameAction));
        private const string PrefsPrefix = "keybind.";
        private static KeyCode[,] bindings;

        private static readonly KeyCode[,] Defaults =
        {
            { KeyCode.W, KeyCode.UpArrow },
            { KeyCode.S, KeyCode.DownArrow },
            { KeyCode.A, KeyCode.LeftArrow },
            { KeyCode.D, KeyCode.RightArrow },
            { KeyCode.Mouse0, KeyCode.None },
            { KeyCode.Mouse1, KeyCode.None },
            { KeyCode.Space, KeyCode.None },
            { KeyCode.Q, KeyCode.None },
            { KeyCode.E, KeyCode.None },
            { KeyCode.F, KeyCode.None },
            { KeyCode.R, KeyCode.None },
        };

        public static string ActionName(GameAction action) => action switch
        {
            GameAction.MoveUp => "Move up",
            GameAction.MoveDown => "Move down",
            GameAction.MoveLeft => "Move left",
            GameAction.MoveRight => "Move right",
            GameAction.Attack => "Attack / charge",
            GameAction.Special => "Class skill",
            GameAction.Dodge => "Dodge",
            GameAction.AbilityQ => "Relic ability 1",
            GameAction.AbilityE => "Relic ability 2",
            GameAction.Mechanic => "Class mechanic",
            _ => "Interact"
        };

        public static KeyCode Get(GameAction action, int slot)
        {
            Load();
            return bindings[(int)action, slot];
        }

        /// <summary>Binds a key; any other action (or slot) already using it is cleared so one key never does two things.</summary>
        public static void Set(GameAction action, int slot, KeyCode key)
        {
            Load();
            if (key != KeyCode.None)
                for (int a = 0; a < Actions.Length; a++)
                    for (int s = 0; s < Slots; s++)
                        if (bindings[a, s] == key && (a != (int)action || s != slot)) Store(a, s, KeyCode.None);
            Store((int)action, slot, key);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            Load();
            for (int a = 0; a < Actions.Length; a++)
                for (int s = 0; s < Slots; s++) Store(a, s, Defaults[a, s]);
            PlayerPrefs.Save();
        }

        private static void Store(int action, int slot, KeyCode key)
        {
            bindings[action, slot] = key;
            PlayerPrefs.SetInt(PrefsPrefix + Actions[action] + "." + slot, (int)key);
        }

        private static void Load()
        {
            if (bindings != null) return;
            bindings = new KeyCode[Actions.Length, Slots];
            for (int a = 0; a < Actions.Length; a++)
                for (int s = 0; s < Slots; s++)
                    bindings[a, s] = (KeyCode)PlayerPrefs.GetInt(PrefsPrefix + Actions[a] + "." + s, (int)Defaults[a, s]);
        }

        // ---------------------------------------------------------------- reading input

        public static bool IsHeld(GameAction action) => Check(action, false);
        public static bool WasPressed(GameAction action) => Check(action, true);

        private static bool Check(GameAction action, bool pressedThisFrame)
        {
            for (int s = 0; s < Slots; s++)
            {
                KeyCode key = Get(action, s);
                if (key != KeyCode.None && (pressedThisFrame ? WasPressed(key) : IsHeld(key))) return true;
            }
            return false;
        }

        public static bool IsHeld(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = Control(key);
            return control != null && control.isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        public static bool WasPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = Control(key);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static UnityEngine.InputSystem.Controls.ButtonControl Control(KeyCode key)
        {
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6)
            {
                var mouse = Mouse.current;
                if (mouse == null) return null;
                return key switch
                {
                    KeyCode.Mouse0 => mouse.leftButton,
                    KeyCode.Mouse1 => mouse.rightButton,
                    KeyCode.Mouse2 => mouse.middleButton,
                    KeyCode.Mouse3 => mouse.backButton,
                    KeyCode.Mouse4 => mouse.forwardButton,
                    _ => null
                };
            }
            var keyboard = Keyboard.current;
            Key mapped = ToKey(key);
            return keyboard == null || mapped == Key.None ? null : keyboard[mapped];
        }

        /// <summary>Most key names match between the two input systems; the rest are translated here.</summary>
        private static Key ToKey(KeyCode key)
        {
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9) return Key.Digit1 + (key - KeyCode.Alpha1);
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return Key.Numpad0 + (key - KeyCode.Keypad0);
            switch (key)
            {
                case KeyCode.None: return Key.None;
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftCommand: return Key.LeftMeta;
                case KeyCode.RightCommand: return Key.RightMeta;
                case KeyCode.KeypadPlus: return Key.NumpadPlus;
                case KeyCode.KeypadMinus: return Key.NumpadMinus;
                case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
                case KeyCode.KeypadDivide: return Key.NumpadDivide;
                case KeyCode.KeypadPeriod: return Key.NumpadPeriod;
            }
            return Enum.TryParse(key.ToString(), true, out Key mapped) ? mapped : Key.None;
        }
#endif

        // ---------------------------------------------------------------- labels

        /// <summary>A short label for a key, e.g. "LMB", "SPACE", "Q".</summary>
        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "-";
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.Mouse3: return "MOUSE 4";
                case KeyCode.Mouse4: return "MOUSE 5";
                case KeyCode.UpArrow: return "UP";
                case KeyCode.DownArrow: return "DOWN";
                case KeyCode.LeftArrow: return "LEFT";
                case KeyCode.RightArrow: return "RIGHT";
                case KeyCode.LeftShift: return "L-SHIFT";
                case KeyCode.RightShift: return "R-SHIFT";
                case KeyCode.LeftControl: return "L-CTRL";
                case KeyCode.RightControl: return "R-CTRL";
                case KeyCode.LeftAlt: return "L-ALT";
                case KeyCode.RightAlt: return "R-ALT";
                case KeyCode.Return: return "ENTER";
            }
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
            return key.ToString().ToUpperInvariant();
        }

        /// <summary>The primary binding's label, falling back to the alternate if the primary is cleared.</summary>
        public static string Label(GameAction action)
        {
            KeyCode key = Get(action, 0);
            return KeyName(key != KeyCode.None ? key : Get(action, 1));
        }

        /// <summary>"WASD" for the default movement keys, otherwise the four keys in up/left/down/right order.</summary>
        public static string MovementLabel()
        {
            string up = Label(GameAction.MoveUp), left = Label(GameAction.MoveLeft), down = Label(GameAction.MoveDown), right = Label(GameAction.MoveRight);
            return up.Length == 1 && left.Length == 1 && down.Length == 1 && right.Length == 1 ? up + left + down + right
                : $"{up}/{left}/{down}/{right}";
        }
    }
}
