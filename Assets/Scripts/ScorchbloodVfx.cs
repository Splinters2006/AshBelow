using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Scorchblood: an enemy's blood catches fire. <see cref="Ignite"/> marks the moment burning and bleeding combine:
    /// blood is flung up out of the body and bursts into flame as it falls, round a column of blood-red fire.
    /// <see cref="Burst"/> rides a Pyre Burst or Crimson Bloom from a scorchblooded body: a wave of burning blood rolls
    /// out to the blast's real radius, throwing flaming drops past it. Drawn as pixel art, like the sprites. Purely visual.
    /// </summary>
    public sealed class ScorchbloodVfx : MeshEffect
    {
        private const int Drops = 10, Tongues = 16;
        private Vector2 at;
        private float radius, seed;
        private bool burst;

        /// <param name="radius">The enemy's hit radius: the flare covers it.</param>
        public static void Ignite(Transform root, Vector2 at, float radius) => Play(root, at, Mathf.Max(0.3f, radius), false, 0.75f);

        /// <param name="radius">The blast's real radius, so what you see is what it scorches.</param>
        public static void Burst(Transform root, Vector2 at, float radius) => Play(root, at, Mathf.Max(0.5f, radius), true, 0.7f);

        private static void Play(Transform root, Vector2 at, float radius, bool burst, float duration)
        {
            var effect = Spawn<ScorchbloodVfx>(root, duration, 11);
            if (effect == null) return;
            effect.Mesh.Pixelated = true;
            effect.at = at;
            effect.radius = radius;
            effect.burst = burst;
            effect.seed = Random.value * 100f;
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            if (burst) DrawBurst(t);
            else DrawIgnite(t);
        }

        private void DrawIgnite(float t)
        {
            Color blood = DungeonEnemy.BleedColor, fire = DungeonEnemy.ScorchColor;
            float fade = 1f - t, flare = Mathf.Clamp01(1f - t / 0.3f);

            // A blood-red glow that swells over the body and burns down.
            Mesh.Disc(at, radius * (1f + 0.35f * EaseOut(t / 0.3f)), FlameMesh.Alpha(blood, 0.35f * fade), FlameMesh.Alpha(FlameMesh.Ember, 0f), 32);
            Mesh.Ring(at, radius * (0.8f + 0.5f * EaseOut(t / 0.45f)), 0.08f * fade, FlameMesh.Alpha(fire, 0.6f * fade), FlameMesh.Alpha(blood, 0f), 40);
            // A dark shockwave with a blood-red edge rolls out as the blood takes fire.
            float shock = EaseOut(t / 0.5f);
            Mesh.Ring(at, radius * (1f + 1.4f * shock), 0.18f * fade, FlameMesh.Alpha(Color.black, 0.45f * fade), FlameMesh.Alpha(blood, 0.6f * fade), 48);

            // A column of blood-red fire stands up out of the body.
            float height = radius * (1.5f + 0.6f * flare) * (1f - 0.6f * t);
            BloodFlame(at + Vector2.down * radius * 0.3f, Vector2.up, radius * 0.75f, height, seed, 0.85f * fade);

            // Blood is flung up and catches fire on the way down.
            for (int i = 0; i < Drops + 4; i++)
            {
                float s = FlameMesh.Hash(seed + i, 5.3f), angle = Mathf.Lerp(0.55f, Mathf.PI - 0.55f, (i + s) / (Drops + 4));
                Vector2 velocity = FlameMesh.Polar(angle, 2f + 1.2f * s);
                Vector2 drop = at + velocity * t * 0.6f + Vector2.down * 3.2f * t * t;
                float size = (0.06f + 0.05f * s) * (1f - 0.7f * t);
                Color color = Color.Lerp(blood, fire, Mathf.Clamp01(t / 0.35f));
                Mesh.Diamond(drop, size, FlameMesh.Alpha(color, 0.85f * fade));
                if (t > 0.3f) BloodFlame(drop, Vector2.up, size * 2.2f, size * 4f, seed + i * 0.7f, 0.85f * fade);
            }

            // A red-hot spark at the moment it catches.
            if (flare > 0f) Mesh.Disc(at, radius * 0.4f * flare, FlameMesh.Alpha(fire, 0.8f * flare), FlameMesh.Alpha(blood, 0f), 24);
        }

        private void DrawBurst(float t)
        {
            Color blood = DungeonEnemy.BleedColor, fire = DungeonEnemy.ScorchColor, dark = FlameMesh.Ember;
            float spread = EaseOut(t / 0.45f), fade = 1f - t, rim = radius * Mathf.Lerp(0.15f, 1f, spread);

            // Burning blood soaks the whole blast, darkening as it cools.
            Mesh.Disc(at, rim, FlameMesh.Alpha(Color.Lerp(blood, dark, t), 0.25f * fade), FlameMesh.Alpha(dark, 0.1f * fade), 48);

            // The wave: a band of blood with a burning crest.
            float band = Mathf.Lerp(0.5f, 0.16f, spread) * Mathf.Min(1f, radius / 2f);
            Mesh.Ring(at, rim - band * 0.4f, band, FlameMesh.Alpha(blood, 0.65f * fade), FlameMesh.Alpha(fire, 0.75f * fade), 56);
            Mesh.Ring(at, rim + band * 0.25f, band * 0.4f, FlameMesh.Alpha(fire, 0.55f * fade), FlameMesh.Alpha(blood, 0f), 56);

            // Blood-red tongues of flame lean out of the crest.
            float height = Mathf.Lerp(0.25f, 0.5f, Mathf.Min(1f, radius / 3.5f)) * (1f - t * 0.8f);
            for (int i = 0; i < Tongues; i++)
            {
                float angle = (i + 0.5f + 0.3f * Mathf.Sin(seed + i * 2.3f)) / Tongues * Mathf.PI * 2f;
                Vector2 outward = FlameMesh.Polar(angle, 1f);
                float wobble = 0.8f + 0.4f * FlameMesh.Hash(seed + i, 1.7f);
                BloodFlame(at + outward * rim, outward, 0.18f * wobble, height * wobble, seed + i * 0.43f, 0.85f * fade);
            }

            // Flaming drops of blood thrown out past the blast, trailing fire.
            for (int i = 0; i < Drops * 2; i++)
            {
                float s = FlameMesh.Hash(seed * 0.53f + i, 4.1f), angle = (i + s) / (Drops * 2) * Mathf.PI * 2f;
                float travel = radius * (0.3f + (0.75f + 0.25f * s) * EaseOut(t / 0.75f));
                Vector2 outward = FlameMesh.Polar(angle, 1f), drop = at + outward * travel + Vector2.down * 0.3f * t * t;
                float size = (0.06f + 0.04f * s) * (1f - t);
                if (size <= 0.005f) continue;
                Mesh.Bar(drop, -outward, size * 5f, size, FlameMesh.Alpha(blood, 0.85f * fade), FlameMesh.Alpha(blood, 0f));
                Mesh.Diamond(drop, size, FlameMesh.Alpha(Color.Lerp(fire, blood, t), 0.85f * fade));
            }
        }

        /// <summary>A tongue of blood-red fire: dark at the edge, crimson in the body, a red-hot core.</summary>
        private void BloodFlame(Vector2 root, Vector2 up, float width, float height, float seed, float alpha)
            => Mesh.Tongue(root, up, width, height, seed, FlameMesh.Alpha(FlameMesh.Ember, 0.85f * alpha),
                FlameMesh.Alpha(FlameMesh.Crimson, 0.95f * alpha), FlameMesh.Alpha(DungeonEnemy.ScorchColor, 0.95f * alpha));
    }
}
