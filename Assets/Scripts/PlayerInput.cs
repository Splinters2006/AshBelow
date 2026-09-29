using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Slopgame
{
    /// <summary>Gameplay input, read through the player's <see cref="KeyBindings"/>.</summary>
    public static class PlayerInput
    {
        public static bool ActiveQ => KeyBindings.WasPressed(GameAction.AbilityQ);
        public static bool ActiveE => KeyBindings.WasPressed(GameAction.AbilityE);
        public static Vector2 Movement => Vector2.ClampMagnitude(new Vector2(
            (KeyBindings.IsHeld(GameAction.MoveRight) ? 1 : 0) - (KeyBindings.IsHeld(GameAction.MoveLeft) ? 1 : 0),
            (KeyBindings.IsHeld(GameAction.MoveUp) ? 1 : 0) - (KeyBindings.IsHeld(GameAction.MoveDown) ? 1 : 0)), 1);
        public static bool Attack => KeyBindings.IsHeld(GameAction.Attack);
        public static bool HeavyAttack => KeyBindings.WasPressed(GameAction.Special);
        public static bool Dodge => KeyBindings.WasPressed(GameAction.Dodge);
        public static bool Interact => KeyBindings.WasPressed(GameAction.Interact);
        public static bool Mechanic => KeyBindings.WasPressed(GameAction.Mechanic);
        // Debug mode stays on F1 so it cannot be lost to a rebind.
        public static bool DebugToggle => KeyBindings.WasPressed(KeyCode.F1);
        public static Vector2 CursorPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.position.ReadValue() ?? new Vector2(Screen.width / 2f, Screen.height / 2f);
#else
                return Input.mousePosition;
#endif
            }
        }
    }
}
