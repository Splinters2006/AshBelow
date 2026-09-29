using UnityEngine;

namespace Slopgame
{
    /// <summary>Windstep: curling gusts of air sweep along the dash, leaves tumble in their wake and a small whirl settles at the landing.</summary>
    public sealed class WindstepVfx : MeshEffect
    {
        private const int Gusts = 7, Points = 14;
        private static readonly Color Air = new Color(0.86f, 1f, 0.92f);
        private readonly Vector2[] points = new Vector2[Points];
        private Vector2 from, to;

        public static void Play(Transform root, Vector2 from, Vector2 to)
        {
            var effect = Spawn<WindstepVfx>(root, 0.55f);
            if (effect == null) return;
            effect.from = from;
            effect.to = (to - from).sqrMagnitude < 0.01f ? from + Vector2.right * 0.1f : to;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            Vector2 path = to - from, dir = path.normalized, side = Vector2.Perpendicular(dir);
            float length = path.magnitude, fade = 1f - t * t;
            Color green = AbilityCatalog.Green;
            for (int g = 0; g < Gusts; g++)
            {
                float seed = FlameMesh.Hash(g, 4.2f), lane = (g - (Gusts - 1) * 0.5f) * 0.14f + (seed - 0.5f) * 0.1f;
                float head = EaseOut(t * (1.6f + seed * 0.6f)) * 1.25f - seed * 0.1f, tail = head - 0.45f - seed * 0.2f;
                float amp = 0.08f + seed * 0.1f, phase = seed * 20f;
                for (int i = 0; i < Points; i++)
                {
                    float s = Mathf.Clamp01(Mathf.Lerp(tail, head, i / (float)(Points - 1)));
                    // Each gust ripples and curls up at its head, like a streak of wind.
                    float curl = Mathf.Sin(s * Mathf.PI * 3f + phase + Age * 12f) * amp;
                    points[i] = from + dir * s * length + side * (lane + curl);
                }
                Color body = Color.Lerp(green, Air, 0.5f + seed * 0.4f);
                Stroke(points, Points, 0.01f, 0.07f, FlameMesh.Alpha(body, 0f), FlameMesh.Alpha(body, 0.85f * fade));
            }
            // Leaves caught in the draft tumble along behind the archer.
            for (int i = 0; i < 6; i++)
            {
                float seed = FlameMesh.Hash(i, 7.7f), s = Mathf.Clamp01(EaseOut(t * 1.4f) - seed * 0.5f);
                Vector2 p = from + dir * s * length + side * ((seed - 0.5f) * 0.9f + Mathf.Sin(Age * 14f + seed * 9f) * 0.15f);
                float spin = Age * (8f + seed * 6f);
                Vector2 f = FlameMesh.Polar(spin, 0.09f), n = Vector2.Perpendicular(f) * 0.45f;
                Color leaf = FlameMesh.Alpha(Color.Lerp(green, new Color(0.35f, 0.7f, 0.3f), seed), fade);
                Mesh.Quad(p - f, p - n, p + f, p + n, leaf, leaf, leaf, leaf);
            }
            // A whirl of air settling around the landing spot.
            float whirl = Mathf.Clamp01((t - 0.15f) / 0.85f);
            if (whirl <= 0f) return;
            for (int k = 0; k < 3; k++)
            {
                float start = k * Mathf.PI * 2f / 3f + Age * 9f, radius = 0.35f + 0.4f * whirl;
                for (int i = 0; i < Points; i++)
                {
                    float u = i / (float)(Points - 1), a = start + u * Mathf.PI * 1.1f;
                    points[i] = to + FlameMesh.Polar(a, radius * (0.7f + 0.3f * u)) * new Vector2(1f, 0.55f) + Vector2.down * 0.3f;
                }
                Stroke(points, Points, 0.01f, 0.06f, FlameMesh.Alpha(Air, 0f), FlameMesh.Alpha(Air, 0.7f * (1f - whirl)));
            }
        }
    }
}
