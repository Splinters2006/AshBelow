using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Shadowstep: the Assassin melts into a burst of dark smoke, a line of fading shadow footfalls marks the path
    /// and she re-forms out of smoke at the landing.
    /// </summary>
    public sealed class ShadowstepVfx : MeshEffect
    {
        public static readonly Color Smoke = new Color(0.08f, 0.03f, 0.14f);
        public static readonly Color Violet = new Color(0.62f, 0.3f, 1f);
        private Vector2 from, to;

        public static void Play(Transform root, Vector2 from, Vector2 to)
        {
            var effect = Spawn<ShadowstepVfx>(root, 0.6f, 8);
            if (effect == null) return;
            effect.from = from;
            effect.to = to;
            effect.Redraw();
        }

        /// <summary>A lone smoke puff, used when the Assassin slips into her Shadow Veil.</summary>
        public static void Puff(Transform root, Vector2 at)
        {
            var effect = Spawn<ShadowstepVfx>(root, 0.6f, 8);
            if (effect == null) return;
            effect.from = effect.to = at;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            Vector2 path = to - from;
            float length = path.magnitude;
            // Shadowy silhouettes step along the path, the earliest fading first.
            if (length > 0.2f)
            {
                Vector2 dir = path / length, side = Vector2.Perpendicular(dir);
                int steps = Mathf.Clamp(Mathf.RoundToInt(length / 0.55f), 2, 9);
                for (int i = 1; i < steps; i++)
                {
                    float u = i / (float)steps, life = Mathf.Clamp01(1f - t * 1.6f + u * 0.4f);
                    if (life <= 0f) continue;
                    Vector2 p = from + path * u + side * (i % 2 == 0 ? 0.08f : -0.08f);
                    Mesh.Ellipse(p, 0.24f, 0.36f, FlameMesh.Alpha(Smoke, 0.55f * life), FlameMesh.Alpha(Violet, 0f), 18);
                    Mesh.Ellipse(p + Vector2.down * 0.38f, 0.2f, 0.06f, FlameMesh.Alpha(Violet, 0.35f * life), FlameMesh.Alpha(Violet, 0f), 14);
                }
            }
            Cloud(from, t, 0f);
            if (length > 0.2f) Cloud(to, t, 1f);
        }

        private void Cloud(Vector2 center, float t, float salt)
        {
            float fade = 1f - t;
            for (int i = 0; i < 9; i++)
            {
                float seed = FlameMesh.Hash(i + salt * 13f, 2.9f), angle = seed * Mathf.PI * 2f + i;
                Vector2 drift = FlameMesh.Polar(angle, (0.25f + seed * 0.35f) * EaseOut(t)) + Vector2.up * t * 0.45f;
                float size = (0.22f + seed * 0.2f) * (0.7f + t * 0.8f);
                Mesh.Disc(center + drift, size, FlameMesh.Alpha(Smoke, 0.7f * fade), FlameMesh.Alpha(Smoke, 0f), 16);
            }
            // Violet embers curl up out of the smoke.
            for (int i = 0; i < 7; i++)
            {
                float seed = FlameMesh.Hash(i + salt * 7f, 6.1f);
                Vector2 p = center + new Vector2((seed - 0.5f) * 0.8f + Mathf.Sin(Age * 10f + i) * 0.05f, -0.2f + t * (0.6f + seed * 0.6f));
                Mesh.Diamond(p, 0.05f + seed * 0.03f, FlameMesh.Alpha(Violet, fade));
            }
        }
    }
}
