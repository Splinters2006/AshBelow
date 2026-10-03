using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Pixel-art stand-ins for <see cref="HeroVfx.Pulse"/>, <see cref="HeroVfx.Sparks"/> and <see cref="CombatVfx.Ring"/>,
    /// for effects that sit beside the pixel fire and ice (the fire and ice guardians' strikes, scorchblood): every shape
    /// snaps to the <see cref="FlameMesh.Pixel"/> grid and fades in steps. Purely visual.
    /// </summary>
    public sealed class PixelBurstVfx : MeshEffect
    {
        private enum Shape { Pulse, Sparks, Ring }
        private const int FadeSteps = 4;
        private Shape shape;
        private Vector2 at;
        private Color color;
        private float radius, size;
        private Vector2[] velocities;

        /// <summary>A disc that swells out to <paramref name="radius"/> behind a bright edge, then fades.</summary>
        public static void Pulse(Transform root, Vector2 at, float radius, Color color, float duration = 0.35f)
        {
            var effect = Play(root, Shape.Pulse, at, color, duration);
            if (effect == null) return;
            effect.radius = Mathf.Clamp(radius, 0.1f, 12f);
            effect.Redraw();
        }

        /// <summary>Pixel sparks that fly outward and slow down, each trailing a short streak. A direction biases them into a spray.</summary>
        public static void Sparks(Transform root, Vector2 at, Color color, int count = 8, float speed = 4f, float duration = 0.3f,
            Vector2? spray = null, float spreadDegrees = 360f, float size = 1f)
        {
            var effect = Play(root, Shape.Sparks, at, color, duration);
            if (effect == null) return;
            effect.size = size;
            effect.velocities = new Vector2[Mathf.Clamp(count, 1, 40)];
            float baseAngle = spray.HasValue && spray.Value.sqrMagnitude > 0.0001f ? Mathf.Atan2(spray.Value.y, spray.Value.x) : 0f;
            float spread = Mathf.Clamp(spreadDegrees, 0f, 360f) * Mathf.Deg2Rad;
            for (int i = 0; i < effect.velocities.Length; i++)
            {
                float angle = spray.HasValue ? baseAngle + Random.Range(-0.5f, 0.5f) * spread : Random.value * Mathf.PI * 2f;
                effect.velocities[i] = FlameMesh.Polar(angle, speed * Random.Range(0.55f, 1.15f));
            }
            effect.Redraw();
        }

        /// <summary>A thin ring that holds its size and fades.</summary>
        public static void Ring(Transform root, Vector2 at, float radius, Color color, float duration = 0.4f)
        {
            var effect = Play(root, Shape.Ring, at, color, duration);
            if (effect == null) return;
            effect.radius = Mathf.Max(0.1f, radius);
            effect.Redraw();
        }

        private static PixelBurstVfx Play(Transform root, Shape shape, Vector2 at, Color color, float duration)
        {
            var effect = Spawn<PixelBurstVfx>(root, duration);
            if (effect == null) return null;
            effect.Mesh.Pixelated = true;
            effect.shape = shape;
            effect.at = at;
            effect.color = color;
            return effect;
        }

        protected override void Draw(float t)
        {
            // Pixel art fades in a few hard steps rather than smoothly.
            float fade = Mathf.Ceil((1f - t) * FadeSteps) / FadeSteps;
            if (shape == Shape.Pulse) DrawPulse(t, fade);
            else if (shape == Shape.Sparks) DrawSparks(t, fade);
            else Mesh.Ring(at, radius, 0.1f, FlameMesh.Alpha(color, fade));
        }

        private void DrawPulse(float t, float fade)
        {
            float r = radius * Mathf.Lerp(0.35f, 1f, EaseOut(t / 0.6f));
            Mesh.Disc(at, r, FlameMesh.Alpha(color, 0.15f * fade), FlameMesh.Alpha(color, 0.45f * fade));
            Mesh.Ring(at, r, 0.2f, FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.5f), fade), FlameMesh.Alpha(color, 0.8f * fade));
        }

        private void DrawSparks(float t, float fade)
        {
            // They slow as they go: the distance covered eases out over the effect's life.
            float travel = Duration * (t - 0.5f * t * t), hot = 1f - t;
            Color head = FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.5f * hot), fade), tail = FlameMesh.Alpha(color, 0.6f * fade);
            foreach (var velocity in velocities)
            {
                Vector2 spark = at + velocity * travel, back = -velocity.normalized;
                float streak = velocity.magnitude * Duration * 0.25f * hot * size;
                if (streak > FlameMesh.Pixel) Mesh.Bar(spark, back, streak, FlameMesh.Pixel, tail, FlameMesh.Alpha(color, 0f));
                Mesh.Diamond(spark, 0.08f * size * (0.5f + 0.5f * hot), head);
            }
        }
    }
}
