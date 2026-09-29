using UnityEngine;

namespace Slopgame
{
    /// <summary>Distinct machine silhouettes, generated with the game's existing pixel palette renderer.</summary>
    public static class NeonBossSprites
    {
        private static Sprite overseer, bastion, core;
        public static Sprite Overseer => overseer != null ? overseer : overseer = Build("Grid Overseer", new[] {
            "...W........W...", "...WW......WW...", ".WWWWWWWWWWWWWW.", "WDDWWDDDDDDWWDDW",
            "WDWWWWWWWWWWWWDW", ".WWDDDDDDDDDDWW.", "..WDEEEEEEEEDW..", "..WDEEDDDDEEDW..",
            "..WDEEEEEEEEDW..", "..WWDDDDDDDDWW..", "...WWWWWWWWWW...", "....WDWWWWDW....",
            "....W.DWWD.W....", "...WW..WW..WW...", "...W........W...", "................" });
        public static Sprite Bastion => bastion != null ? bastion : bastion = Build("Siege Engine", new[] {
            "WWWW........WWWW", "WEEW..WWWW..WEEW", "WDDW.WWEEWW.WDDW", "WDDWWWWWWWWWWDDW",
            "WWWWDDDDDDDDWWWW", "WWWDDWWWWWWDDWWW", "WDDDWWEWWEWWDDDW", "WDDDWWWWWWWWDDDW",
            "WWWWDDWWWWDDWWWW", ".WWWWWWWWWWWWWW.", "..DDWWWWWWWWDD..", ".WDDWDDDDDDWDDW.",
            ".WDDW......WDDW.", ".WDDW......WDDW.", ".WWWW......WWWW.", "................" });
        public static Sprite Core => core != null ? core : core = Build("Singularity Core", new[] {
            "......WWWW......", "....WWDDDDWW....", "...WDDWWWWDDW...", "..WDWWDDDDWWDW..",
            ".WDWDDEEEEDDWDW.", ".WDWDEWWWEDWDW..", "WDWDEWDDWEDWDW..", "WDWDEWDDWEDWDW..",
            "WDWDEWDDWEDWDW..", ".WDWDEWWWEDWDW..", ".WDWDDEEEEDDWDW.", "..WDWWDDDDWWDW..",
            "...WDDWWWWDDW...", "....WWDDDDWW....", "......WWWW......", "................" });
        private static Sprite Build(string name, string[] rows) => DungeonVisuals.PaletteSprite(name, rows, key => key switch {
            'W' => Color.white, 'D' => new Color(0.12f, 0.14f, 0.22f), 'E' => new Color(1f, 0.95f, 1f), _ => Color.clear });
    }
}
