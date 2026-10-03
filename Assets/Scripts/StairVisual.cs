using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The way off a floor: a stone stairwell of gold-edged steps sinking into the dark, or (past a world's third
    /// guardian) a stone ring holding a swirling portal out of the world. Either stays dark until the floor is cleared;
    /// the stairs are barred by an iron grate that lifts away, letting a warm light rise from below.
    /// </summary>
    public sealed class StairVisual : MonoBehaviour
    {
        private const int PortalPixels = 24;
        private const float PortalSize = 2.1f, MoteInterval = 1.1f;
        private const int StairPixels = 32;
        private const float StairSize = 1.6f, GrateLift = 0.45f;
        private static Sprite portalFrame, portalVoid, portalSwirl, stairwell, grateSprite;
        private SpriteRenderer swirl, innerSwirl, core, grate, glow;
        private float unlockedAt = -1f;
        private Color portalTint;
        private float nextMotes;
        private bool isPortal;
        private readonly List<SpriteRenderer> stone = new List<SpriteRenderer>();
        private readonly List<Color> colors = new List<Color>();
        private bool unlocked;
        public static StairVisual Create(Transform parent, Vector2 position)
        {
            var root = new GameObject("Descending stairs");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var stairs = root.AddComponent<StairVisual>();
            var well = DungeonVisuals.Create("Stairwell", root.transform, position, Vector2.one * StairSize, Color.white, 1);
            well.sprite = Stairwell;
            stairs.stone.Add(well); stairs.colors.Add(Color.white);
            // Light from the floor below, only once the way is open.
            stairs.glow = DungeonVisuals.Create("Stair light", root.transform, position + Vector2.up * 0.35f, new Vector2(1.3f, 1f), Color.clear, 2);
            stairs.glow.sprite = DungeonVisuals.GlowSprite;
            stairs.grate = DungeonVisuals.Create("Stair grate", root.transform, position, Vector2.one * StairSize, Color.white, 3);
            stairs.grate.sprite = Grate;
            stairs.SetUnlocked(false, true);
            return stairs;
        }

        /// <summary>The portal that replaces the stairs once a world's last guardian falls, lit in <paramref name="tint"/>.</summary>
        public static StairVisual CreatePortal(Transform parent, Vector2 position, Color tint)
        {
            var root = new GameObject("World portal");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var portal = root.AddComponent<StairVisual>();
            portal.isPortal = true;
            portal.portalTint = tint;
            portal.PortalPart("Portal void", PortalVoid, 1f, Color.white, 1);
            portal.swirl = portal.PortalPart("Portal swirl", PortalSwirl, 1f, tint, 2);
            portal.innerSwirl = portal.PortalPart("Portal inner swirl", PortalSwirl, 0.58f, Color.Lerp(tint, Color.white, 0.6f), 3);
            portal.core = DungeonVisuals.Create("Portal core", root.transform, position, Vector2.one * 0.25f, Color.white, 3);
            var frame = portal.PortalPart("Portal frame", PortalFrame, 1f, Color.white, 4);
            portal.stone.Add(frame); portal.colors.Add(Color.white);
            portal.SetUnlocked(false, true);
            return portal;
        }

        private SpriteRenderer PortalPart(string name, Sprite sprite, float scale, Color color, int order)
        {
            var part = DungeonVisuals.Create(name, transform, transform.position, Vector2.one * PortalSize * scale, color, order);
            part.sprite = sprite;
            return part;
        }

        private static readonly Color StoneLight = new Color(0.56f, 0.53f, 0.5f), StoneMid = new Color(0.4f, 0.37f, 0.36f),
            StoneDark = new Color(0.24f, 0.22f, 0.23f), Mortar = new Color(0.13f, 0.12f, 0.13f), Abyss = new Color(0.02f, 0.02f, 0.03f);
        private static readonly Color GoldDeep = new Color(0.36f, 0.22f, 0.07f);
        // Nearest step first: how many pixels deep each one is. Above the last, the stairwell drops into the dark.
        private static readonly int[] StepDepths = { 5, 4, 4, 3, 3, 2 };

        /// <summary>
        /// Seen from above: a block-built stone rim lit from the upper left around a shaft of steps going down and away,
        /// each narrower and darker than the last with a gold nosing, and the deep end lost in darkness.
        /// </summary>
        private static Sprite Stairwell => stairwell != null ? stairwell : stairwell = PixelSprite("Stairwell", StairPixels, StairSize, (x, y) =>
        {
            const int Rim = 3, Last = StairPixels - 1;
            if (x < Rim || x > Last - Rim || y < Rim || y > Last - Rim)
            {
                if (x == 0 || y == 0 || x == Last || y == Last) return Mortar;
                // Blocks in courses three pixels high, each course offset from the last.
                int course = y / 3;
                if (y % 3 == 0 || (x + course * 3) % 7 == 0) return Mortar;
                bool lit = y > Last - Rim || x < Rim;
                bool shaded = y < Rim || x > Last - Rim;
                return lit ? StoneLight : shaded ? StoneDark : StoneMid;
            }
            // Inside the shaft, counted from its near (bottom) edge.
            int depth = y - Rim, inner = Last - 2 * Rim;
            int step = 0, top = 0;
            while (step < StepDepths.Length && depth >= top + StepDepths[step]) top += StepDepths[step++];
            int fromSide = Mathf.Min(x - Rim, Last - Rim - x);
            if (step >= StepDepths.Length)
                return Color.Lerp(new Color(0.07f, 0.06f, 0.07f), Abyss, (float)(depth - top) / Mathf.Max(1, inner - top));
            float shade = (float)step / StepDepths.Length;
            // Side walls close in a pixel per step, in shadow.
            if (fromSide < step) return Color.Lerp(StoneDark, Abyss, shade + 0.2f);
            if (fromSide == step) return Color.Lerp(Mortar, Abyss, shade);
            // The nosing at the far edge of each step catches the light; the step beyond starts in its shadow.
            if (depth == top + StepDepths[step] - 1) return Color.Lerp(AbilityCatalog.Gold, GoldDeep, shade);
            if (depth == top && step > 0) return Color.Lerp(Mortar, Abyss, shade);
            return Color.Lerp(Color.Lerp(StoneMid, StoneLight, (depth - top) / (float)StepDepths[step]), Abyss, shade * 0.85f);
        });

        /// <summary>Iron bars across the shaft, with two cross bars and rivets where they meet.</summary>
        private static Sprite Grate => grateSprite != null ? grateSprite : grateSprite = PixelSprite("Stair grate", StairPixels, StairSize, (x, y) =>
        {
            const int Rim = 3, Last = StairPixels - 1;
            if (x < Rim - 1 || x > Last - Rim + 1 || y < Rim - 1 || y > Last - Rim + 1) return Color.clear;
            bool bar = (x - Rim) % 5 == 2, cross = y == 10 || y == 21;
            bool frame = x == Rim - 1 || x == Last - Rim + 1 || y == Rim - 1 || y == Last - Rim + 1;
            if (bar && cross) return new Color(0.62f, 0.6f, 0.58f);
            if (frame || cross) return y == 11 || y == 22 ? new Color(0.48f, 0.47f, 0.5f) : new Color(0.27f, 0.26f, 0.3f);
            if (bar) return new Color(0.36f, 0.35f, 0.4f);
            if ((x - Rim) % 5 == 3) return new Color(0f, 0f, 0f, 0.35f);
            return Color.clear;
        });

        /// <summary>A square pixel sprite <paramref name="size"/> world units across, painted pixel by pixel from the bottom left.</summary>
        private static Sprite PixelSprite(string name, int pixels, float size, System.Func<int, int, Color> paint)
        {
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var colors = new Color[pixels * pixels];
            for (int y = 0; y < pixels; y++)
                for (int x = 0; x < pixels; x++)
                    colors[y * pixels + x] = paint(x, y);
            texture.SetPixels(colors);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, pixels, pixels), Vector2.one * 0.5f, pixels);
        }

        // A stone ring set with gold studs, lit from the upper left.
        private static Sprite PortalFrame => portalFrame != null ? portalFrame : portalFrame = PortalSprite("Portal frame", (distance, angle, lit) =>
            distance > 1f || distance < 0.68f ? Color.clear
            : distance > 0.93f || distance < 0.74f ? new Color(0.07f, 0.06f, 0.13f)
            : Mathf.Abs(Mathf.Repeat(angle / (Mathf.PI * 0.25f), 1f) - 0.5f) < 0.16f ? new Color(0.98f, 0.8f, 0.36f)
            : lit ? new Color(0.6f, 0.6f, 0.72f) : new Color(0.33f, 0.33f, 0.46f));

        private static Sprite PortalVoid => portalVoid != null ? portalVoid : portalVoid = PortalSprite("Portal void", (distance, angle, lit) =>
            distance < 0.76f ? new Color(0.03f, 0.02f, 0.07f) : Color.clear);

        // Three spiral arms in three brightnesses, white so the world's colour can tint them.
        private static Sprite PortalSwirl => portalSwirl != null ? portalSwirl : portalSwirl = PortalSprite("Portal swirl", (distance, angle, lit) =>
        {
            if (distance >= 0.76f) return Color.clear;
            float arm = Mathf.Repeat(angle / (Mathf.PI * 2f) * 3f + distance * 2.2f, 1f);
            return arm < 0.3f ? Color.white : arm < 0.6f ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.22f);
        });

        /// <summary>A round pixel sprite one world unit across: each pixel is coloured by its distance from the centre (0-1), its angle and whether it faces the light.</summary>
        private static Sprite PortalSprite(string name, System.Func<float, float, bool, Color> paint)
        {
            var texture = new Texture2D(PortalPixels, PortalPixels, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[PortalPixels * PortalPixels];
            for (int y = 0; y < PortalPixels; y++)
                for (int x = 0; x < PortalPixels; x++)
                {
                    Vector2 offset = (new Vector2(x + 0.5f, y + 0.5f) - Vector2.one * (PortalPixels * 0.5f)) / (PortalPixels * 0.5f);
                    pixels[y * PortalPixels + x] = paint(offset.magnitude, Mathf.Atan2(offset.y, offset.x), offset.y - offset.x > 0f);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, PortalPixels, PortalPixels), Vector2.one * 0.5f, PortalPixels);
        }

        private void Update()
        {
            if (!isPortal) { UpdateStairs(); return; }
            if (!unlocked) return;
            // The swirl turns in steps, like a sprite animation.
            float time = Time.time, step = Mathf.Floor(time * 12f);
            swirl.transform.rotation = Quaternion.Euler(0f, 0f, -step * 7.5f);
            innerSwirl.transform.rotation = Quaternion.Euler(0f, 0f, step * 11.25f);
            core.transform.localScale = Vector3.one * (0.25f + 0.08f * Mathf.Sin(time * 5f));
            if (time < nextMotes) return;
            nextMotes = time + MoteInterval;
            HeroVfx.Motes(transform.parent, transform.position, PortalSize * 0.5f, portalTint);
        }
        /// <summary>The grate lifting off and fading, then the light below breathing and the odd golden mote rising out.</summary>
        private void UpdateStairs()
        {
            if (!unlocked) return;
            float since = Time.time - unlockedAt;
            float lift = Mathf.Clamp01(since / GrateLift);
            if (grate.enabled)
            {
                grate.transform.position = transform.position + Vector3.up * (0.25f * lift * lift);
                grate.color = new Color(1f, 1f, 1f, 1f - lift);
                if (lift >= 1f) grate.enabled = false;
            }
            glow.color = FlameMesh.Alpha(AbilityCatalog.Gold, lift * (0.32f + 0.1f * Mathf.Sin(Time.time * 2.4f)));
            if (Time.time < nextMotes) return;
            nextMotes = Time.time + MoteInterval * 1.4f;
            HeroVfx.Motes(transform.parent, (Vector2)transform.position + Vector2.up * 0.3f, 0.45f, AbilityCatalog.Gold, 5, 1.2f);
        }

        public void SetUnlocked(bool value, bool force = false)
        {
            if (!force && unlocked == value) return;
            unlocked = value;
            if (isPortal)
            {
                swirl.enabled = innerSwirl.enabled = core.enabled = value;
                if (value && !force) HeroVfx.Pulse(transform.parent, transform.position, PortalSize * 1.4f, portalTint, 0.7f);
            }
            else
            {
                unlockedAt = value ? Time.time : -1f;
                grate.enabled = true;
                grate.transform.position = transform.position;
                grate.color = Color.white;
                glow.color = Color.clear;
                if (value && !force)
                {
                    HeroVfx.Pulse(transform.parent, transform.position, StairSize * 1.2f, AbilityCatalog.Gold, 0.6f);
                    HeroVfx.Sparks(transform.parent, transform.position, new Color(0.6f, 0.58f, 0.6f), 10, 3f, 0.35f);
                }
            }
            for (int i = 0; i < stone.Count; i++)
                stone[i].color = value ? colors[i] : Color.Lerp(colors[i], new Color(0.18f, 0.18f, 0.2f), isPortal ? 0.8f : 0.45f);
        }
    }
}
