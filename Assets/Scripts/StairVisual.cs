using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The way off a floor: gold stairs down, or (past a world's third guardian) a stone ring holding a swirling portal
    /// out of the world. Either stays dark until the floor is cleared.
    /// </summary>
    public sealed class StairVisual : MonoBehaviour
    {
        private const int PortalPixels = 24;
        private const float PortalSize = 2.1f, MoteInterval = 1.1f;
        private static Sprite portalFrame, portalVoid, portalSwirl;
        private SpriteRenderer swirl, innerSwirl, core;
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
            stairs.Part("Dark stairwell", Vector2.zero, new Vector2(1.35f, 1.35f), new Color(0.025f, 0.025f, 0.03f));
            for (int i = 0; i < 5; i++)
            {
                float width = 1.06f - i * 0.13f;
                float y = -0.46f + i * 0.22f;
                Color gold = Color.Lerp(AbilityCatalog.Gold, new Color(0.34f, 0.21f, 0.07f), i / 5f);
                stairs.Part("Step " + (i + 1), new Vector2(0, y), new Vector2(width, 0.19f), gold);
                stairs.Part("Step lip " + (i + 1), new Vector2(0, y - 0.07f), new Vector2(width, 0.035f), gold * 1.25f);
            }
            stairs.Part("Left rail", new Vector2(-0.63f, 0), new Vector2(0.12f, 1.4f), AbilityCatalog.Gold);
            stairs.Part("Right rail", new Vector2(0.63f, 0), new Vector2(0.12f, 1.4f), AbilityCatalog.Gold);
            stairs.SetUnlocked(false, true);
            return stairs;
        }
        private void Part(string name, Vector2 offset, Vector2 size, Color color)
        {
            var part = DungeonVisuals.Create(name, transform, (Vector2)transform.position + offset, size, color, 1);
            stone.Add(part); colors.Add(color);
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
            if (!isPortal || !unlocked) return;
            // The swirl turns in steps, like a sprite animation.
            float time = Time.time, step = Mathf.Floor(time * 12f);
            swirl.transform.rotation = Quaternion.Euler(0f, 0f, -step * 7.5f);
            innerSwirl.transform.rotation = Quaternion.Euler(0f, 0f, step * 11.25f);
            core.transform.localScale = Vector3.one * (0.25f + 0.08f * Mathf.Sin(time * 5f));
            if (time < nextMotes) return;
            nextMotes = time + MoteInterval;
            HeroVfx.Motes(transform.parent, transform.position, PortalSize * 0.5f, portalTint);
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
            for (int i = 0; i < stone.Count; i++)
                stone[i].color = value ? colors[i] : Color.Lerp(colors[i], new Color(0.18f, 0.18f, 0.2f), 0.8f);
        }
    }
}
