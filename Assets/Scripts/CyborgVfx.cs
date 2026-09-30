using UnityEngine;

namespace Slopgame
{
    /// <summary>Effects for the Augment: the straight plasma ray and the sentry turret's body.</summary>
    public static class CyborgVfx
    {
        /// <summary>A straight ray of light: a soft glow under a white-hot core, fading out quickly, with a flash at each end.</summary>
        public static void Ray(Transform parent, Vector2 from, Vector2 to, Color color, float width)
        {
            if (parent == null) return;
            Vector2 offset = to - from;
            float length = offset.magnitude;
            HeroVfx.Pulse(parent, from, 0.25f + width, color, 0.12f);
            if (length < 0.05f) return;
            Vector2 middle = (from + to) * 0.5f;
            var glowColor = color;
            glowColor.a = 0.4f;
            var ray = DungeonVisuals.Create("Plasma ray", parent, middle, new Vector2(length, width * 2.4f), glowColor, 6);
            ray.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
            var core = DungeonVisuals.Create("Core", ray.transform, middle, new Vector2(1f, 0.3f), Color.Lerp(color, Color.white, 0.75f), 7);
            core.transform.localPosition = Vector2.zero;
            ray.gameObject.AddComponent<FadingSprite>().Duration = 0.12f + width * 0.4f;
            HeroVfx.Sparks(parent, to, Color.Lerp(color, Color.white, 0.5f), 4, 3f, 0.2f, -offset, 120f, 0.8f);
        }

        /// <summary>The sentry turret: tripod, housing, a barrel that turns to aim, and a glowing plasma eye.</summary>
        public static Transform TurretBody(Transform parent, Vector2 position, out Transform barrel)
        {
            var root = new GameObject("Sentry turret").transform;
            root.SetParent(parent, false);
            root.position = position;
            var steel = new Color(0.62f, 0.7f, 0.82f);
            var dark = new Color(0.2f, 0.23f, 0.3f);
            Part(root, "Shadow", new Vector2(0f, -0.3f), new Vector2(0.7f, 0.14f), new Color(0.01f, 0.02f, 0.04f, 0.35f), 2);
            Part(root, "Left leg", new Vector2(-0.2f, -0.2f), new Vector2(0.08f, 0.26f), dark, 4);
            Part(root, "Right leg", new Vector2(0.2f, -0.2f), new Vector2(0.08f, 0.26f), dark, 4);
            Part(root, "Mast", new Vector2(0f, -0.12f), new Vector2(0.1f, 0.24f), dark, 4);
            Part(root, "Housing", new Vector2(0f, 0.06f), new Vector2(0.42f, 0.3f), steel, 5);
            Part(root, "Housing shade", new Vector2(0f, -0.05f), new Vector2(0.42f, 0.08f), new Color(0.42f, 0.5f, 0.62f), 5);
            barrel = new GameObject("Barrel").transform;
            barrel.SetParent(root, false);
            barrel.localPosition = new Vector2(0f, 0.08f);
            Part(barrel, "Barrel", new Vector2(0.24f, 0f), new Vector2(0.34f, 0.1f), dark, 6);
            Part(barrel, "Muzzle", new Vector2(0.42f, 0f), new Vector2(0.06f, 0.14f), CyborgAttack.Plasma, 7);
            Part(root, "Eye", new Vector2(0f, 0.1f), new Vector2(0.1f, 0.1f), CyborgAttack.Plasma, 7);
            return root;
        }

        private static void Part(Transform parent, string name, Vector2 offset, Vector2 size, Color color, int order)
        {
            var part = DungeonVisuals.Create(name, parent, parent.position, size, color, order);
            part.transform.localPosition = offset;
        }
    }
}
