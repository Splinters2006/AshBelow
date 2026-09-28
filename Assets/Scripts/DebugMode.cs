using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Session-only debug admin mode (toggle with F1 or from the main menu).
    /// While enabled: the player cannot lose HP, every hit kills, dodge/class/relic cooldowns are skipped,
    /// and movement speed is doubled. It never touches the save file.
    /// </summary>
    public static class DebugMode
    {
        public const float SpeedMultiplier = 2f;
        public static bool Enabled { get; private set; }

        public static void Set(bool enabled) { Enabled = enabled; }
        public static void Toggle() { Enabled = !Enabled; }

        /// <summary>Returns zero while debug mode is enabled so cooldown checks always pass.</summary>
        public static float Cooldown(float remaining) => Enabled ? 0f : remaining;

        // Keeps the mode off at the start of every play session, even with domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() { Enabled = false; }
    }
}
