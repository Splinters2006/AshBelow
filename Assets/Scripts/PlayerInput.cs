using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Slopgame
{
    public static class PlayerInput
    {
        public static bool ActiveQ
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.qKey.wasPressedThisFrame ?? false;
#else
                return Input.GetKeyDown(KeyCode.Q);
#endif
            }
        }
        public static bool ActiveE
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.eKey.wasPressedThisFrame ?? false;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            }
        }
        public static Vector2 Movement
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                return Vector2.ClampMagnitude(new Vector2(
                    (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                    (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0)), 1);
#else
                return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1);
#endif
            }
        }
        public static bool Attack
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.leftButton.isPressed ?? false;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }
        public static bool HeavyAttack
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.rightButton.wasPressedThisFrame ?? false;
#else
                return Input.GetMouseButtonDown(1);
#endif
            }
        }
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
        public static bool Dodge
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.spaceKey.wasPressedThisFrame ?? false;
#else
                return Input.GetKeyDown(KeyCode.Space);
#endif
            }
        }
        public static bool DebugToggle
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.f1Key.wasPressedThisFrame ?? false;
#else
                return Input.GetKeyDown(KeyCode.F1);
#endif
            }
        }
        public static bool Interact
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.fKey.wasPressedThisFrame ?? false;
#else
                return Input.GetKeyDown(KeyCode.F);
#endif
            }
        }
    }
}
