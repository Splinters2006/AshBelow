using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// 16x16 pixel-art sprites for the regular heroes (every class except Admin, which keeps its own sprite).
    /// Each hero has two layers built from one grid: a body layer drawn in greys so the SpriteRenderer's colour
    /// tints it with the class colour (keeping the dodge/hit flashes), and an accent layer with fixed colours for
    /// skin, steel, gold and wood. Sprites face right; flip them to face left.
    /// </summary>
    public static class HeroSprites
    {
        // Body layer (tinted): L highlight, W base, M shade, D outline.
        // Accent layer (fixed): K steel, Q steel highlight, k dark steel, G gold, g dark gold, B wood, b dark wood,
        // S skin, s skin shade, E eyes, H hair, I glow, F white cloth, f bowstring.
        private static readonly Dictionary<WeaponType, string[]> Grids = new Dictionary<WeaponType, string[]>
        {
            { WeaponType.Sword, new[] // Knight
            {
                "......LW........", ".....LWWM.......", "....DDQKDD......", "...DQKKKKkD.....",
                "...DKKKKKKkD..Q.", "...DEEEEEEkD.QK.", "...DKKKKKKkDQKD.", "..DDDkkkkkDDKD..",
                ".DQKDLWWWMDgD...", "DLWWDWWMWWDGsD..", "DWMWDWMMMMDsD...", "DWWMDDgGgDD.....",
                ".DMDDkWWMkD.....", "..D.DkkDkkD.....", "....DKkDKkD.....", "...DDDD.DDDD....",
            } },
            { WeaponType.Bow, new[] // Archer
            {
                ".............bB.", ".....DDDD....fB.", "....DLWWMD...f.B", "...DLWWWWMD..f.B",
                "...DWHHHHMD..f.B", "...DHSSESsD..f.B", "....DSSSsD...f.B", "..FDDLWWMDD..f.B",
                "..FDLWWWMMWDSsfB", ".bBDWWgGgMDD.f.B", ".bBDWWWWWMD..f.B", ".bBDDBBGBBD..f.B",
                "..bDMWWWMMD..fB.", "....DMWDMMD..bB.", "....DBBDBbD.....", "...DDDD.DDDD....",
            } },
            { WeaponType.Staff, new[] // Wizard
            {
                ".......D........", "......DWD.......", ".....DLWMD......", "....DLWGWMD..I..",
                "...DLWWWWWMD.III", ".DDLWWWWWWMMDDI.", "DLWWWWWWWWWWMMDB", "..DHSSSSSSHD..B.",
                "..DHSESSESHD..B.", "..DHHSSSSHHD..B.", ".DLWHHHHHHWMDSB.", ".DLWWHHHHWWMDsB.",
                ".DLWWMWGWMWMD.B.", ".DWWMWWGWWMMD.B.", ".DWMWWWGWWWMD.B.", "..DDDDDDDDDD..b.",
            } },
            { WeaponType.Daggers, new[] // Assassin
            {
                ".......D........", "......DLD.......", ".....DLWWD......", "....DLWWWMD.....",
                "...DLWDDDMMD....", "...DWDIDIDMD....", ".Q.DWMDDDMMD..Q.", ".Q..DWWWWMD...Q.",
                ".K.DLWMMMWMD..K.", ".gDDWWWMWWMDDDg.", ".sDLWWWMWWMMDDs.", "..DDDDgGgDDDD...",
                "....DMWWWMD.....", "....DMWDWMD.....", "....DkkDkkD.....", "...DDDD.DDDD....",
            } },
            { WeaponType.Hammer, new[] // Paladin
            {
                ".......GG.......", "......DGgD......", ".....DQKGKD.....", "...DDQKKGKKDD...",
                "...DQKKKKKKkD...", "...DKEEKKEEkD.Q.", "...DKKKKKKKkDQK.", "..DGDkkkkkkDQKD.",
                ".DGGDLWFFWMDKD..", "DLFWDWFFFFMDgD..", "DFFFDWWFFWMGsD..", "DLFWDDgGGgDsD...",
                ".DMDDLWWWMMD....", "..D.DWWDWMMD....", "....DKkDKkD.....", "...DDDD.DDDDD...",
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

        private static Sprite Get(WeaponType weapon, Dictionary<WeaponType, Sprite> cache, bool accent)
        {
            if (!Grids.TryGetValue(weapon, out var rows)) return null;
            if (cache.TryGetValue(weapon, out var sprite) && sprite != null) return sprite;
            sprite = Build(weapon + (accent ? " hero details" : " hero body"), rows, accent);
            cache[weapon] = sprite;
            return sprite;
        }

        private static Sprite Build(string name, string[] rows, bool accent)
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
                        ? (body.HasValue ? Color.clear : AccentColor(c))
                        : body ?? Color.clear;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, width / WorldSize);
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
                default: return Color.clear;
            }
        }
    }
}
