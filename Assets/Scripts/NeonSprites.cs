using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Neon Arcology's robots, drawn like the ash world's enemies: W is the tinted chassis, D the dark plating,
    /// E a glowing eye or lens, and a husk's reactor core in R and C.
    /// </summary>
    public static class NeonSprites
    {
        private static Sprite drone, laserDrone, mech, scuttler, core;

        public static Sprite Enemy(bool ranged, bool tank) => tank ? Mech : ranged ? LaserDrone : Drone;

        /// <summary>A hovering orb with twin rotors and a wide lens.</summary>
        public static Sprite Drone => drone != null ? drone : drone = Build("Drone", new[]
        {
            "................", "..WW........WW..", ".WWWW......WWWW.", "..DD..WWWW..DD..",
            "...DDWWWWWWDD...", "....WWWWWWWW....", "...WWDDDDDDWW...", "...WDDEEEEDDW...",
            "...WDDEEEEDDW...", "...WWDDDDDDWW...", "....WWWWWWWW....", ".....WWWWWW.....",
            "......D..D......", ".....D....D.....", "................", "................"
        });

        /// <summary>A delta-winged gunship with a lens on top and its emitter underneath.</summary>
        public static Sprite LaserDrone => laserDrone != null ? laserDrone : laserDrone = Build("Laser drone", new[]
        {
            "................", ".......WW.......", "......WEEW......", ".....WWEEWW.....",
            "....WWWWWWWW....", "...WWDDDDDDWW...", "..WWDDEEEEDDWW..", ".WWWDDEEEEDDWWW.",
            "WWW.WDDDDDDW.WWW", "WW..WWWWWWWW..WW", "W....WWDDWW....W", "......WDDW......",
            "......WEEW......", ".......EE.......", "................", "................"
        });

        /// <summary>A squat walker with a visor slit and shoulder lamps.</summary>
        public static Sprite Mech => mech != null ? mech : mech = Build("Heavy mech", new[]
        {
            "....DDDDDDDD....", "...DWWWWWWWWD...", "...DWDEEEEDWD...", "...DWWWWWWWWD...",
            "DDDDDDWWWWDDDDDD", "DWWWDWWWWWWDWWWD", "DWEWDWWDDWWDWEWD", "DWWWDWWDDWWDWWWD",
            "DDDDDWWWWWWDDDDD", "..DDWWWWWWWWDD..", "...DWWDDDDWWD...", "..DWWWD..DWWWD..",
            "..DWWD....DWWD..", ".DDWWD....DWWDD.", ".DDDDD....DDDDD.", "................"
        });

        /// <summary>The arcology's skitter: a many-legged maintenance bot.</summary>
        public static Sprite Scuttler => scuttler != null ? scuttler : scuttler = Build("Scuttle bot", new[]
        {
            "................", "................", "D..............D", ".D...WWWWWW...D.",
            "..D.WWWWWWWW.D..", "...WWEWWWWEWW...", "D..WWDDDDDDWW..D", ".DDWWDWWWWDWWDD.",
            "...WWDDDDDDWW...", "D..WWWWWWWWWW..D", ".DD.WWWWWWWW.DD.", "...D.WWWWWW.D...",
            "..D..........D..", ".D............D.", "................", "................"
        });

        /// <summary>The arcology's husk: a boxy bot carrying an unstable reactor.</summary>
        public static Sprite Core => core != null ? core : core = Build("Volatile core", new[]
        {
            "................", "....DDDDDDDD....", "...DWWWWWWWWD...", "...DWEWWWWEWD...",
            "...DWWWWWWWWD...", ".DDDDDDDDDDDDDD.", ".DWWDRRRRRRDWWD.", ".DWWDRCCCCRDWWD.",
            ".DWWDRCCCCRDWWD.", ".DWWDRRRRRRDWWD.", ".DDDDDDDDDDDDDD.", "...DWWWWWWWWD...",
            "...DWWD..DWWD...", "...DDDD..DDDD...", "................", "................"
        });

        private static Sprite Build(string name, string[] rows) => DungeonVisuals.PaletteSprite(name, rows, key => key switch
        {
            'W' => new Color(0.85f, 0.87f, 0.9f),
            'D' => new Color(0.16f, 0.17f, 0.24f),
            'E' => Color.white,
            'R' => new Color(1f, 0.35f, 0.85f),
            'C' => new Color(1f, 0.92f, 1f),
            _ => Color.clear
        });
    }
}
