using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// 16-pixel-tall chibi pixel-art sprites for every hero (the Specimen has one per form).
    /// Each hero has two layers built from one grid: a body layer drawn in greys so the SpriteRenderer's colour
    /// tints it with the class colour (keeping the dodge/hit flashes), and an accent layer with fixed colours for
    /// skin, steel, gold, wood, fur, gloves and horns. Sprites face right; flip them to face left.
    /// </summary>
    public static class HeroSprites
    {
        // Body layer (tinted): L highlight, W base, M shade, D outline.
        // Accent layer (fixed): K steel, Q steel highlight, k dark steel, G gold, g dark gold, B wood, b dark wood,
        // S skin, s skin shade, E eyes, H hair, I glow, F white cloth, f bowstring,
        // A floppy-ear / tail fur, R boxing glove, r glove shade, P pink tongue.
        // N demon horn / tail black, n horn sheen, V dark violet hair, U hair highlight, v glowing violet eyes,
        // T glowing tail tip, C pale skin, c pale skin shade. Wings: X leather, x crease, Z bone, Y outline, y claw,
        // w glowing violet edge.
        // i blue iris / visor glint, l green iris, h brown hair highlight, a blond hair, e blond hair shade, o auburn hair,
        // p blush, m mouth, t fur highlight, u dark fur (the inside of the Brawler's ears).
        // The Gambler reuses G / g for his gold tooth, coins and purse.
        // J glowing plasma, j deep plasma, O gunmetal (the Augment's implant eye, power core and arm cannon).
        // The Specimen: 1 amber vein glow, 2 stone skin, 3 stone shade, 4 dark messy hair, 5 bandage, 8 amber eye core, 9 stone highlight.
        private static readonly Dictionary<WeaponType, string[]> Grids = new Dictionary<WeaponType, string[]>
        {
            { WeaponType.Sword, new[] // Knight: a plumed great helm with eyes glinting through the visor, a gold-bossed shield and a raised sword
            {
                "..........DLD.......", ".........DLWD.......", ".....DDDDLWMDDD.....", "....DQQKKDDDKkkD....",
                "...DQKKKKKKKKKkkD...", "...DQKKKKKKKKKKkD..D", "...DKDDDDDDDDDDkD.DQ", "...DKDiIDDDDiIDkDDQD",
                "...DKKkKKkKKkKkkDQKD", "....DkKKKKKKKKkDDQKD", ".DDDDDDkkkkkkDDDgGgD", "DLWWDLWWWWWWWMDDSbD.",
                "DWGWDWWMgGgWWMDWsD..", "DWWMDDDDgGgDDDD.D...", ".DMD.DkKKDDKKkD.....", "..D..DDDD.DDDDD.....",
            } },
            { WeaponType.Bow, new[] // Archer: a leaf-green hood over auburn hair, green eyes, a quiver on the back and a tall strung longbow
            {
                "..................B.", "......DDDDDD.....Bb.", ".....DLLWWWMD...fDB.", "....DLWWWWWWMD..f.Bb",
                "...DLWWWWWWWWMD.f.Bb", "...DWWoooooooWMDf..B", "...DWoohSSooohWDf..B", "..DWWoSSSSSSSoWDf..B",
                "..DWoSSElSSElSsDf..B", "..DMoSSSSSSSSpsDf..B", "..DMDoSSSmSSsoD.f.Bb", ".BbDDDsSSSSsDDDDf.Bb",
                ".bBDLWWGgGWWMDSsfBb.", ".bBDWWWWBWWMMD..fD..", "..DDMWWDDWWMDD......", "...DDbbD.DbbDD......",
            } },
            { WeaponType.Staff, new[] // Wizard: a broad pointed hat with a gold star, green eyes over a long white beard, and a staff topped with a glowing crystal
            {
                "........D...........", ".......DLD..........", "......DLWMD.........", "......DLWMD.....I...",
                ".....DLWWWMD...IiI..", ".....DLWGWMMD..DID..", "....DLWWWWWWMD..B...", "DDDDDLWWWWWWMMDDDDB.",
                ".DLWWWWWWWWWWWMMMDB.", "..DDDHHHHHHHHDDDDB..", "...DHSSElSSElSHD.B..", "...DHSSSSSSSSSHDSs..",
                "..DLFFFSSmSSFFFDsB..", "..DLWFFFFFFFFFWMDB..", "..DLWWWfFFFfWWMMDB..", "...DDDDDDDDDDDDD.b..",
            } },
            { WeaponType.Daggers, new[] // Assassin: a deep hood over violet hair, a shadowed face with glowing violet eyes, a gold-buckled sash and a dagger in each hand
            {
                "........DDDD........", "......DDLWWMDD......", ".....DLWWWWWWMD.....", "....DLWWWWWWWWMD....",
                "....DLWDDDDDDWMD....", "...DLWDVUUVVVDWMD...", "...DWDVSSSSSSVDWD...", "...DWDSSEvSEvSDWD.Q.",
                ".Q.DWDSSSSSSSsDWDQ..", "..QDWDDMMMMMMDDWDK..", "..KDLWMMWWWMMWWDgD..", "...gDLWWMWWMWWMDs...",
                "...sDDDDgGgDDDDD....", "....DMWWWDWWWMD.....", "....DkkkDDkkkkD.....", "...DDDDD..DDDDD.....",
            } },
            { WeaponType.Hammer, new[] // Paladin: a gold-crested helm over blond hair, blue eyes, a white tabard with a gold sun and a steel warhammer
            {
                "........GGG.........", ".......DGYGD...DDDDD", "......DQKGKkD..DQGkD", ".....DQKKGKKkD.DKGkD",
                "....DQKKKGKKKkDDKGkD", "....DKaaaaaaaakDDDDD", "....DaSSSSSSSSaD.B..", "....DSSEiSSEiSsD.B..",
                "....DSSSSSSSSpsD.B..", "....DaSSSmmSSsaD.B..", "...DGDaSSSSSSaDGDB..", "..DGGDDFWWWWFDDGgBs.",
                "..DLWFDFWGGWFMDGDs..", "..DFGFDWWgGWWMDD....", "...DFDDLWWWWWMD.....", "....DDDKKDDKKDD.....",
            } },
            { WeaponType.Fists, new[] // Brawler: a puppygirl pit-fighter with auburn hair, big droopy dog ears, green eyes, a button nose and a :3 mouth with her tongue out, a tagged collar, a wagging tail and big gloves
            {
                "....................", "......DDDDDDDD......", "..DDDoohhooooooDDD..", ".DAAtDohooooooDtAAD.",
                "DAtAADooooooooDAAtAD", "DAAAuDoSSSSSSoDuAAAD", "DAAAuDSSElSSElDuAAAD", "DAAAuDSpSSSESpDuAAAD",
                ".DAAuDsSSSmSmSDuAAD.", ".DD.DDDsSSSPsDDD.DD.", "DtD..DDrrrGrrDD.DRRr", "DAtDDLWWWWWFFDDRRRRr",
                "DAAADWWWWWWMMDRRRRRr", ".DDDDDDgGgDDD.DrrrD.", "....DSSDDSsD........", "...DDkkD.DkkDD......",
            } },
            { WeaponType.Tail, new[] // Demoness: a pale girl with curled black ram horns, long dark violet hair, glowing eyes, a spade-tipped tail and bat wings drawn like Wing Dash's (clawed wrist, finger bones, scalloped violet-lit edge)
            {
                ".y..........................y.", ".yY......NNNn....nNNN......Yy.", ".YZY....NnnNNDDDDNNnnN....YZY.", ".YZZYY.Nn.DVUUUVVVVD.nN.YYZZY.",
                "YZxXZZYNnNVUVVVVVVVVNnNYZZXxZY", "YwxXXZZYNNVVVVVCCVVVNNYZZXXxwY", "YXxZXxZZYDVVVCCCCCCVDYZZxXZxXY", "ZwwxXXxxZDVVCCEvCCEvDZxxXXxwwZ",
                "YYwxXXXxxDVVCCCCCCCcDxxXXXxwYY", ".YwwZwwwDVVVcCCCPCcDxxwwwZwwY.", ".YwYwwYTDVVVVDcCCcD.wwwYwwYwY.", ".YwYYwYTTDVVDLWWWWWDwYYYwYYwY.",
                "..Y.YZY.TNDVDLWFFWMCD..YZY.Y..", ".....Y...NDDWWWMWWMcD...Y.....", ".........NDWWWWWWWWWD.........", "..........NDDCcDDCcD..........",
            } },
            { WeaponType.Coins, new[] // Gambler: a travelling merchant in a wide-brimmed banded hat, a gold-toothed grin, a fat coin purse and a flipped coin
            {
                "....................", "......DDDDDDDD......", ".....DLWWWWWWMD.....", ".....DLWWWWWWMD.....",
                ".....DbbbbGbbbD.....", "..DDDDDDDDDDDDDDD...", "...DDLWWWWWWWMDD....", "....DHSSSSSSSSHD....",
                "....DSSElSSElSsD....", "....DSSSSSSSSSsD....", "....DsSSmGmmSssD..G.", "...DDDsSSSSSssDD.GgG",
                "..DLWWFFFFFFWWMD.G..", ".DLWWWFFGFFWWMMDSs..", ".DSDbBGBbBbBWMDDD...", "...DkkDDDDkkDD......",
            } },
            { WeaponType.Beam, new[] // Augment: a soldier with brown hair and a steel skull plate with a glowing implant eye, plated armour, a plasma core and an arm cannon
            {
                "....................", "....Q...............", "....kDDDDDDDD.......", "...DKHHhHHHHHD......",
                "..DQKKHhHHHHHHD.....", "..DKKKHHHHHHHHHD....", "..DKKHSSSSSSSSHD....", "..DKJjSSSElSSSsD....",
                "..DkKKSSSSSSSSsD....", "..DkKKSSSmSSSssD....", "...DDkKsSSSSssD.....", "...DDLWDOkDWMDDkKKQD",
                "..DLWWWjJjWWMkKKKKJj", ".DLWWWWWjWWWMkOOkkDD", ".DMDDOOOOOODDD......", "...DMWDDDWWMD.......",
            } },
            { WeaponType.Scythe, new[] // Reaper: a bare skull in a deep hood, a great cloak that hides the body and trails off in tatters where legs would be, and a tall scythe
            {
                "...........kKKKKQQ..", "......DDDD.kKk..KQQ.", ".....DLWWMDB.....KQ.", "....DLWWWWMDB.....Q.",
                "...DLWDDDDWMDB......", "...DWDFFFFDWDB......", "...DWDNFFNDWMDB.....", "...DWDFFFFDWMDB.....",
                "...DWMDFNFDWMDB.....", "..DLWWMDDDWWMDB.....", "..DLWWWWWWWWMFFB....", ".DLWWWWWWWWWMDDB....",
                ".DLWWWWWWWWMMD.DB...", ".DWWMWWWMWWWMD..B...", "..DWDDWMDDWMDD..b...", "..D...DD...D........",
            } },
            { WeaponType.Mutation, new[] // Specimen (frail): a scrawny escaped lab subject with messy dark hair, plasters on his cheeks, a hospital gown and bandaged wrists
            {
                "........D.DD.D......", "......DD4D44D4DD....", ".....D44444444444D..", "....D4444444444444D.",
                "....D44SSSSSSSS44D..", "....D4SSSSSSSSSSsD..", "....DSSEESSSSEESsD..", "....DSSEiSSSSEiSsD..",
                "....DSs5sSSSs5ssSD..", ".....DSSSSssSSSsD...", "......DDDsSSsDDD....", ".....DLWWWWWWWMD....",
                "....DFDLWWWWWWMDFD..", "....D5DWWMWWWMMD5D..", "....DSDLWWWWWWMDSD..", ".....DDDsDDDsDDD....",
            } },
        };
        // Drawn about 1.45 units tall before the hero's 0.65 scale, matching the old hero footprint.
        private const float WorldSize = 1.45f;
        private static readonly Dictionary<WeaponType, Sprite> bodies = new Dictionary<WeaponType, Sprite>();
        private static readonly Dictionary<WeaponType, Sprite> accents = new Dictionary<WeaponType, Sprite>();

        public static bool Has(WeaponType weapon) => Grids.ContainsKey(weapon);

        /// <summary>The tintable body layer, or null for classes without a regular hero sprite.</summary>
        public static Sprite Body(WeaponType weapon) => Get(weapon, bodies, false);

        /// <summary>The fixed-colour detail layer, or null for classes without a regular hero sprite.</summary>
        public static Sprite Accent(WeaponType weapon) => Get(weapon, accents, true);

        // The Specimen's grown forms. The Behemoth is a hulking slab of stone-grey muscle with glowing amber veins and
        // eyes, his gown torn to a rag round his middle (drawn wider than the others, then scaled up by his form).
        // The Edge is still the same size as his frail self, now with a headband and a chain whip coiled in his hand.
        private static readonly string[] BehemothGrid =
        {
            "..........DDDD..........", ".........D4444D.........", "........D433334D........", "........D282282D........",
            "....DDDDD322223DDDDD....", "...D99222D2222D22299D...", "..D9222221D99D1222229D..", ".D22221222922922212222D.",
            "D222D2229D2222D9222D222D", "D212D22DLWWWWWWLD22D212D", "D222D2DLWWMWWMWWLD2D222D", "D2223DDDLWWWWWWLDDD3222D",
            "D29992D.D22DD22D.D29992D", "D22222D.D23DD32D.D22222D", ".DDDDD..D33DD33D..DDDDD.", "........DDDDDDDD........",
        };
        private static readonly string[] EdgeGrid =
        {
            "....................", "......DD4D4DDD..QQ..", ".....D44444444D...Q.", "....D4444444444D..K.",
            ".LWDDLWWWWWWWWMD..k.", "LW..DSEESSSEESsD.K..", ".W..DSSEESSSEEsD.k..", "....DSSElSSSElsDK...",
            "....DSSSSSSSSssDk...", ".....DSSSssSSsDK....", "......DDsSSsDDSk....", ".....DLWWWWWWMDS....",
            "....DFDLWWWWWMDD....", "....D5DWW5WWMMD.....", ".....DLWWDWWMD......", ".....DDsD.DsDD......",
        };
        private static readonly Sprite[] specimenBodies = new Sprite[3], specimenAccents = new Sprite[3];

        /// <summary>The Specimen's body layer for a form (frail, Behemoth or Edge).</summary>
        public static Sprite SpecimenBody(SpecimenForm form) => SpecimenSprite(form, false);

        /// <summary>The Specimen's detail layer for a form.</summary>
        public static Sprite SpecimenAccent(SpecimenForm form) => SpecimenSprite(form, true);

        private static Sprite SpecimenSprite(SpecimenForm form, bool accent)
        {
            if (form == SpecimenForm.Frail) return accent ? Accent(WeaponType.Mutation) : Body(WeaponType.Mutation);
            int index = (int)form;
            var cache = accent ? specimenAccents : specimenBodies;
            if (cache[index] != null) return cache[index];
            var rows = form == SpecimenForm.Behemoth ? BehemothGrid : EdgeGrid;
            return cache[index] = Build("Specimen " + form + (accent ? " details" : " body"), rows, accent, (x, c) => true, Vector2.one * 0.5f);
        }

        private static Sprite Get(WeaponType weapon, Dictionary<WeaponType, Sprite> cache, bool accent)
        {
            if (!Grids.TryGetValue(weapon, out var rows)) return null;
            if (cache.TryGetValue(weapon, out var sprite) && sprite != null) return sprite;
            sprite = Build(weapon + (accent ? " hero details" : " hero body"), rows, accent, (x, c) => true, Vector2.one * 0.5f);
            cache[weapon] = sprite;
            return sprite;
        }

        // The Demoness's wing pixels, and the pixel on each side of her back where a wing joins it (the flap's pivot).
        private const string WingPixels = "XxZYyw";
        private const float WingShoulderX = 9f, WingShoulderY = 8f;
        private static Sprite winglessAccent;
        private static readonly Sprite[] wings = new Sprite[2];

        /// <summary>True for heroes whose wings can beat on their own (the Demoness).</summary>
        public static bool HasWings(WeaponType weapon) => weapon == WeaponType.Tail;

        /// <summary>The Demoness's detail layer with her wings left out, worn while the separate wing layers beat.</summary>
        public static Sprite WinglessAccent => winglessAccent != null ? winglessAccent
            : winglessAccent = Build("Tail hero details (wingless)", Grids[WeaponType.Tail], true, (x, c) => WingPixels.IndexOf(c) < 0, Vector2.one * 0.5f);

        // The Reaper's scythe pixels: steel blade and wooden shaft.
        private const string ScythePixels = "KQkBb";
        private static Sprite scythelessAccent;

        /// <summary>The Reaper's detail layer with his scythe left out, worn while the scythe itself swings through a cut.</summary>
        public static Sprite ScythelessAccent => scythelessAccent != null ? scythelessAccent
            : scythelessAccent = Build("Scythe hero details (empty-handed)", Grids[WeaponType.Scythe], true, (x, c) => ScythePixels.IndexOf(c) < 0, Vector2.one * 0.5f);

        /// <summary>
        /// One of the Demoness's wings (side -1 left, +1 right) exactly as drawn on her sprite, pivoted at the shoulder
        /// so it can be rotated to beat. Place it at <see cref="WingShoulder"/> on an unflipped hero.
        /// </summary>
        public static Sprite Wing(int side)
        {
            int index = side < 0 ? 0 : 1;
            if (wings[index] != null) return wings[index];
            var rows = Grids[WeaponType.Tail];
            float half = rows[0].Length * 0.5f, shoulderX = side < 0 ? WingShoulderX : rows[0].Length - WingShoulderX;
            return wings[index] = Build(side < 0 ? "Demoness left wing" : "Demoness right wing", rows, true,
                (x, c) => WingPixels.IndexOf(c) >= 0 && (side < 0 ? x < half : x >= half),
                new Vector2(shoulderX / rows[0].Length, WingShoulderY / rows.Length));
        }

        /// <summary>Where a wing's shoulder sits relative to the centre of the unflipped hero sprite, in sprite units.</summary>
        public static Vector2 WingShoulder(int side)
        {
            var rows = Grids[WeaponType.Tail];
            float unit = WorldSize / rows.Length;
            return new Vector2(side * (rows[0].Length * 0.5f - WingShoulderX), WingShoulderY - rows.Length * 0.5f) * unit;
        }

        /// <param name="include">Which accent pixels (by column and grid letter) to keep.</param>
        private static Sprite Build(string name, string[] rows, bool accent, System.Func<int, char, bool> include, Vector2 pivot)
        {
            int width = rows[0].Length, height = rows.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    char c = x < rows[y].Length ? rows[y][x] : '.';
                    Color? body = BodyColor(c);
                    pixels[(height - y - 1) * width + x] = accent
                        ? (body.HasValue || !include(x, c) ? Color.clear : AccentColor(c))
                        : body ?? Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            // Scaled by height, so a wider grid (the Demoness's wings) spreads sideways instead of shrinking the hero.
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, height / WorldSize);
        }

        private static Color? BodyColor(char c)
        {
            switch (c)
            {
                case 'L': return new Color(1f, 1f, 1f);
                case 'W': return new Color(0.84f, 0.84f, 0.86f);
                case 'M': return new Color(0.58f, 0.58f, 0.64f);
                case 'D': return new Color(0.1f, 0.11f, 0.16f);
                default: return null;
            }
        }

        private static Color AccentColor(char c)
        {
            switch (c)
            {
                case 'K': return new Color(0.78f, 0.84f, 0.92f);
                case 'Q': return new Color(0.95f, 0.97f, 1f);
                case 'k': return new Color(0.45f, 0.52f, 0.63f);
                case 'G': return new Color(1f, 0.8f, 0.38f);
                case 'g': return new Color(0.78f, 0.52f, 0.2f);
                case 'B': return new Color(0.56f, 0.37f, 0.2f);
                case 'b': return new Color(0.34f, 0.22f, 0.12f);
                case 'S': return new Color(0.98f, 0.8f, 0.64f);
                case 's': return new Color(0.84f, 0.62f, 0.48f);
                case 'E': return new Color(0.07f, 0.08f, 0.12f);
                case 'H': return new Color(0.36f, 0.24f, 0.15f);
                case 'I': return new Color(0.55f, 0.95f, 1f);
                case 'F': return new Color(0.95f, 0.95f, 0.9f);
                case 'f': return new Color(0.72f, 0.72f, 0.66f);
                case 'A': return new Color(0.58f, 0.38f, 0.22f);
                case 'R': return new Color(0.93f, 0.2f, 0.2f);
                case 'r': return new Color(0.58f, 0.09f, 0.12f);
                case 'P': return new Color(1f, 0.52f, 0.62f);
                case 'N': return new Color(0.08f, 0.05f, 0.12f);
                case 'n': return new Color(0.42f, 0.38f, 0.5f);
                case 'V': return new Color(0.24f, 0.06f, 0.4f);
                case 'U': return new Color(0.4f, 0.14f, 0.58f);
                case 'v': return new Color(0.85f, 0.5f, 1f);
                case 'T': return new Color(0.56f, 0.24f, 0.86f);
                case 'C': return new Color(1f, 0.94f, 0.93f);
                case 'c': return new Color(0.86f, 0.76f, 0.8f);
                case 'J': return new Color(0.45f, 1f, 0.8f);
                case 'j': return new Color(0.12f, 0.55f, 0.45f);
                case 'O': return new Color(0.24f, 0.27f, 0.33f);
                case 'i': return new Color(0.3f, 0.6f, 1f);
                case 'l': return new Color(0.3f, 0.75f, 0.4f);
                case 'h': return new Color(0.58f, 0.4f, 0.24f);
                case 'a': return new Color(0.98f, 0.84f, 0.46f);
                case 'e': return new Color(0.76f, 0.55f, 0.24f);
                case 'o': return new Color(0.62f, 0.26f, 0.16f);
                case 'p': return new Color(1f, 0.62f, 0.62f);
                case 'm': return new Color(0.55f, 0.16f, 0.2f);
                case 'u': return new Color(0.36f, 0.2f, 0.11f);
                case 't': return new Color(0.76f, 0.54f, 0.34f);
                // The Demoness's bat wings: dark oxblood leather with lighter creases, veins and near-black bone.
                case 'X': return new Color(0.13f, 0.05f, 0.07f);
                case 'x': return new Color(0.23f, 0.1f, 0.11f);
                case 'Z': return new Color(0.07f, 0.025f, 0.04f);
                case 'Y': return new Color(0.04f, 0.02f, 0.03f);
                case 'y': return new Color(0.6f, 0.54f, 0.48f);
                // The glowing violet trailing edge along the wings' scallops, as in Wing Dash.
                case 'w': return new Color(0.66f, 0.3f, 1f);
                case '1': return new Color(1f, 0.62f, 0.2f);
                case '2': return new Color(0.6f, 0.59f, 0.57f);
                case '3': return new Color(0.4f, 0.39f, 0.39f);
                case '4': return new Color(0.17f, 0.15f, 0.18f);
                case '5': return new Color(0.72f, 0.7f, 0.64f);
                case '8': return new Color(1f, 0.93f, 0.62f);
                case '9': return new Color(0.78f, 0.76f, 0.72f);
                default: return Color.clear;
            }
        }
    }
}
