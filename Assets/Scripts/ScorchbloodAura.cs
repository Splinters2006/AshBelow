using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Scorchblood's lasting look, while the enemy both burns and bleeds: it stands in a pool of burning blood, wreathed
    /// in dark blood-red flame that throbs with a heartbeat. Every beat sends a crimson shock ring out of the body, and
    /// burning blood and black smoke boil off it. Drawn as pixel art, like the sprites. Purely visual; it follows
    /// <see cref="DungeonEnemy.IsScorchblooded"/> on every machine.
    /// </summary>
    public sealed class ScorchbloodAura : EnemyStatusVisual
    {
        private const int Back = 0, Front = 1, Tongues = 7, Embers = 7, Smoke = 5;
        private const float BeatPeriod = 0.85f;
        private static readonly int[] Orders = { 5, 8 };
        protected override int[] LayerOrders => Orders;
        protected override bool Pixelated => true;

        private bool shown;
        private float shownAt, seed;

        private void LateUpdate()
        {
            bool scorched = Enemy.IsScorchblooded;
            if (scorched && !shown) { shown = true; shownAt = Time.time; seed = Random.value * 100f; }
            else if (!scorched && shown) { shown = false; Clear(); }
            if (!shown) return;

            Vector2 size = BodySize, center = Body != null ? (Vector2)Body.bounds.center : (Vector2)transform.position;
            float r = Mathf.Max(size.x, size.y) * 0.5f, grow = Mathf.Clamp01((Time.time - shownAt) / 0.25f);
            float phase = Mathf.Repeat(Time.time - shownAt, BeatPeriod) / BeatPeriod;
            // Lub-dub: a hard beat and a softer echo.
            float beat = Mathf.Max(Bump(phase, 0.02f), 0.6f * Bump(phase, 0.2f));
            Vector2 feet = center + Vector2.down * r * 0.85f;

            var back = Begin(Back);
            if (back != null)
            {
                DrawBack(back, center, feet, r, beat, phase, grow);
                Commit(Back);
            }
            var front = Begin(Front);
            if (front != null)
            {
                DrawFront(front, center, feet, r, beat, grow);
                Commit(Front);
            }
        }

        private void DrawBack(FlameMesh mesh, Vector2 center, Vector2 feet, float r, float beat, float phase, float grow)
        {
            Color blood = DungeonEnemy.BleedColor, dark = FlameMesh.Ember;

            // A pool of burning blood, black at the heart and glowing at the rim.
            float pool = r * (1.15f + 0.1f * beat) * grow;
            mesh.Ellipse(feet, pool * 1.05f, pool * 0.42f, FlameMesh.Alpha(blood, 0.35f + 0.2f * beat), FlameMesh.Alpha(dark, 0f));
            mesh.Ellipse(feet, pool * 0.8f, pool * 0.3f, FlameMesh.Alpha(Color.black, 0.65f), FlameMesh.Alpha(dark, 0.5f));

            // A red-black glow throbs behind the body.
            mesh.Disc(center, r * (1.2f + 0.2f * beat) * grow, FlameMesh.Alpha(blood, 0.3f + 0.2f * beat), FlameMesh.Alpha(dark, 0f), 32);

            // Each beat drives a crimson shock ring out of the body.
            if (phase < 0.55f)
            {
                float u = phase / 0.55f;
                mesh.Ring(center, r * (0.9f + 0.8f * EaseOut(u)), 0.08f * (1f - u), FlameMesh.Alpha(blood, 0.65f * (1f - u)), FlameMesh.Alpha(blood, 0f), 40);
            }

            // Tall, dark blood-fire wreathes the body from behind.
            for (int i = 0; i < Tongues; i++)
            {
                float s = FlameMesh.Hash(seed + i, 2.1f), u = (i + 0.5f) / Tongues;
                Vector2 root = feet + new Vector2(Mathf.Lerp(-r * 0.8f, r * 0.8f, u), (s - 0.5f) * r * 0.2f);
                Vector2 up = new Vector2((u - 0.5f) * 0.4f, 1f).normalized;
                float edge = 1f - Mathf.Abs(u - 0.5f) * 0.9f;
                BloodTongue(mesh, root, up, r * (0.4f + 0.15f * s), r * (1.5f + 0.7f * s) * edge * (1f + 0.25f * beat) * grow, seed + i * 1.7f);
            }
        }

        private void DrawFront(FlameMesh mesh, Vector2 center, Vector2 feet, float r, float beat, float grow)
        {
            Color blood = DungeonEnemy.BleedColor, fire = DungeonEnemy.ScorchColor;

            // Hot flames lick round the feet without hiding the body.
            for (int i = 0; i < 3; i++)
            {
                float u = i / 2f, s = FlameMesh.Hash(seed + i, 7.7f);
                Vector2 root = feet + new Vector2(Mathf.Lerp(-r * 0.6f, r * 0.6f, u), -r * 0.05f);
                BloodTongue(mesh, root, Vector2.up, r * (0.28f + 0.08f * s), r * (0.5f + 0.25f * beat) * grow, seed + i * 3.3f);
            }

            // Burning blood boils up off the body and chars as it rises.
            float time = Time.time;
            for (int i = 0; i < Embers; i++)
            {
                float s = FlameMesh.Hash(seed + i, 4.4f), rise = Mathf.Repeat(time * (0.7f + 0.5f * s) + s, 1f);
                Vector2 at = center + new Vector2((s - 0.5f) * r * 1.3f + Mathf.Sin(time * 4f + i) * r * 0.15f, -r * 0.4f + rise * r * 2.4f);
                Color color = rise < 0.4f ? Color.Lerp(fire, blood, rise / 0.4f * 0.5f) : Color.Lerp(Color.Lerp(fire, blood, 0.5f), FlameMesh.Ember, (rise - 0.4f) / 0.6f);
                mesh.Diamond(at, (0.05f + 0.03f * s) * (1f - 0.5f * rise), FlameMesh.Alpha(color, 0.85f * (1f - rise) * grow));
            }

            // Black smoke curls off the top.
            for (int i = 0; i < Smoke; i++)
            {
                float s = FlameMesh.Hash(seed + i, 9.2f), rise = Mathf.Repeat(time * (0.35f + 0.2f * s) + s, 1f);
                Vector2 at = center + new Vector2((s - 0.5f) * r * 0.7f + Mathf.Sin(time * 2f + i * 1.9f) * r * 0.2f, r * (0.6f + rise * 1.8f));
                mesh.Disc(at, r * (0.15f + 0.35f * rise), FlameMesh.Alpha(new Color(0.08f, 0.02f, 0.02f), 0.45f * (1f - rise) * grow), FlameMesh.Alpha(Color.black, 0f), 12);
            }
        }

        /// <summary>A flickering tongue of dark blood-fire: near-black at the edge, crimson in the body, a red-hot core.</summary>
        private static void BloodTongue(FlameMesh mesh, Vector2 root, Vector2 up, float width, float height, float seed)
            => mesh.Tongue(root, up, width, height, seed, FlameMesh.Alpha(FlameMesh.Ember, 0.85f), FlameMesh.Alpha(FlameMesh.Crimson, 0.95f),
                FlameMesh.Alpha(DungeonEnemy.ScorchColor, 0.95f));

        private static float Bump(float x, float at) => Mathf.Clamp01(1f - Mathf.Abs(x - at) / 0.09f);
        private static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
    }
}
