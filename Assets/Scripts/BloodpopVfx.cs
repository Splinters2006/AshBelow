using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Bloodpop: every wound on the enemy bursts at once. A blister of blood swells over it for a beat, then pops:
    /// a white flash, a ragged crown of blood thrown outward, drops that arc up and fall back, and a splatter left
    /// on the floor that soaks away. Purely visual.
    /// </summary>
    public sealed class BloodpopVfx : MeshEffect
    {
        private const float Pop = 0.2f;
        private Vector2 at;
        private float radius, seed;

        /// <param name="radius">The enemy's hit radius: the blister covers it and the burst scales with it.</param>
        public static void Play(Transform root, Vector2 at, float radius)
        {
            var effect = Spawn<BloodpopVfx>(root, 0.75f, 10);
            if (effect == null) return;
            effect.at = at;
            effect.radius = Mathf.Max(0.3f, radius);
            effect.seed = at.x * 3.7f + at.y * 9.1f;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            Color blood = SamuraiAttack.Blood, dark = new Color(blood.r * 0.35f, 0f, blood.b * 0.25f);
            if (t < Pop)
            {
                // The blister: blood is dragged in from all round as it swells, shivering, with a wet highlight.
                float swell = t / Pop, size = radius * (0.5f + 0.8f * swell * swell) * (1f + 0.06f * Mathf.Sin(t * 180f));
                for (int i = 0; i < 10; i++)
                {
                    Vector2 dir = FlameMesh.Polar(FlameMesh.Hash(i, seed) * Mathf.PI * 2f, 1f);
                    float far = Mathf.Lerp(radius * 2.6f, size, swell);
                    Mesh.Bar(at + dir * (far + 0.4f), -dir, 0.4f, 0.05f, FlameMesh.Alpha(blood, 0f), FlameMesh.Alpha(blood, 0.9f));
                }
                Mesh.Disc(at, size * 1.25f, FlameMesh.Alpha(blood, 0.5f * swell), FlameMesh.Alpha(blood, 0f), 28);
                Mesh.Disc(at, size, FlameMesh.Alpha(blood, 0.9f), FlameMesh.Alpha(dark, 0.9f), 28);
                Mesh.Ellipse(at + new Vector2(-size * 0.3f, size * 0.35f), size * 0.28f, size * 0.16f, FlameMesh.Alpha(Color.white, 0.85f), FlameMesh.Alpha(Color.white, 0f), 14);
                return;
            }
            float burst = (t - Pop) / (1f - Pop), fade = 1f - burst, spread = EaseOut(burst);
            // What it leaves on the floor: blotches flung out flat, which stay put and soak away last.
            for (int i = 0; i < 9; i++)
            {
                float angle = (i + FlameMesh.Hash(i, seed + 1.3f)) / 9f * Mathf.PI * 2f, far = radius * (0.9f + 1.5f * FlameMesh.Hash(i, seed + 4.2f));
                float size = radius * (0.22f + 0.3f * FlameMesh.Hash(i, seed + 6.6f)) * Mathf.Clamp01(burst / 0.15f);
                Mesh.Ellipse(at + FlameMesh.Polar(angle, far * Mathf.Clamp01(burst / 0.2f)), size * 1.3f, size * 0.7f, FlameMesh.Alpha(dark, 0.75f * fade), FlameMesh.Alpha(dark, 0.5f * fade), 12);
            }
            Mesh.Ellipse(at, radius * 1.3f, radius * 0.8f, FlameMesh.Alpha(dark, 0.7f * fade), FlameMesh.Alpha(dark, 0.4f * fade), 20);
            // The flash of the pop itself, gone in a blink.
            float flash = Mathf.Clamp01(1f - burst / 0.18f);
            if (flash > 0f) Mesh.Disc(at, radius * (1f + 1.4f * (1f - flash)), FlameMesh.Alpha(Color.white, flash), FlameMesh.Alpha(blood, 0.6f * flash), 28);
            // The skin of the blister, torn into a ragged crown that flies outward and thins.
            const int Spikes = 14;
            float inner = radius * (0.8f + 2.2f * spread);
            for (int i = 0; i < Spikes; i++)
            {
                float a0 = (i - 0.5f) / Spikes * Mathf.PI * 2f, a1 = (i + 0.5f) / Spikes * Mathf.PI * 2f, mid = i / (float)Spikes * Mathf.PI * 2f;
                float length = radius * (0.5f + 1.1f * FlameMesh.Hash(i, seed + 2.9f)) * (1f - 0.5f * burst);
                Mesh.Triangle(at + FlameMesh.Polar(a0, inner), at + FlameMesh.Polar(mid, inner + length), at + FlameMesh.Polar(a1, inner),
                    FlameMesh.Alpha(blood, 0.9f * fade), FlameMesh.Alpha(i % 4 == 0 ? Color.white : blood, fade), FlameMesh.Alpha(blood, 0.9f * fade));
            }
            Mesh.Ring(at, inner, 0.14f * fade + 0.01f, FlameMesh.Alpha(blood, fade), 40);
            Mesh.Ring(at, inner * 0.7f, 0.05f * fade + 0.01f, FlameMesh.Alpha(Color.white, 0.8f * fade), 40);
            // Drops thrown up and out, falling back under their own weight, each dragging a short tail.
            for (int i = 0; i < 16; i++)
            {
                float angle = FlameMesh.Hash(i, seed + 8.1f) * Mathf.PI * 2f, speed = radius * (1.8f + 2.8f * FlameMesh.Hash(i, seed + 5.5f));
                float lift = 1.2f + 1.6f * FlameMesh.Hash(i, seed + 7.4f);
                Vector2 velocity = FlameMesh.Polar(angle, speed) + Vector2.up * (lift - 4.5f * burst);
                Vector2 drop = at + FlameMesh.Polar(angle, speed * burst) + Vector2.up * (lift * burst - 2.25f * burst * burst);
                float size = (0.07f + 0.07f * FlameMesh.Hash(i, seed + 3.3f)) * (0.5f + 0.5f * fade);
                Color colour = FlameMesh.Alpha(i % 5 == 0 ? Color.white : blood, fade);
                Mesh.Bar(drop, -velocity.normalized, size * 3.5f, size, colour, FlameMesh.Alpha(blood, 0f));
                Mesh.Disc(drop, size, colour, colour, 8);
            }
        }
    }
}
