using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Rocket Boost: twin jets of thruster exhaust burned along the dash path, white-hot where the Augment is and cooling
    /// to red behind him, with fire licking up off the floor and a trail of smoke that rises as the exhaust burns away.
    /// Touchdown throws out a ring of flame over a scorch mark.
    /// </summary>
    public sealed class RocketBoostVfx : MeshEffect
    {
        public const float Life = 0.6f;
        private static readonly Color Smoke = new Color(0.22f, 0.2f, 0.2f);
        private Vector2 from, to;
        private float blastRadius;

        public static void Play(Transform root, Vector2 from, Vector2 to, float blastRadius)
        {
            var effect = Spawn<RocketBoostVfx>(root, Life, 5);
            if (effect == null) return;
            effect.from = from;
            effect.to = to;
            effect.blastRadius = blastRadius;
            effect.Redraw();
            Vector2 travel = to - from;
            Vector2 back = travel.sqrMagnitude > 0.0001f ? -travel.normalized : Vector2.down;
            HeroVfx.Sparks(root, from, CyborgAttack.MissileColor, 12, 4.5f, 0.35f, back, 70f, 1.1f);
            HeroVfx.Sparks(root, to, FlameMesh.Yellow, 14, 5f, 0.35f, null, 360f, 1f);
            HeroVfx.Sparks(root, to, Smoke, 8, 2f, 0.5f, Vector2.up, 140f, 1.2f);
        }

        protected override void Draw(float t)
        {
            Vector2 travel = to - from;
            float length = travel.magnitude;
            Vector2 dir = length > 0.001f ? travel / length : Vector2.right, side = Vector2.Perpendicular(dir);
            float fade = 1f - t;

            // Scorch mark and touchdown ring of flame.
            float blast = EaseOut(Mathf.Clamp01(t / 0.45f));
            Mesh.Ellipse(to + Vector2.down * 0.1f, blastRadius * 0.7f, blastRadius * 0.45f, new Color(0.05f, 0.03f, 0.02f, 0.45f * fade), new Color(0.05f, 0.03f, 0.02f, 0f), 28);
            if (t < 0.45f)
            {
                float ringFade = 1f - t / 0.45f;
                Mesh.Ring(to, blastRadius * blast, 0.28f * ringFade, FlameMesh.Alpha(FlameMesh.Yellow, ringFade), FlameMesh.Alpha(FlameMesh.Crimson, 0f), 40);
                Mesh.Ring(to, blastRadius * blast * 0.8f, 0.12f * ringFade, FlameMesh.Alpha(FlameMesh.Core, ringFade), FlameMesh.Alpha(FlameMesh.Orange, 0.5f * ringFade), 40);
            }
            if (length < 0.05f) return;

            // Smoke rises from the path as the exhaust burns away.
            int puffs = Mathf.Max(2, Mathf.RoundToInt(length / 0.45f));
            for (int i = 0; i < puffs; i++)
            {
                float u = (i + 0.5f) / puffs, seed = FlameMesh.Hash(i, length);
                Vector2 at = Vector2.Lerp(from, to, u) + side * (seed - 0.5f) * 0.3f + Vector2.up * t * (0.4f + seed * 0.3f);
                float size = 0.12f + 0.25f * t + seed * 0.08f;
                Mesh.Ellipse(at, size, size * 0.8f, FlameMesh.Alpha(Smoke, 0.35f * Mathf.Sin(t * Mathf.PI) * (0.4f + 0.6f * u)), FlameMesh.Alpha(Smoke, 0f), 12);
            }

            // Twin exhaust jets, burning away from the take-off end first.
            float burnt = Mathf.Clamp01(t * 1.4f);
            Vector2 tail = Vector2.Lerp(from, to, burnt);
            for (int jet = -1; jet <= 1; jet += 2)
            {
                Vector2 offset = side * jet * 0.12f;
                Vector2 a = tail + offset, b = to + offset;
                float width = 0.22f * fade;
                // Outer flame: crimson at the cold end, orange at the hot end.
                Mesh.Quad(a - side * width * 0.3f, a + side * width * 0.3f, b + side * width, b - side * width,
                    FlameMesh.Alpha(FlameMesh.Crimson, 0f), FlameMesh.Alpha(FlameMesh.Crimson, 0f), FlameMesh.Alpha(FlameMesh.Orange, 0.8f * fade), FlameMesh.Alpha(FlameMesh.Orange, 0.8f * fade));
                // White-hot core.
                Mesh.Quad(a - side * 0.01f, a + side * 0.01f, b + side * width * 0.35f, b - side * width * 0.35f,
                    FlameMesh.Alpha(FlameMesh.Yellow, 0f), FlameMesh.Alpha(FlameMesh.Yellow, 0f), FlameMesh.Alpha(FlameMesh.Core, fade), FlameMesh.Alpha(FlameMesh.Core, fade));
            }

            // Fire licking up off the floor along the path, dying down from the take-off end.
            int tongues = Mathf.Max(2, Mathf.RoundToInt(length / 0.4f));
            for (int i = 0; i < tongues; i++)
            {
                float u = (i + 0.5f) / tongues;
                if (u < burnt) continue;
                float heat = Mathf.Clamp01((u - burnt) / Mathf.Max(0.05f, 1f - burnt));
                float seed = FlameMesh.Hash(i * 3.1f, length);
                Vector2 at = Vector2.Lerp(from, to, u) + side * (seed - 0.5f) * 0.35f;
                Mesh.Flame(at, Vector2.up, 0.16f + seed * 0.08f, (0.25f + seed * 0.2f) * (0.5f + 0.5f * heat), seed, fade * (0.5f + 0.5f * heat));
            }
        }
    }
}
