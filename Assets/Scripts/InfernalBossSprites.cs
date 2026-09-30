using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Infernal Court guardians' 40x40 pixel art, drawn as left halves and mirrored by <see cref="BossArt"/>: a shaded
    /// body (L highlight, W base, M shade, D outline) tinted by the boss's body colour, and a fixed-colour detail layer on
    /// top for faces, gold, gems, horns and magma.
    /// </summary>
    public static class InfernalBossSprites
    {
        private static Sprite matriarch, matriarchDetails, hound, houndDetails, judge, judgeDetails;

        // The Hex Matriarch: spiked gold crown with violet gems, long black-violet hair, a pale face with glowing violet eyes,
        // a gold collar and hex brooch, flared sleeves with hexfire orbs in her hands, and a gown with a gold hem.
        private static readonly string[] MatriarchGrid =
        {
            "...................G", "...................G", "..............G...gG", "..............Gg.gGG",
            "..........G...GGgGGG", "..........Gg..GGGYYG", "...........GgGGGGYYG", "...........gGGGGGGGG",
            "..........KKgggggggg", ".........KKhhKKKKKKK", "........KKhhKKKKhKKK", ".......KKhKKKKhKSSSS",
            ".......KhKKKKhSSSSSS", "......KKhKKKKSSSSSSS", "......KhhKKSKKKKSSSS", "......KhKKKSKsEEKSSS",
            "......KhKKKSSKEYKsSS", "......KhKKKsSSKKSSsS", ".....KKhKKKsSSSSSSsS", ".....KhhKKKKsSSSSSSs",
            ".....KhKKKK.KsSSSRRR", ".....KhKKKK..KsssSSS", "....KKhKKDDD..DKssss", "....KhhKDLWDDDGDDGGG",
            "...KKhKDLWWMWWWGGYGG", "...KhKDLWWMDDMWWWGOY", "..KKhKDWWMD.DMWWWGOO", "..KhKDLWMD...DWWWGGG",
            "..KhKDWWMD..DMWWWWLW", "..Kh.DWMD..DMWWWWLWW", "..K..DWMD.DMWWWWLWWW", "....DSSSDOOODWWWLWWW",
            "....DSsSOYYYODWLWWWW", "....DDsDOYYYODWLWWMW", ".....DD.OOOOODWLWWMW", "........DDOODLWWWWMW",
            ".......DLWWWWWWWMWWW", "......DLWWWWWMWWWWWM", ".....DGgGgGgGgGgGgGg", ".....DDDDDDDDDDDDDDD"
        };

        // Gorgoth: curling bone horns and pointed ears, a snarling muzzle with a molten throat and fangs, shaggy neck fur,
        // lava cracks through its hide and four clawed legs.
        private static readonly string[] HoundGrid =
        {
            "....................", "..hH................", "..hHH...............", "...hHH..............",
            "....hHH.............", ".D...hHH............", ".DLD..hHH....DD.....", ".DLWD..hHDDDDLWD....",
            "..DWWDDLWWWWWWWWDDDD", "..DWWWLWWWWWWWWWWWWW", "...DWWWWWMMMWWWWWWWW", "...DWWWWMDDDDMWWWWWW",
            "....DWWMWDDKKDDWWWWW", "....DWWDEYEKDDMWWWWW", "....DWWDEEKDMWWWWWLL", "....DWWWDDDMWWWWLLWW",
            ".....DWWWWWWWWLLWDDD", "..D..DWWWWWWWLLWDKKK", "..DD.DWWWWWWWLWWDKKK", ".DWD..DWWWWWLWWWWDDD",
            ".DWWD.DWWWWWWWDDDMMM", "..DWWDDWWWWWDDFKDDDD", "..DWWWDWWWWDFFKOOOOO", "...DWWWWWWWDDFKOYYYY",
            "..DDWWWWWWWWDFFKOOOO", ".DLWWWOWWWWWWDDFFKKK", ".DWWWOWWWWWWWWDDDDDD", "DLWWMWOOWWWWWWWWOWWW",
            "DWWMWWWOWWWWWWWOOWWW", "DWWMWWWWWWWWWWMWOWWW", "DWMDDWWWWWWWWMDWWWWW", "DWMD.DWWWWWWMD.DWWWW",
            "DWMD.DLWWWWWMD..DWWW", "DWMD.DWWWWWWMD..DWWW", "DWWD.DWWWWWWMD..DWWW", "DWWD.DWWWWWMMD..DWWM",
            "DWWMDDWWWWWMMD.DWWWM", "DFWFWDFWFWFWFD.DFWFW", ".F.F..F.F.F.F...F.F.", "...................."
        };

        // Vassago: dark curved horns around a tall gold mitre, a crimson face with a braided beard, and judge's robes with
        // a gold stole, an ember seal at the chest and a gold hem.
        private static readonly string[] JudgeGrid =
        {
            "...................Y", "..................GY", ".................GGG", ".HH.............GGYG",
            ".hHH...........gGGGG", "..hHH.........gGGGGG", "..hhHH.......gGGYGGG", "...hhHH.....gGGGGGGG",
            "....hhHHH..gGGGGGGGG", ".....hhhHHDDDDDDDDDD", ".......hhDSSSSSSSSSS", "........DSSSSSSSSSSS",
            "........DSKKKKSSSSSS", "........DSsKEEKSSSSs", "........DSSsKKSSSSsS", "........DsSSSSSSSsSS",
            "........DsSSSSSSSKsK", "........DsSSSSSSSSSS", "........DbsSSKKKKKKK", ".........DbbSSSSSSSS",
            ".........DbBbBbBbBbB", "........DDDbBbBbBbBb", "......DDLWDDbBbBbBbB", ".....DLWWWWDDbBbBbBb",
            "....DLWWWWWGGDbBbBbB", "...DLWWWMWWGWGDDDDGG", "...DWWWMDMWWGWWWWGOO", "..DLWWMD.DMWGWWWWGOY",
            "..DWWMD..DWWGWWWWWGG", "..DWWMD..DWWGWWWWWWW", "..DWMD...DWWGWWWWWLW", "..DSSD...DWWGWWWWWLW",
            "..DSsD..DLWWGWWWWWLW", "...DD...DWWWGWWWWWLW", ".......DLWWWGWWWWWMW", "......DLWWWMGWWWWMWW",
            "......DWWWWMGWWWMWWW", ".....DLWWWMWGWWWMWWW", ".....DGGGGGGGGGGGGGG", ".....DDDDDDDDDDDDDDD"
        };

        public static Sprite Matriarch => matriarch != null ? matriarch : matriarch = BossArt.Build("Hex Matriarch", MatriarchGrid, false, MatriarchColor);
        public static Sprite MatriarchDetails => matriarchDetails != null ? matriarchDetails
            : matriarchDetails = BossArt.Build("Hex Matriarch details", MatriarchGrid, true, MatriarchColor);
        public static Sprite Hound => hound != null ? hound : hound = BossArt.Build("Brimstone Hound", HoundGrid, false, HoundColor);
        public static Sprite HoundDetails => houndDetails != null ? houndDetails : houndDetails = BossArt.Build("Brimstone Hound details", HoundGrid, true, HoundColor);
        public static Sprite Judge => judge != null ? judge : judge = BossArt.Build("Infernal Judge", JudgeGrid, false, JudgeColor);
        public static Sprite JudgeDetails => judgeDetails != null ? judgeDetails : judgeDetails = BossArt.Build("Infernal Judge details", JudgeGrid, true, JudgeColor);

        // G gold, g dark gold, Y gem glow, K hair, h hair sheen, S skin, s skin shade, E eyes, R lips, O hexfire.
        private static Color MatriarchColor(char c)
        {
            switch (c)
            {
                case 'G': return new Color(0.98f, 0.78f, 0.32f);
                case 'g': return new Color(0.62f, 0.44f, 0.16f);
                case 'Y': return new Color(0.95f, 0.8f, 1f);
                case 'K': return new Color(0.08f, 0.02f, 0.12f);
                case 'h': return new Color(0.3f, 0.12f, 0.4f);
                case 'S': return new Color(0.92f, 0.86f, 0.96f);
                case 's': return new Color(0.68f, 0.58f, 0.76f);
                case 'E': return new Color(0.8f, 0.35f, 1f);
                case 'R': return new Color(0.42f, 0.06f, 0.28f);
                case 'O': return new Color(0.72f, 0.3f, 1f);
                default: return Color.clear;
            }
        }

        // H horn, h horn shade, E eyes, Y eye core, K maw and nose, F fangs and claws, O magma.
        private static Color HoundColor(char c)
        {
            switch (c)
            {
                case 'H': return new Color(0.9f, 0.84f, 0.7f);
                case 'h': return new Color(0.52f, 0.42f, 0.34f);
                case 'E': return new Color(1f, 0.8f, 0.25f);
                case 'Y': return new Color(1f, 1f, 0.85f);
                case 'K': return new Color(0.06f, 0f, 0.02f);
                case 'F': return new Color(0.97f, 0.94f, 0.86f);
                case 'O': return new Color(1f, 0.45f, 0.08f);
                default: return Color.clear;
            }
        }

        // H horn, h horn shade, G gold, g dark gold, Y ember, S skin, s skin shade, K brow and mouth, E eyes, F teeth, O seal, B/b beard.
        private static Color JudgeColor(char c)
        {
            switch (c)
            {
                case 'H': return new Color(0.3f, 0.16f, 0.14f);
                case 'h': return new Color(0.1f, 0.04f, 0.05f);
                case 'G': return new Color(1f, 0.8f, 0.35f);
                case 'g': return new Color(0.62f, 0.44f, 0.16f);
                case 'Y': return new Color(1f, 0.45f, 0.1f);
                case 'S': return new Color(0.75f, 0.22f, 0.2f);
                case 's': return new Color(0.46f, 0.1f, 0.1f);
                case 'K': return new Color(0.05f, 0f, 0.02f);
                case 'E': return new Color(1f, 0.9f, 0.4f);
                case 'F': return new Color(0.95f, 0.9f, 0.8f);
                case 'O': return new Color(1f, 0.5f, 0.1f);
                case 'B': return new Color(0.28f, 0.07f, 0.06f);
                case 'b': return new Color(0.16f, 0.04f, 0.04f);
                default: return Color.clear;
            }
        }
    }
}
