using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// 40x40 two-layer art for the Ash Below's and the Neon Arcology's guardians, drawn as left halves and mirrored by
    /// <see cref="BossArt"/>: a shaded body tinted by the boss, and a fixed-colour detail layer on top.
    /// </summary>
    public static class GuardianSprites
    {
        private static Sprite warden, wardenDetails, duelist, duelistDetails, archdemon, archdemonDetails, overseer, overseerDetails, bastion, bastionDetails, core, coreDetails;

        // The Rime Warden: a crowned hood around a gold mask with frost-lit eyes, a fur-trimmed robe, icicles on its sleeves and hem, and a frost crystal cradled at its chest.
        private static readonly string[] WardenGrid =
        {
            "...................G", "..............G....G", "..............GG..GG", "...........G..GGGGGG",
            "...........GGGGgGgGG", "...........gGgGGGYGG", "..........DDDDDDDDDD", ".........DLWWWWWWWWW",
            "........DLWWWWWWWWWW", ".......DLWWWDDDDDDDD", ".......DWWWDKKKKKKKK", "......DLWWDKKKgGGGGG",
            "......DWWDKKgGGGGGGG", "......DWWDKKgKEEKGGG", "......DWMDKKgGKKgGGG", "......DWMDKKKgGGGGGG",
            "......DWMDKKKgGGgggg", ".....DLWMDKKKKgGGGGG", ".....DWWMMDKKKKggggg", "....DLWWWWMDDDDDDDDD",
            "....DFFFWWWMFFFFFFFF", "...DFfFFFWWWWWWWWWWW", "..DLWFfFWWWWWWWWDDDD", "..DWWWWWWWWWWWDDDIOO",
            "..DWWMWWWWWWWDDIOOYY", ".DLWWMDWWWWWDLDIOYYY", ".DWWMD.DWWWWDLDIOOYY", ".DWMD..DWWWWWDDDIOOO",
            "DFFFD..DWWMWWWWDDDDD", "DIfID..DWWMWWWWWWWLW", ".I.I..DLWWMWWWWWWWLW", "......DWWWMWWWWWWLWW",
            ".....DLWWMWWWWWWWLWW", ".....DWWWMWWWWWWLWWW", "....DLWWMWWWWWWWLWWW", "....DWWWMWWWWWWLWWWW",
            "...DLWWMWWWWWWWLWWWW", "...DFFFFFFFFFFFFFFFF", "...DIfIfIDDDDDDIfIfI", "....I.I.........I.I."
        };
        public static Sprite Warden => warden != null ? warden : warden = BossArt.Build("Rime Warden", WardenGrid, false, WardenColor);
        public static Sprite WardenDetails => wardenDetails != null ? wardenDetails : wardenDetails = BossArt.Build("Rime Warden details", WardenGrid, true, WardenColor);

        // G gold, g dark gold, E eyes, K hood void, O crystal, Y crystal core, F frost fur, f fur shade, I icicles.
        private static Color WardenColor(char c)
        {
            switch (c)
            {
                case 'G': return new Color(0.98f, 0.78f, 0.32f);
                case 'g': return new Color(0.62f, 0.44f, 0.16f);
                case 'E': return new Color(0.85f, 0.97f, 1f);
                case 'K': return new Color(0.02f, 0.03f, 0.08f);
                case 'O': return new Color(0.4f, 0.75f, 1f);
                case 'Y': return new Color(0.88f, 0.98f, 1f);
                case 'F': return new Color(0.95f, 0.97f, 1f);
                case 'f': return new Color(0.7f, 0.8f, 0.9f);
                case 'I': return new Color(0.6f, 0.88f, 1f);
                default: return Color.clear;
            }
        }

        // The Steel Duelist: a crested helm with a glowing visor, a red scarf and long red coat, a belt of throwing knives, and a sabre in each hand.
        private static readonly string[] DuelistGrid =
        {
            "...................R", "..................RR", "..................Rr", "..................RR",
            ".................RRr", "...............DDDRD", "..............DLWWDW", ".............DLWWWWW",
            ".............DWWMMMM", ".............DWDDDDD", ".............DWDEEEE", ".............DWWDDDD",
            ".............DMWWWWW", "..............DMWWMW", "..............RDDDDD", "...........DDRRRrRRR",
            "..........DLWDRRRRrR", ".........DLWWWDDRRDD", ".........DWWMWWDDLWW", "........DLWMDDWWWLWW",
            "........DWWMD.DWWWLW", ".......DLWMD..DWWWLW", ".......DWWMD..DWWWMW", ".......DWMD..DKkKkKk",
            "......DSSD...DRRRWRR", "......DSsb..DRRrRWRr", ".......DDB..DRRrRWRr", "........B...DRrRRWrR",
            "........B..DRRrRRWRr", ".......B...DRrRRDWRr", ".......B...DRrRDDWRR", "......B.....DDDDWMDD",
            "......B.......DWWMD.", ".....B........DWWMD.", ".....B........DWWMD.", "....B.........DWWMD.",
            "....B........DLWWMD.", "...B.........DLLLLD.", "...B.........DDDDDD.", "...................."
        };
        public static Sprite Duelist => duelist != null ? duelist : duelist = BossArt.Build("Steel Duelist", DuelistGrid, false, DuelistColor);
        public static Sprite DuelistDetails => duelistDetails != null ? duelistDetails : duelistDetails = BossArt.Build("Steel Duelist details", DuelistGrid, true, DuelistColor);

        // R red cloth, r red shade, E visor, K/k knife belt, B blade, b hilt, S gauntlet, s gauntlet shade.
        private static Color DuelistColor(char c)
        {
            switch (c)
            {
                case 'R': return new Color(0.85f, 0.2f, 0.25f);
                case 'r': return new Color(0.5f, 0.08f, 0.12f);
                case 'E': return new Color(0.45f, 0.95f, 1f);
                case 'K': return new Color(0.35f, 0.4f, 0.48f);
                case 'k': return new Color(0.75f, 0.8f, 0.88f);
                case 'B': return new Color(0.8f, 0.97f, 1f);
                case 'b': return new Color(0.5f, 0.35f, 0.2f);
                case 'S': return new Color(0.35f, 0.4f, 0.5f);
                case 's': return new Color(0.2f, 0.24f, 0.3f);
                default: return Color.clear;
            }
        }

        // Malphas: great ridged horns, burning eyes, a fanged maw, spread bat wings, a magma-cracked chest, claws and cloven hooves.
        private static readonly string[] ArchdemonGrid =
        {
            "..HH................", ".hHHH...............", ".hhHHH..............", "..hhHHH.............",
            "...hhHHH............", "....hhHHHD..........", ".....hhHHHDDDDDDDDDD", "......hhHHDLWWWWWWWW",
            ".D.......DLWWWWWWWWW", ".DD......DMMWWWWWWWW", ".DWD.....DWMDDMMWWWW", ".DWWD....DWDEEDMWWWW",
            ".DWLWD...DWDEYEDWWWW", ".DWWLWD...DWDDDMWWWW", ".DWMWLWD..DWWWWWWWWW", ".DWMWWLWD.DMKKKKKKKK",
            ".DWMWWWLWDDMKFKFKFKF", ".DWWMWWWLWDMKOOOOOOO", ".DWWMWWWWLWDKFKFKFKF", ".DWWWMWWWWLDDKKKKKKK",
            ".DWWWMWWWWWLDDDDDDDD", ".DWWWWMWWDDLWWWWWWWW", ".DWWWWMWDLWWWWMWWWWW", ".DWMWWWDLWWWWMOWWWWW",
            "..DMWWMDWWWWMOOOWWOW", "..DD.DMDWWWMWOYOWOOW", ".D.D..DDWWWMWWOOWOYO", "........DWWWMWWWWOYY",
            "........DWWWWMWWWWOO", "........DWWWWWMMWWWW", "........DFDWWWWWMMMM", ".......FDFDWWWMWWWWW",
            "........F.DWWWMDDDWW", "..........DWWMMD.DMW", "..........DWWMD...DW", ".........DLWWMD...DW",
            ".........DhhhhD...Dh", "........DhHHHhD..DhH", "........DDDDDD...DDD", "...................."
        };
        public static Sprite Archdemon => archdemon != null ? archdemon : archdemon = BossArt.Build("Hellfire Archdemon", ArchdemonGrid, false, ArchdemonColor);
        public static Sprite ArchdemonDetails => archdemonDetails != null ? archdemonDetails : archdemonDetails = BossArt.Build("Hellfire Archdemon details", ArchdemonGrid, true, ArchdemonColor);

        // H horn, h horn shade, E eyes, Y eye core, K maw, F fangs and claws, O magma.
        private static Color ArchdemonColor(char c)
        {
            switch (c)
            {
                case 'H': return new Color(0.9f, 0.84f, 0.7f);
                case 'h': return new Color(0.5f, 0.4f, 0.32f);
                case 'E': return new Color(1f, 0.96f, 0.55f);
                case 'Y': return new Color(1f, 1f, 0.9f);
                case 'K': return new Color(0.05f, 0f, 0.02f);
                case 'F': return new Color(0.97f, 0.94f, 0.86f);
                case 'O': return new Color(1f, 0.45f, 0.08f);
                default: return Color.clear;
            }
        }

        // The Grid Overseer: an antenna crown with emitter nodes, a domed head with one great lens, pylon shoulders striped with scan slits, a circuit-traced torso, cable arms and a hovering thruster.
        private static readonly string[] OverseerGrid =
        {
            "........C.........C.", "........D.........D.", "........D.........D.", ".........D.......DD.",
            ".........DD....DDLW.", "..........DDDDDLWWWW", "...........DLWWWWWWW", "..........DLWWWWWWWW",
            "..........DWWWKKKKKK", ".........DLWWKKccccc", ".........DWWKKcCCCCC", ".........DWWKcCCEEEE",
            ".........DWWKcCEEEYY", ".........DWWKcCEEEYY", ".........DWWKKcCCCCC", "..........DWWKKccccc",
            "..........DMWWKKKKKK", "...DDDDD...DMMWWWWWW", "..DLWWWWD...DDDDDDDD", "..DWCWCWD..DLWWWWWWW",
            "..DWCWCWDDDLWWcWWWWW", "..DWCWCWDWWWWWWcccWW", "..DWCWCWDWWMWWWWWcCC", "..DMWMWMDWMDWWWWWcCC",
            "...DDDDDDWMDWWWccWWW", "......DWWMD.DWWcWWWW", ".....DWWMD..DWWcWWWW", ".....DWMD...DMWWWWWW",
            "....DWMD.....DMWWWWW", "...DCD.......DMMWWWW", "..DC.CD.......DMMMMM", "..C...C.......DDDDDD",
            "...............DLWWW", "..............DLWWWW", "..............DWWWMM", "..............DDDDDD",
            "...............CcCCC", "................cCcC", ".................CcC", "..................C."
        };
        public static Sprite Overseer => overseer != null ? overseer : overseer = BossArt.Build("Grid Overseer", OverseerGrid, false, OverseerColor);
        public static Sprite OverseerDetails => overseerDetails != null ? overseerDetails : overseerDetails = BossArt.Build("Grid Overseer details", OverseerGrid, true, OverseerColor);

        // C neon, c dark neon, K lens glass, E lens, Y lens glint.
        private static Color OverseerColor(char c)
        {
            switch (c)
            {
                case 'C': return new Color(0.25f, 0.95f, 1f);
                case 'c': return new Color(0.1f, 0.5f, 0.6f);
                case 'K': return new Color(0.02f, 0.05f, 0.08f);
                case 'E': return new Color(0.85f, 1f, 1f);
                case 'Y': return new Color(1f, 1f, 1f);
                default: return Color.clear;
            }
        }

        // Bastion: shoulder missile pods with amber warheads, an armoured head with an amber visor, hazard striping, cannon arms and tracked legs.
        private static readonly string[] BastionGrid =
        {
            "....................", "....................", "..DDDDDDD...........", ".DLWWWWWWD..........",
            ".DWADWADWD..........", ".DWAAWAAWD......DDDD", ".DWDDWDDWD....DDLWWW", ".DWAWWAWWD...DLWWWWW",
            ".DWAAWAAWD..DLWWWWWW", ".DMWWWWWMD..DWWMMMMM", ".DDDDDDDDDDDDWMKKKKK", "....DWWWWWWDWWMKAAAA",
            "...DLWWWWWWWDWMKAYYY", "...DWWMMMMWWDWMKKKKK", "...DWWMDDMWWWDWWWWWW", "..DLWWMD.DMWWWDDDDDD",
            "..DWWMD..DDWWWWWWWWW", "..DWWMD.DDDWYKYKYKYK", ".DLWMD..DkDWKYKYKYKY", ".DWWMD..DkDWYKYKYKYK",
            ".DWMD...DkDWWWWWWWWW", ".DDD....DkDWWWMWWWMW", ".DkD....DDDWWMWWWMWW", ".DkD......DWWWWWWWWW",
            ".DkD......DWWMAWWWAW", ".DDD......DWWWWWWWWW", "..........DDDDDDDDDD", ".........DLWWWWD..DL",
            "........DLWWWWWD..DW", "........DWWWMWWD..DW", "........DWWMWWWD..DW", ".......DKKKKKKKKD.DK",
            ".......DKkKkKkKkD.DK", ".......DkKkKkKkKD.Dk", ".......DKKKKKKKKD.DK", ".......DDDDDDDDDD.DD",
            "....................", "....................", "....................", "...................."
        };
        public static Sprite Bastion => bastion != null ? bastion : bastion = BossArt.Build("Siege Engine", BastionGrid, false, BastionColor);
        public static Sprite BastionDetails => bastionDetails != null ? bastionDetails : bastionDetails = BossArt.Build("Siege Engine details", BastionGrid, true, BastionColor);

        // A amber, Y amber glow and hazard stripe, K black, k tread.
        private static Color BastionColor(char c)
        {
            switch (c)
            {
                case 'A': return new Color(1f, 0.65f, 0.15f);
                case 'Y': return new Color(1f, 0.95f, 0.6f);
                case 'K': return new Color(0.06f, 0.06f, 0.08f);
                case 'k': return new Color(0.35f, 0.35f, 0.4f);
                default: return Color.clear;
            }
        }

        // Null: an armoured sphere around a black event horizon with a violet rim and a white-hot core, a tilted orbit ring with satellite pods, and antenna spines.
        private static readonly string[] CoreGrid =
        {
            "...................V", "...................D", "...................D", "...................D",
            "...................D", "...............DDDDD", ".............DDLLLLW", "...........DDLLLLLWW",
            "..........DLLLLLLWWW", ".........DDLLLLLWDDD", "........DLDLLLDDVVVV", ".......DLLLDLDVVvvvv",
            ".......DLLLLDVVvvKKK", "......DLLLLDVvvKKKKK", "......DLLLDVVvKKKKKK", ".....DLLLLDVvKKKKKKK",
            ".....DLLLWVvvKKKKKEE", ".....DLLWDVvKKKKKEEY", ".....DLWWDVvKKKKEEYY", ".....DDDDDVvKKKKEYYY",
            "....rrWWWDVvKKKKEYYY", "..rr.DWWWDVvKKKKEEYY", "RDVD.DWWWDVvKKKKKEEY", "RVVV.DWWWWVvvKKKKKEE",
            "RDVDrrWWWWDVvKKKKKKK", "......DWWWDVVvKKKKKK", "......DWWWWDVvvKKKKK", ".......DWWWWDVVvvKKK",
            ".......DWWWDWDVVvvvv", "........DDDWWWDDVVVV", ".........DWWWWWWWDDD", "..........DWWWWWWMMD",
            "...........DDWWWMMMD", ".............DDMMMMD", "...............DDDDD", "...................D",
            "...................D", "...................D", "...................D", "...................V"
        };
        public static Sprite Core => core != null ? core : core = BossArt.Build("Singularity Core", CoreGrid, false, CoreColor);
        public static Sprite CoreDetails => coreDetails != null ? coreDetails : coreDetails = BossArt.Build("Singularity Core details", CoreGrid, true, CoreColor);

        // V violet, v violet shade, K event horizon, E core glow, Y core, R orbit ring, r ring shade.
        private static Color CoreColor(char c)
        {
            switch (c)
            {
                case 'R': return new Color(0.85f, 0.7f, 1f);
                case 'r': return new Color(0.4f, 0.3f, 0.55f);
                case 'V': return new Color(0.75f, 0.35f, 1f);
                case 'v': return new Color(0.45f, 0.15f, 0.65f);
                case 'K': return new Color(0.02f, 0f, 0.05f);
                case 'E': return new Color(0.9f, 0.75f, 1f);
                case 'Y': return new Color(1f, 1f, 1f);
                default: return Color.clear;
            }
        }
    }
}
