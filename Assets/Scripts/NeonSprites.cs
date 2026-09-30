using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Neon Arcology's robots, drawn like the ash world's enemies: L, W and M are the tinted chassis, D the dark plating,
    /// E a glowing eye or lens, and a husk's reactor core in R and C.
    /// </summary>
    public static class NeonSprites
    {
        private static Sprite drone, laserDrone, mech, scuttler, core;

        public static Sprite Enemy(bool ranged, bool tank) => tank ? Mech : ranged ? LaserDrone : Drone;

        /// <summary>A hovering orb with twin rotors and a wide lens.</summary>
        public static Sprite Drone => drone != null ? drone : drone = Build("Drone", new[]
        {
            "................", ".DLWMD....DLWMD.", "..DDD......DDD..", "...DD.DDDD.DD...",
            "....DDLWWMDD....", "...DLWWWWWWMD...", "..DLWDDDDDDWMD..", "..DWDDEEEEDDMD..",
            "..DWDDEEEEDDMD..", "..DWWDDDDDDMMD..", "...DMWWWWWWMD...", "....DMMWWMMD....",
            ".....DDDDDD.....", "......D..D......", ".....D....D.....", "................"
        });

        /// <summary>A delta-winged gunship with a lens on top and its emitter underneath.</summary>
        public static Sprite LaserDrone => laserDrone != null ? laserDrone : laserDrone = Build("Laser drone", new[]
        {
            "................", ".......DD.......", "......DEED......", ".....DLEEMD.....",
            "....DLWWWWMD....", "...DLWDDDDWMD...", "..DLWDDEEDDWMD..", ".DLWWDDEEDDWWMD.",
            "DLWDMWDDDDWMDWMD", "DWD.DMWWWWMD.DMD", "DD...DMWWMD...DD", "......DDDD......",
            "......DEED......", ".......EE.......", "................", "................"
        });

        /// <summary>A squat walker with a visor slit and shoulder lamps.</summary>
        public static Sprite Mech => mech != null ? mech : mech = Build("Heavy mech", new[]
        {
            "....DDDDDDDD....", "...DLWWWWWWMD...", "...DWDEEEEDMD...", "...DWWWWWWWMD...",
            "DDDDDDMWWMDDDDDD", "DLWWDLWWWWWDLWMD", "DWEWDWWDDWMDWEMD", "DWWMDWWDDWMDWMMD",
            "DDDDDMWWWWMDDDDD", "..DDLWWWWWWMDD..", "...DWMDDDDWMD...", "..DLWMD..DLWMD..",
            "..DWMD....DWMD..", ".DDWMD....DWMDD.", ".DDDDD....DDDDD.", "................"
        });

        /// <summary>The arcology's skitter: a many-legged maintenance bot.</summary>
        public static Sprite Scuttler => scuttler != null ? scuttler : scuttler = Build("Scuttle bot", new[]
        {
            "................", "................", "D..............D", ".D...DDDDDD...D.",
            "..D.DLWWWWMD.D..", "...DLEWWWWEMD...", "D..DWDDDDDDMD..D", ".DDDWDWWWWDMDDD.",
            "...DWDDDDDDMD...", "D..DWWWWWWWMD..D", ".DD.DMWWWWMD.DD.", "...D.DDDDDD.D...",
            "..D..........D..", ".D............D.", "................", "................"
        });

        /// <summary>The arcology's husk: a boxy bot carrying an unstable reactor.</summary>
        public static Sprite Core => core != null ? core : core = Build("Volatile core", new[]
        {
            "................", "....DDDDDDDD....", "...DLWWWWWWMD...", "...DWEWWWWEMD...",
            "...DWWWWWWMMD...", ".DDDDDDDDDDDDDD.", ".DLWDRRRRRRDWMD.", ".DWWDRCCCCRDWMD.",
            ".DWWDRCCCCRDWMD.", ".DWMDRRRRRRDMMD.", ".DDDDDDDDDDDDDD.", "...DLWWWWWWMD...",
            "...DWMD..DWMD...", "...DDDD..DDDD...", "................", "................"
        });

        private static Sprite Build(string name, string[] rows) => DungeonVisuals.ShadedSprite(name, rows, new Color(0.16f, 0.17f, 0.24f), key => key switch
        {
            'E' => Color.white,
            'R' => new Color(1f, 0.35f, 0.85f),
            'C' => new Color(1f, 0.92f, 1f),
            _ => Color.clear
        });
    }
}
