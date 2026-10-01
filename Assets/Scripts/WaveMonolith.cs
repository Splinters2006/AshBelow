using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The standing stone on a wave level: nothing attacks until a hero touches it, which calls the first wave. While it
    /// waits its runes pulse in the world's colour, it pings the floor around it and an arrow bobs over it; all of that
    /// flares when a hero is close enough to touch it, and goes dark once the waves are under way.
    /// </summary>
    public sealed class WaveMonolith : MonoBehaviour
    {
        public const float Reach = 1.6f;
        private const float BaseY = -0.7f, PingInterval = 1.8f;

        // O outline, L/M/D lit, mid and shaded stone, R runes (drawn on the glow layer).
        private static readonly string[] StoneGrid =
        {
            "......OOOO......", ".....OLLMMO.....", "....OLLMMMDO....", "....OLMMMMDO....", "...OLLMMMMDDO...",
            "...OLMRRRRMDO...", "...OLMMMMMMDO...", "...OLMMRRMMDO...", "...OLMRMMRMDO...", "...OLMRMMRMDO...",
            "...OLMMRRMMDO...", "...OLMMMMMMDO...", "...OLMRMMRMDO...", "...OLMMRRMMDO...", "...OLMRMMRMDO...",
            "...OLMMMMMMDO...", "...OLMRRRRMDO...", "...OLMMRRMMDO...", "...OLMMRRMMDO...", "...OLMMMMMMDO...",
            "..OOLMMMMMMDOO..", ".OLLLLLLLLLLLDO.", ".OLMMMMMMMMMMDO.", "OLMMMMMMMMMMMMDO", "ODDDDDDDDDDDDDDO",
            "OOOOOOOOOOOOOOOO"
        };

        // The Arcane Spire's: a tapering crystal obelisk banded in gold on a rune-set dais, a star over its tip and two
        // motes at its sides. O outline, W glint, L/C/D crystal, G/g gold, S/s dais, R runes and F the star and motes (glow layer).
        private static readonly string[] SpireGrid =
        {
            ".........FF.........", "........FFFF........", ".........FF.........", "....................",
            ".........OO.........", "........OWLO........", "........OLCO........", ".......OWLCDO.......",
            ".......OLLCDO.......", ".......OLCCDO.......", "......OGGGGGGO......", "......OLLCCDDO......",
            "..F...OLCRRCDO...F..", ".FFF..OLCRRCDO..FFF.", "..F...OLCCCCDO...F..", "......OLRCCRDO......",
            "......OLCRRCDO......", "......OLCRRCDO......", ".....OLLRCCRDDO.....", ".....OLLCCCCDDO.....",
            ".....OLCCRRCCDO.....", ".....OLCRCCRCDO.....", ".....OLCRCCRCDO.....", ".....OLCCRRCCDO.....",
            ".....OLCCCCCCDO.....", "....OGGGGGGGGGGO....", "....OggggggggggO....", "...OOLLCCCCCCDDOO...",
            "..OSSSSSSSSSSSSSSO..", ".OSsRRsssRRsssRRsSO.", "OSssssssssssssssssSO", "OssssssssssssssssssO",
            ".OOOOOOOOOOOOOOOOOO."
        };

        private static readonly string[] ArrowGrid = { "WWWWWWW", ".WWWWW.", "..WWW..", "...W..." };

        private static Sprite stone, stoneRunes, spire, spireRunes, arrow, halo;

        private DungeonRun run;
        private SpriteRenderer runes, glow, aura, marker;
        private Color accent;
        private float top, nextPing;
        private bool spent;

        public static WaveMonolith Create(DungeonRun run, Transform parent, Vector2 position)
        {
            var root = new GameObject("Wave monolith");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var monolith = root.AddComponent<WaveMonolith>();
            monolith.run = run;
            monolith.accent = run.World.Accent;
            monolith.Build(run.World.Hero == WeaponType.Staff);
            return monolith;
        }

        private void Build(bool arcane)
        {
            string[] grid = arcane ? SpireGrid : StoneGrid;
            float width = arcane ? 1.75f : 1.35f, height = width * grid.Length / grid[0].Length;
            Vector2 center = new Vector2(0f, BaseY + height * 0.5f);
            top = BaseY + height;
            glow = Part("Glow", Halo, new Vector2(0f, BaseY + 0.08f), new Vector2(width * 2.2f, width * 0.9f), 2);
            aura = Part("Aura", Halo, center, new Vector2(width * 1.9f, height * 1.25f), 2);
            Part("Stone", arcane ? Spire : Stone, center, Vector2.one * width, 3).color = Color.white;
            runes = Part("Runes", arcane ? SpireRunes : StoneRunes, center, Vector2.one * width, 4);
            marker = Part("Interact marker", Arrow, new Vector2(0f, top + 0.4f), Vector2.one * 0.45f, 9);
        }

        private SpriteRenderer Part(string name, Sprite sprite, Vector2 offset, Vector2 size, int order)
        {
            var part = DungeonVisuals.Create(name, transform, (Vector2)transform.position + offset, size, accent, order);
            part.sprite = sprite;
            return part;
        }

        private static Sprite Stone => stone != null ? stone : stone = DungeonVisuals.PaletteSprite("Monolith", StoneGrid, StoneColor);
        private static Sprite StoneRunes => stoneRunes != null ? stoneRunes : stoneRunes = DungeonVisuals.PaletteSprite("Monolith runes", StoneGrid, RuneColor);
        private static Sprite Spire => spire != null ? spire : spire = DungeonVisuals.PaletteSprite("Arcane monolith", SpireGrid, SpireColor);
        private static Sprite SpireRunes => spireRunes != null ? spireRunes : spireRunes = DungeonVisuals.PaletteSprite("Arcane monolith runes", SpireGrid, RuneColor);
        private static Sprite Arrow => arrow != null ? arrow : arrow = DungeonVisuals.PaletteSprite("Interact arrow", ArrowGrid, key => key == 'W' ? Color.white : Color.clear);

        private static Color RuneColor(char key) => key == 'R' || key == 'F' ? Color.white : Color.clear;

        private static Color StoneColor(char key) => key switch
        {
            'O' => new Color(0.05f, 0.05f, 0.08f),
            'L' => new Color(0.5f, 0.52f, 0.62f),
            'M' => new Color(0.31f, 0.32f, 0.41f),
            'D' => new Color(0.19f, 0.19f, 0.27f),
            'R' => new Color(0.1f, 0.1f, 0.15f),
            _ => Color.clear
        };

        private static Color SpireColor(char key) => key switch
        {
            'O' => new Color(0.07f, 0.06f, 0.2f),
            'W' => Color.white,
            'L' => new Color(0.78f, 0.84f, 1f),
            'C' => new Color(0.42f, 0.44f, 0.9f),
            'D' => new Color(0.24f, 0.2f, 0.58f),
            'G' => new Color(0.98f, 0.82f, 0.4f),
            'g' => new Color(0.7f, 0.5f, 0.2f),
            'S' => new Color(0.5f, 0.5f, 0.68f),
            's' => new Color(0.28f, 0.28f, 0.44f),
            'R' => new Color(0.1f, 0.08f, 0.3f),
            _ => Color.clear
        };

        /// <summary>A soft round glow one world unit across, for the light the stone throws.</summary>
        private static Sprite Halo
        {
            get
            {
                if (halo != null) return halo;
                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Monolith halo", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float fade = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (size * 0.5f)) / (size * 0.5f));
                        pixels[y * size + x] = new Color(1f, 1f, 1f, fade * fade);
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                return halo = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            }
        }

        public bool IsNear(DungeonPlayer hero) => hero != null && Vector2.Distance(hero.transform.position, transform.position) < Reach;

        /// <summary>The waves have been called: the stone discharges and its runes go dark.</summary>
        public void Activate()
        {
            if (spent) return;
            spent = true;
            HeroVfx.Pulse(transform.parent, transform.position, 4f, accent, 0.6f);
            HeroVfx.Sparks(transform.parent, (Vector2)transform.position + Vector2.up * 0.6f, accent, 24, 6f, 0.6f, Vector2.up, 360f);
            ScreenFx.Shake(0.25f, 0.35f);
            runes.color = FlameMesh.Alpha(accent, 0.25f);
            glow.enabled = aura.enabled = marker.enabled = false;
        }

        private void Update()
        {
            if (spent) return;
            bool near = run != null && IsNear(run.Player);
            float time = Time.time, beat = Mathf.Sin(time * (near ? 8f : 3f));
            runes.color = near ? Color.Lerp(accent, Color.white, 0.6f + 0.3f * beat) : FlameMesh.Alpha(accent, 0.7f + 0.3f * beat);
            glow.color = FlameMesh.Alpha(accent, (near ? 0.6f : 0.38f) + 0.12f * beat);
            aura.color = FlameMesh.Alpha(accent, (near ? 0.32f : 0.16f) + 0.06f * beat);
            marker.color = near ? Color.white : accent;
            marker.transform.localScale = Vector3.one * (near ? 0.6f : 0.45f);
            marker.transform.position = (Vector2)transform.position + Vector2.up * (top + 0.4f + 0.1f * Mathf.Sin(time * (near ? 7f : 3.5f)));
            if (time < nextPing) return;
            nextPing = time + PingInterval;
            HeroVfx.Pulse(transform.parent, (Vector2)transform.position + Vector2.up * BaseY, Reach, accent, 0.9f);
        }
    }
}
