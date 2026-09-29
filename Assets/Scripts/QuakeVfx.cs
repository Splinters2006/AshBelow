using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Earthshatter: cracks split the ground from the stomp while each quake ring heaves up a band of jagged rock
    /// that sinks back into a rolling wave of dust. Purely visual; the Knight applies the damage.
    /// </summary>
    public sealed class QuakeVfx : MeshEffect
    {
        private const float RingLife = 0.55f;
        private static readonly Color Dust = new Color(0.72f, 0.62f, 0.48f);
        private static readonly Color Rock = new Color(0.36f, 0.3f, 0.25f);
        private static readonly Color Crack = new Color(0.12f, 0.08f, 0.06f);
        private readonly Vector2[] points = new Vector2[8];
        private Vector2 center;
        private float radius, interval;
        private int rings;
        private Color color;

        public static void Play(Transform root, Vector2 center, float radius, int rings, float interval, Color color)
        {
            rings = Mathf.Max(1, rings);
            var effect = Spawn<QuakeVfx>(root, (rings - 1) * interval + RingLife + 0.5f, 3);
            if (effect == null) return;
            effect.center = center;
            effect.radius = radius;
            effect.rings = rings;
            effect.interval = Mathf.Max(0.01f, interval);
            effect.color = color;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            float spread = (rings - 1) * interval + 0.15f;
            float front = radius * Mathf.Clamp01(Age / spread);
            float fade = Mathf.Clamp01((Duration - Age) / 0.5f);
            // The crater where the Knight stomped.
            Mesh.Disc(center, 0.55f, FlameMesh.Alpha(Crack, 0.55f * fade), FlameMesh.Alpha(Rock, 0f), 24);
            // Jagged cracks race outward with the quake.
            for (int c = 0; c < 10; c++)
            {
                float seed = FlameMesh.Hash(c, 1.7f), angle = c * Mathf.PI * 0.2f + seed * 0.5f, reach = front * (0.75f + seed * 0.3f);
                for (int i = 0; i < points.Length; i++)
                {
                    float u = i / (float)(points.Length - 1);
                    float jag = (FlameMesh.Hash(c * 10 + i, 5.3f) - 0.5f) * 0.45f * u;
                    points[i] = center + FlameMesh.Polar(angle + jag, 0.3f + reach * u);
                }
                Stroke(points, points.Length, 0.14f, 0.02f, FlameMesh.Alpha(Crack, 0.85f * fade), FlameMesh.Alpha(Crack, 0.3f * fade));
                Stroke(points, points.Length, 0.05f, 0.01f, FlameMesh.Alpha(color, 0.5f * fade), FlameMesh.Alpha(color, 0f));
            }
            for (int ring = 1; ring <= rings; ring++)
            {
                float life = (Age - (ring - 1) * interval) / RingLife;
                if (life < 0f || life > 1f) continue;
                float r = radius * ring / rings, heave = Mathf.Sin(Mathf.Min(1f, life * 1.6f) * Mathf.PI);
                // A rolling wave of dust just outside the ring.
                Mesh.Ring(center, r + 0.25f * life, 0.35f + 0.4f * life, FlameMesh.Alpha(Dust, 0.45f * (1f - life)), FlameMesh.Alpha(Dust, 0f), 56);
                Mesh.Ring(center, r, 0.09f, FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.3f), 0.8f * (1f - life)), 64);
                // Shards of rock punch up through the ground and sink back.
                int shards = 10 + ring * 5;
                for (int i = 0; i < shards; i++)
                {
                    float seed = FlameMesh.Hash(ring * 50 + i, 3.1f), a = (i + seed * 0.6f) * Mathf.PI * 2f / shards;
                    Vector2 foot = center + FlameMesh.Polar(a, r + (seed - 0.5f) * 0.2f);
                    float h = heave * (0.18f + seed * 0.22f) * (0.8f + ring * 0.08f), w = 0.1f + seed * 0.08f;
                    if (h < 0.01f) continue;
                    Color top = FlameMesh.Alpha(Color.Lerp(Rock, Dust, 0.5f), 1f), bottom = FlameMesh.Alpha(Rock, 1f);
                    Mesh.Triangle(foot + Vector2.left * w, foot + new Vector2((seed - 0.5f) * 0.08f, h), foot + Vector2.right * w, bottom, top, bottom);
                }
            }
        }
    }
}
