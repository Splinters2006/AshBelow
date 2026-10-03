using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The way between floors, drawn by <see cref="DescentIris"/>: taking the stairs closes the screen on them as the hero
    /// walks down, it stays dark over the floor's talent pick, and every new floor opens again on the hero. Runs on
    /// unscaled time, since a solo talent pick freezes the game. Purely cosmetic and local.
    /// </summary>
    public static class FloorTransition
    {
        public const float Close = 0.5f, Open = 0.55f;
        private static float closingAt = -1f, openingAt = -1f;
        private static bool held;

        /// <summary>Where the screen closes onto (the stairs) while it is closing or shut.</summary>
        public static Vector2 Focus { get; private set; }
        public static bool IsClosing => closingAt >= 0f;
        /// <summary>True once the screen has closed all the way.</summary>
        public static bool IsShut => IsClosing && Time.unscaledTime - closingAt >= Close;
        public static bool IsHeld => held;

        public static void BeginClose(Vector2 focus)
        {
            Focus = focus;
            closingAt = Time.unscaledTime;
            openingAt = -1f;
            held = false;
        }

        /// <summary>The close is done: the screen stays dark until <see cref="BeginOpen"/>.</summary>
        public static void Hold() { closingAt = -1f; held = true; }

        public static void BeginOpen()
        {
            closingAt = -1f;
            held = false;
            openingAt = Time.unscaledTime;
        }

        public static void Clear() { closingAt = openingAt = -1f; held = false; }

        /// <summary>How open the screen is, 0 (black) to 1 (clear), and whether it centres on <see cref="Focus"/>.</summary>
        public static float Openness(out bool onFocus)
        {
            onFocus = IsClosing || held;
            if (held) return 0f;
            if (IsClosing)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - closingAt) / Close);
                // Quickest at the last, like the party hall's pull into the portal.
                return (1f - t) * (1f - t);
            }
            if (openingAt < 0f) return 1f;
            float open = (Time.unscaledTime - openingAt) / Open;
            if (open >= 1f) { openingAt = -1f; return 1f; }
            return open <= 0f ? 0f : 1f - (1f - open) * (1f - open) * (1f - open);
        }
    }
}
