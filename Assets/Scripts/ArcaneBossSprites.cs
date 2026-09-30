using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Arcane Spire guardians' 40x40 pixel art, drawn as left halves and mirrored by <see cref="BossArt"/>: a shaded
    /// robe (L highlight, W base, M shade, D outline) tinted by the boss's body colour, and a fixed-colour detail layer on
    /// top for faces, armour, ice and gold. Their swords and staffs are drawn by the bosses themselves, so they can swing.
    /// </summary>
    public static class ArcaneBossSprites
    {
        private static Sprite spellblade, spellbladeDetails, archmage, archmageDetails, magister, magisterDetails;

        // Selwyn: a deep hood with a gold circlet and cyan gem, glowing eyes over a steel face mask, silver pauldrons and
        // breastplate set with runes, a gold rune belt and a long robe with a rune-stitched hem.
        private static readonly string[] SpellbladeGrid =
        {
            "....................", "....................", "................DDDD", "..............DDLLLW",
            ".............DLLWWWW", "............DLWWWWWW", "...........DLWWWWWWW", "...........DWWWWMMMM",
            "..........DLWWWMKKKK", "..........DWWWMKGGGC", "..........DWWWMKKKKK", "..........DWWMKKSSSS",
            "..........DWWMKSEESS", "..........DWWMKSSSSS", "..........DWWMKaaaaa", "..........DWMMMKaAaA",
            "........aAAADMMMKKKK", "......aAAAAAAaDMMDDD", ".....aAACAAAAAaDDaAA", ".....aAAAAAAAAaDaAAA",
            ".....aaAAAAAAaaDaAAC", "......DaaaaaaDaAAAAA", "......DWWMDDDaAAACAA", ".....DLWWMD.DaAAAAAC",
            ".....DWWWMD.DaaAAAAA", ".....DWWMD..DWaaaaaa", "....DSSSD...DWGGGGGG", "....DSSsD...DWGGCGGC",
            ".....DDD...DLWWWWWWW", "...........DLWWMWWWW", "..........DLWWWMWWWL", "..........DWWWMWWWLW",
            ".........DLWWWMWWWLW", ".........DWWWMWWWWLW", "........DLWWMWWWWLWW", "........DWWWMWWWWLWW",
            ".......DLWWMWWWWLWWW", ".......DGCGGGCGGGCGG", "...........DaaaD....", "...................."
        };

        // Isolde: a crown of ice spikes with a frost gem, long white hair framing a pale face with glowing eyes, a high
        // ice collar and a flowing gown with an ice-crystal hem.
        private static readonly string[] ArchmageGrid =
        {
            "...................I", "..............I....I", "..............I..IiI", "...........I..Ii.IiI",
            "...........Ii.IiIIiI", "...........IiIIiIiYI", "............iiiiiiii", "..........HHHHHHHHHH",
            ".........HHhHHHHHHHH", "........HHhHHHHSSSSS", "........HhHHHHSSSSSS", "........HhHHHSSSSSSS",
            ".......HHhHHHSSEESSS", ".......HhHHHHSSSSSSS", ".......HhHHHHSSSSSsS", "......HHhHHHHSSSSSRR",
            "......HhHHHHHHsSSSSS", "......HhHHHIiIsssIiI", ".....HHhHHDLIiIIIiIL", ".....HhHHDLWWLIIiLWW",
            ".....HhHDLWWWWLLLWWY", "....HHhDLWWWMWWWWWWW", "....HhHDWWWMDWWWWWWW", "....HhDLWWMD.DWWWWWW",
            "...HHhDWWMD..DWWWWLW", "...HhHDWMD..DMWWWWLW", "...Hh.DFFD.DMWWWWLWW", "...H..DFfD.DWWWWWLWW",
            "......DDD.DLWWWWLWWW", "..........DWWWWMLWWW", ".........DLWWWMWLWWW", ".........DWWWWMWLWWW",
            "........DLWWWMWWLWWW", "........DWWWMWWLWWWW", ".......DLWWWMWWLWWWW", ".......DWWWMWWWLWWWW",
            "......DLWWMWWWLWWWWW", "......DIiIIIiIIIiIII", "......DDDDDDDDDDDDDD", "...................."
        };

        // Aurelion: a towering starred wizard hat with a gold band and wide brim, glowing eyes in its shadow, a long white
        // beard, and gold-trimmed robes with a gem clasp and a gem-set hem.
        private static readonly string[] MagisterGrid =
        {
            "...................D", "..................DL", ".................DLW", "................DLWW",
            "...............DLWWW", "..............DLWYWW", ".............DLWWWWW", "............DLWWWWWW",
            "...........DLWWWWWYM", "..........DLWWYWWWMM", ".........DLWWWWWWMMM", "........DLWWWWWWMGGG",
            "........DMMMMMMMGGYG", "..DDDDDDDLLLLLLLLLLL", ".DLLWWWWWWWWWWWWWWWW", "..DDDDMMMMMMMMMMMMMM",
            "......DDKKKKKKKKKKKK", "........DKKKKKKKKKKK", "........DKSSSSEESSSS", "........DKSSSSSSSSsS",
            "........DBBSSSSSSSsS", ".......DBBBBSSSSSBBB", ".......DBBBBBBBBBBBB", "......DWDBBBBBBBBBBB",
            ".....DLWWDBBBBBBBBBB", "....DLWWWMDBBBBBBBBB", "...DLWWWMDGDBBBBBBBB", "...DWWWMDGWWDBBBBBBB",
            "..DLWWMDGWWWWDBBBBBB", "..DWWMD.GWWWWWDBBBBB", "..DSSD..GWWWWWWDBBBD", "..DSsD..GWWWWWWWDBDG",
            "...DD...GWWWWWWWWDGY", "........GWWWWWWWWLGG", ".......DGWWWWWWWLWWW", ".......DGWWWWWWLWWWW",
            "......DLGWWWWWLWWWWW", "......DGGYGGGYGGGYGG", "......DDDDDDDDDDDDDD", "...................."
        };

        public static Sprite Spellblade => spellblade != null ? spellblade : spellblade = BossArt.Build("Spellblade", SpellbladeGrid, false, SpellbladeColor);
        public static Sprite SpellbladeDetails => spellbladeDetails != null ? spellbladeDetails
            : spellbladeDetails = BossArt.Build("Spellblade details", SpellbladeGrid, true, SpellbladeColor);
        public static Sprite Archmage => archmage != null ? archmage : archmage = BossArt.Build("Rime Archmage", ArchmageGrid, false, ArchmageColor);
        public static Sprite ArchmageDetails => archmageDetails != null ? archmageDetails
            : archmageDetails = BossArt.Build("Rime Archmage details", ArchmageGrid, true, ArchmageColor);
        public static Sprite Magister => magister != null ? magister : magister = BossArt.Build("Grand Magister", MagisterGrid, false, MagisterColor);
        public static Sprite MagisterDetails => magisterDetails != null ? magisterDetails
            : magisterDetails = BossArt.Build("Grand Magister details", MagisterGrid, true, MagisterColor);

        // A steel, a steel shade, C rune glow, G gold, K hood shadow, S skin, s skin shade, E eyes.
        private static Color SpellbladeColor(char c)
        {
            switch (c)
            {
                case 'A': return new Color(0.82f, 0.86f, 0.94f);
                case 'a': return new Color(0.45f, 0.5f, 0.62f);
                case 'C': return new Color(0.45f, 0.85f, 1f);
                case 'G': return new Color(0.95f, 0.78f, 0.35f);
                case 'K': return new Color(0.05f, 0.05f, 0.12f);
                case 'S': return new Color(0.93f, 0.8f, 0.7f);
                case 's': return new Color(0.7f, 0.55f, 0.5f);
                case 'E': return new Color(0.5f, 0.95f, 1f);
                default: return Color.clear;
            }
        }

        // I ice, i ice shade, Y frost gem, H hair, h hair shade, S skin, s skin shade, E eyes, R lips, F/f hands.
        private static Color ArchmageColor(char c)
        {
            switch (c)
            {
                case 'I': return new Color(0.85f, 0.97f, 1f);
                case 'i': return new Color(0.45f, 0.75f, 1f);
                case 'Y': return new Color(0.55f, 0.95f, 1f);
                case 'H': return new Color(0.96f, 0.98f, 1f);
                case 'h': return new Color(0.55f, 0.68f, 0.9f);
                case 'S': return new Color(0.88f, 0.84f, 0.96f);
                case 's': return new Color(0.66f, 0.62f, 0.82f);
                case 'E': return new Color(0.3f, 0.9f, 1f);
                case 'R': return new Color(0.45f, 0.55f, 0.85f);
                case 'F': return new Color(0.82f, 0.9f, 1f);
                case 'f': return new Color(0.6f, 0.7f, 0.88f);
                default: return Color.clear;
            }
        }

        // G gold, Y gems and stars, K brim shadow, S skin, s skin shade, E eyes, B beard.
        private static Color MagisterColor(char c)
        {
            switch (c)
            {
                case 'G': return new Color(1f, 0.8f, 0.35f);
                case 'Y': return new Color(1f, 0.95f, 0.6f);
                case 'K': return new Color(0.04f, 0.02f, 0.08f);
                case 'S': return new Color(0.92f, 0.8f, 0.7f);
                case 's': return new Color(0.7f, 0.55f, 0.5f);
                case 'E': return new Color(0.8f, 0.6f, 1f);
                case 'B': return new Color(0.93f, 0.93f, 0.97f);
                default: return Color.clear;
            }
        }
    }
}
