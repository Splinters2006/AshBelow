using UnityEngine;

namespace Slopgame
{
    /// <summary>The Infernal Court guardians' silhouettes, tinted by their accent colour like the Arcology machines.</summary>
    public static class InfernalBossSprites
    {
        private static Sprite matriarch, hound, judge;
        public static Sprite Matriarch => matriarch != null ? matriarch : matriarch = Build("Hex Matriarch", new[] {
            "....D..........D....", "...DWD...DD...DWD...", "...DWWD.DWWD.DWWD...", "..DWWWWDWWWWDWWWWD..",
            "..DDDDDDDDDDDDDDDD..", "...DWWWWWWWWWWWWD...", "...DWEEWWWWWWEEWD...", "...DWWWWWDDWWWWWD...",
            "....DWWDDDDDDWWD....", "..DDDWWWWWWWWWWDDD..", ".DWWDWWWWWWWWWWDWWD.", "DWWDWWWEWWWWEWWWDWWD",
            "DWDDWWWEWWWWEWWWDDWD", ".D.DWWWWWWWWWWWWD.D.", "...DWWWWWWWWWWWWD...", "..DWWWWWWWWWWWWWWD..",
            "..DWWDWWWWWWWWDWWD..", ".DWWDWWWWWWWWWWDWWD.", ".DWDDDWWWWWWWWDDDWD.", ".DDDDDDDDDDDDDDDDDD." });
        public static Sprite Hound => hound != null ? hound : hound = Build("Brimstone Hound", new[] {
            ".D................D.", ".DD..............DD.", "..DD..DDDDDDDD..DD..", "..DWDDWWWWWWWWDDWD..",
            ".DWWWWWWWWWWWWWWWWD.", "DWWWDDWWWWWWWWDDWWWD", "DWWDEEDWWWWWWDEEDWWD", "DWWWDDWWWWWWWWDDWWWD",
            ".DWWWWWWWWWWWWWWWWD.", "..DWWWWDDDDDDWWWWD..", "..DWWWDEDEEDEDWWWD..", "...DWWDDDDDDDDWWD...",
            "..DWWWWWWWWWWWWWWD..", ".DWWWWWWWWWWWWWWWWD.", "DWWDWWWWWWWWWWWWDWWD", "DWDDWWWWDDDDWWWWDDWD",
            "DWD.DWWD....DWWD.DWD", "DWD.DWWD....DWWD.DWD", "DDD.DDDD....DDDD.DDD", "...................." });
        public static Sprite Judge => judge != null ? judge : judge = Build("Infernal Judge", new[] {
            ".......D.DD.D.......", "......DEDEEDED......", "......DDDDDDDD......", ".....DWWWWWWWWD.....",
            ".....DWEWWWWEWD.....", ".....DWWWWWWWWD.....", "......DWDDDDWD......", "..D.DDWWWWWWWWDD.D..",
            ".DED.DWWWWWWWWD.DED.", "..D.DWWWWWWWWWWD.D..", "..DDWWWWWWWWWWWWDD..", "...DWWWEWWWWEWWWD...",
            "...DWWWEWWWWEWWWD...", "...DWWWWWWWWWWWWD...", "..DWWWWWWWWWWWWWWD..", "..DWWWWWWWWWWWWWWD..",
            ".DWWWWWWWWWWWWWWWWD.", ".DWWWWDWWWWWWDWWWWD.", "DWWWWWDWWWWWWDWWWWWD", "DDDDDDDDDDDDDDDDDDDD" });
        private static Sprite Build(string name, string[] rows) => DungeonVisuals.PaletteSprite(name, rows, key => key switch {
            'W' => Color.white, 'D' => new Color(0.14f, 0.04f, 0.07f), 'E' => new Color(1f, 0.95f, 0.75f), _ => Color.clear });
    }
}
