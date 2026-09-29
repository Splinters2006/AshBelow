using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Sharpened Dagger's whet: a dagger flashes up over the Assassin's head, a whetstone scrapes along its edge
    /// throwing sparks and leaving the honed edge glowing, then the point gleams. Follows the hero. Purely visual.
    /// </summary>
    public sealed class SharpenVfx : MeshEffect
    {
        private static readonly Color Steel = new Color(0.58f, 0.62f, 0.76f), Honed = new Color(0.92f, 0.97f, 1f);
        private static readonly Color Stone = new Color(0.22f, 0.2f, 0.28f), SparkHot = new Color(1f, 0.86f, 0.55f);
        private const float ScrapeStart = 0.12f, ScrapeSpan = 0.36f;
        private Transform hero;
        private Vector2 anchor;
        private int passes;
        private float size;

        /// <summary><paramref name="full"/> is the activation (two long scrapes); a backstab gets one quick scrape.</summary>
        public static void Play(Transform root, Transform hero, bool full)
        {
            var effect = Spawn<SharpenVfx>(root, full ? 0.6f : 0.4f, 11);
            if (effect == null) return;
            effect.hero = hero;
            effect.anchor = hero != null ? (Vector2)hero.position : Vector2.zero;
            effect.passes = full ? 2 : 1;
            effect.size = full ? 1f : 0.75f;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            if (hero != null) anchor = hero.position;
            float appear = EaseOut(t / 0.15f), fade = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
            Vector2 up = Vector2.up, side = Vector2.right;
            Vector2 center = anchor + up * (1.05f + 0.1f * t) * size;
            float length = 0.9f * size * appear, halfWidth = 0.09f * size * appear;
            Vector2 hilt = center - up * length * 0.35f, tip = center + up * length * 0.65f, edge = hilt + side * halfWidth;

            Mesh.Ellipse(center + up * length * 0.15f, 0.35f * size, 0.6f * size,
                FlameMesh.Alpha(SharpenedDagger.EdgeColor, 0.3f * fade), FlameMesh.Alpha(SharpenedDagger.EdgeColor, 0f), 24);
            // The blade: a shaded flat on the left and a bright bevel on the right, then guard, grip and pommel.
            Mesh.Triangle(hilt - side * halfWidth, tip, hilt, FlameMesh.Alpha(Steel, fade), FlameMesh.Alpha(Honed, fade), FlameMesh.Alpha(Steel, fade));
            Mesh.Triangle(hilt, tip, edge, FlameMesh.Alpha(Honed, fade), FlameMesh.Alpha(Honed, fade), FlameMesh.Alpha(Honed, fade));
            Mesh.Bar(hilt - side * halfWidth * 2.2f, side, halfWidth * 4.4f, 0.06f * size, FlameMesh.Alpha(Stone, fade), FlameMesh.Alpha(Stone, fade));
            Mesh.Bar(hilt, -up, 0.22f * size, 0.07f * size, FlameMesh.Alpha(ShadowstepVfx.Smoke, fade), FlameMesh.Alpha(ShadowstepVfx.Smoke, fade));
            Mesh.Diamond(hilt - up * 0.26f * size, 0.05f * size, FlameMesh.Alpha(Honed, fade));

            float pass = ScrapeSpan / passes;
            for (int k = 0; k < passes; k++)
            {
                float u = (t - ScrapeStart - k * pass) / pass;
                if (u < 0f || u > 1f) continue;
                float along = u * u * (3f - 2f * u);
                Vector2 contact = Vector2.Lerp(edge, tip, along), toContact = contact - edge;
                // The freshly honed edge glows white behind the stone.
                if (toContact.sqrMagnitude > 0.0001f)
                    Mesh.Bar(edge, toContact.normalized, toContact.magnitude, 0.04f * size, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade));
                Vector2 slab = (side + up * 0.35f).normalized;
                Mesh.Bar(contact - slab * 0.02f, slab, 0.32f * size, 0.11f * size, FlameMesh.Alpha(Stone, fade), FlameMesh.Alpha(Stone, fade));
                Mesh.Bar(contact, slab, 0.3f * size, 0.025f * size, FlameMesh.Alpha(Steel, fade), FlameMesh.Alpha(Steel, fade));
                // Sparks spray off the edge where the stone bites.
                float frame = Mathf.Floor(Age * 40f);
                for (int j = 0; j < 6; j++)
                {
                    float h = FlameMesh.Hash(j + k * 7, frame);
                    Vector2 direction = FlameMesh.Polar(-0.7f + h * 1.3f, 1f);
                    float spark = (0.12f + 0.22f * FlameMesh.Hash(frame, j)) * size;
                    Mesh.Bar(contact + direction * 0.05f, direction, spark, 0.025f, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(SparkHot, 0f));
                }
            }

            // Once honed, a gleam runs up the blade and flares into a star at the point.
            float honedAt = ScrapeStart + ScrapeSpan;
            float run = Mathf.Clamp01((t - honedAt) / 0.12f);
            if (run > 0f && run < 1f)
            {
                Vector2 shine = Vector2.Lerp(hilt, tip, run);
                float w = halfWidth * (1f - run) + 0.02f;
                Mesh.Bar(shine - side * w, side, w * 2f, 0.07f * size, FlameMesh.Alpha(Color.white, 0.9f * fade), FlameMesh.Alpha(Color.white, 0.9f * fade));
            }
            float gleam = Mathf.Clamp01(1f - Mathf.Abs(t - honedAt - 0.14f) / 0.14f);
            if (gleam > 0f)
            {
                float g = gleam * size;
                Mesh.Bar(tip - up * 0.4f * g, up, 0.8f * g, 0.045f, FlameMesh.Alpha(Color.white, gleam), FlameMesh.Alpha(Color.white, gleam));
                Mesh.Bar(tip - side * 0.32f * g, side, 0.64f * g, 0.045f, FlameMesh.Alpha(Color.white, gleam), FlameMesh.Alpha(Color.white, gleam));
                for (int d = -1; d <= 1; d += 2)
                {
                    Vector2 diagonal = new Vector2(d, 1f).normalized;
                    Mesh.Bar(tip - diagonal * 0.16f * g, diagonal, 0.32f * g, 0.03f, FlameMesh.Alpha(SharpenedDagger.EdgeColor, gleam), FlameMesh.Alpha(SharpenedDagger.EdgeColor, gleam));
                }
                Mesh.Ellipse(tip, 0.22f * g, 0.22f * g, FlameMesh.Alpha(Color.white, 0.6f * gleam), FlameMesh.Alpha(SharpenedDagger.EdgeColor, 0f), 16);
            }
        }
    }
}
