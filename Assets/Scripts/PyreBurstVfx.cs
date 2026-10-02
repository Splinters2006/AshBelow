using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Pyre Burst: a burning enemy's body bursts. A white-hot flash at the heart, a shockwave of fire rolling out to the
    /// blast's real radius with tongues of flame riding its crest, embers thrown past it, and a scorched ring left on the
    /// floor where the blast stopped. Bigger ranks draw a bigger blast, so what you see is what it hits.
    /// </summary>
    public sealed class PyreBurstVfx : MeshEffect
    {
        private const int Tongues = 14, Embers = 18;
        private Vector2 center;
        private float radius, seed;

        public static void Play(Transform root, Vector2 center, float radius)
        {
            var effect = Spawn<PyreBurstVfx>(root, 0.6f, 10);
            if (effect == null) return;
            effect.center = center;
            effect.radius = Mathf.Max(0.5f, radius);
            effect.seed = Random.value * 100f;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            // The wave reaches the full radius at 40% of the effect, then the fire lingers on the rim and burns out.
            float spread = EaseOut(t / 0.4f), fade = 1f - t, rim = radius * Mathf.Lerp(0.2f, 1f, spread);
            Color clear = FlameMesh.Alpha(FlameMesh.Orange, 0f);

            // A faint heat haze over the whole blast, so its reach reads at a glance.
            Mesh.Disc(center, rim, FlameMesh.Alpha(FlameMesh.Orange, 0.22f * fade * fade), FlameMesh.Alpha(FlameMesh.Crimson, 0.1f * fade), 48);

            // The scorched ring left behind once the wave has passed.
            if (t > 0.3f)
            {
                float burn = Mathf.Clamp01((t - 0.3f) / 0.2f) * fade;
                Mesh.Ring(center, radius * 0.97f, 0.16f, FlameMesh.Alpha(FlameMesh.Ember, 0.7f * burn), FlameMesh.Alpha(FlameMesh.Ember, 0f), 56);
            }

            // The rolling crest: a hot band with a dark leading edge.
            float band = Mathf.Lerp(0.55f, 0.18f, spread) * Mathf.Min(1f, radius / 2f);
            Mesh.Ring(center, rim - band * 0.25f, band, FlameMesh.Alpha(FlameMesh.Yellow, 0.85f * fade), FlameMesh.Alpha(FlameMesh.Crimson, 0.55f * fade), 56);
            Mesh.Ring(center, rim + band * 0.3f, band * 0.35f, FlameMesh.Alpha(FlameMesh.Crimson, 0.5f * fade), clear, 56);

            // Tongues of flame stand up out of the crest, leaning outward.
            float height = Mathf.Lerp(0.35f, 0.75f, Mathf.Min(1f, radius / 3.5f)) * (1f - t * 0.8f);
            for (int i = 0; i < Tongues; i++)
            {
                float angle = (i + 0.37f * Mathf.Sin(seed + i * 1.7f)) / Tongues * Mathf.PI * 2f;
                Vector2 outward = FlameMesh.Polar(angle, 1f);
                float wobble = 0.85f + 0.3f * FlameMesh.Hash(seed + i, 3.1f);
                Mesh.Flame(center + outward * (rim - band * 0.4f), outward, 0.28f * wobble, height * wobble, seed + i * 0.61f, fade);
            }

            // The heart of the burst: a white-hot flash that collapses fast.
            float flash = Mathf.Clamp01(1f - t / 0.22f);
            if (flash > 0f)
            {
                Mesh.Disc(center, radius * 0.45f * (0.6f + 0.4f * flash), FlameMesh.Alpha(FlameMesh.Core, flash), FlameMesh.Alpha(FlameMesh.Yellow, 0f), 32);
                Mesh.Disc(center, radius * 0.18f, FlameMesh.Alpha(Color.white, flash), FlameMesh.Alpha(FlameMesh.Core, 0f), 24);
            }

            // Embers thrown out past the blast, slowing and dimming as they fly.
            for (int i = 0; i < Embers; i++)
            {
                float s = FlameMesh.Hash(seed * 0.37f + i, 7.3f), angle = (i + s) / Embers * Mathf.PI * 2f;
                float travel = radius * (0.35f + (0.75f + 0.45f * s) * EaseOut(t / 0.7f));
                Vector2 at = center + FlameMesh.Polar(angle, travel) + Vector2.up * 0.25f * t * t;
                float size = (0.05f + 0.05f * s) * (1f - t);
                if (size > 0.005f) Mesh.Diamond(at, size, FlameMesh.Alpha(Color.Lerp(FlameMesh.Yellow, FlameMesh.Crimson, t), fade));
            }
        }
    }
}
