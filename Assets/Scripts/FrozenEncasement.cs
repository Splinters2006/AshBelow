using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Frozen: the enemy is sealed inside a translucent block of ice, so a freeze never reads like the Demoness's pulsing
    /// violet paralysis. The block slams shut round the body, frost shards jut from its base and a glint runs over its
    /// face. As the thaw nears it cracks and shudders; when the freeze ends (or the enemy dies in it) it shatters.
    /// Purely visual; it follows <see cref="DungeonEnemy.IsFrozen"/> on every machine.
    /// </summary>
    public sealed class FrozenEncasement : EnemyStatusVisual
    {
        public static readonly Color Light = new Color(0.86f, 0.97f, 1f), Shade = new Color(0.36f, 0.7f, 0.95f),
            Deep = new Color(0.14f, 0.36f, 0.7f), Mist = new Color(0.9f, 0.97f, 1f);

        private const int Back = 0, Front = 1;
        private const float EncaseTime = 0.16f, ThawWarning = 0.6f, GlintPeriod = 1.8f;
        private static readonly int[] Orders = { 5, 8 };
        private static readonly Vector2[] GlintRays = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        protected override int[] LayerOrders => Orders;

        private readonly Vector2[] outline = new Vector2[8];
        private bool shown;
        private float shownAt, seed;
        private Vector2 lastCenter, lastHalf;

        private void LateUpdate()
        {
            bool frozen = Enemy.IsFrozen && Enemy.Health > 0;
            if (frozen && !shown) Encase();
            else if (!frozen && shown) Shatter();
            if (!shown) return;

            Vector2 half = BodySize * 0.62f, center = Body != null ? (Vector2)Body.bounds.center : (Vector2)transform.position;
            float grow = Mathf.Clamp01((Time.time - shownAt) / EncaseTime);
            grow = 1f - (1f - grow) * (1f - grow);
            // The co-op guest only hears "still frozen" in snapshots, so only the host knows when the thaw is close.
            bool guest = Enemy.Run != null && Enemy.Run.IsGuest;
            float thaw = guest ? 0f : 1f - Mathf.Clamp01(Enemy.FreezeRemaining / ThawWarning);
            if (thaw > 0f) center += new Vector2(Mathf.Sin(Time.time * 70f), Mathf.Sin(Time.time * 53f + 1f)) * 0.035f * thaw;
            half *= Mathf.Lerp(1.35f, 1f, grow);
            lastCenter = center;
            lastHalf = half;

            var back = Begin(Back);
            if (back != null)
            {
                // Cold mist pooling round the base.
                back.Ellipse(center + Vector2.down * half.y * 0.95f, half.x * 1.45f, half.y * 0.35f, FlameMesh.Alpha(Mist, 0.4f * grow), FlameMesh.Alpha(Mist, 0f));
                Commit(Back);
            }
            var front = Begin(Front);
            if (front != null)
            {
                DrawBlock(front, center, half, grow, thaw);
                Commit(Front);
            }
        }

        private void Encase()
        {
            shown = true;
            shownAt = Time.time;
            seed = Random.value * 100f;
            var root = Enemy.Run != null ? Enemy.Run.ProjectileRoot : null;
            if (root == null) return;
            Vector2 at = Body != null ? (Vector2)Body.bounds.center : (Vector2)transform.position;
            HeroVfx.Pulse(root, at, Mathf.Max(BodySize.x, BodySize.y) * 0.9f, Mist, 0.25f);
            HeroVfx.Sparks(root, at, Light, 10, 2.2f, 0.3f);
        }

        private void Shatter()
        {
            shown = false;
            Clear();
            var root = Enemy.Run != null ? Enemy.Run.ProjectileRoot : null;
            if (root != null && lastHalf != Vector2.zero) IceShatterVfx.Play(root, lastCenter, lastHalf);
        }

        private void DrawBlock(FlameMesh mesh, Vector2 c, Vector2 half, float alpha, float thaw)
        {
            float hx = half.x, hy = half.y, k = Mathf.Min(hx, hy) * 0.32f;
            // A chamfered, slightly lopsided block, clockwise from the top left.
            outline[0] = new Vector2(-hx + k, hy);
            outline[1] = new Vector2(hx - k * 0.6f, hy);
            outline[2] = new Vector2(hx, hy - k * 0.6f);
            outline[3] = new Vector2(hx, -hy + k);
            outline[4] = new Vector2(hx - k, -hy);
            outline[5] = new Vector2(-hx + k * 0.7f, -hy);
            outline[6] = new Vector2(-hx, -hy + k * 0.7f);
            outline[7] = new Vector2(-hx, hy - k);
            for (int i = 0; i < outline.Length; i++)
                outline[i] = c + outline[i] + new Vector2(FlameMesh.Hash(seed + i, 1.3f) - 0.5f, FlameMesh.Hash(seed + i, 2.9f) - 0.5f) * k * 0.3f;

            // The ice itself: see-through in the middle, frostier towards the edges, lit from the top left.
            Vector2 light = new Vector2(-0.6f, 0.8f);
            Color middle = FlameMesh.Alpha(Color.Lerp(Shade, Light, 0.45f), 0.3f * alpha);
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                mesh.Triangle(c, a, b, middle, EdgeColor(a - c, light, 0.62f * alpha), EdgeColor(b - c, light, 0.62f * alpha));
            }
            // An inner facet, offset towards the light, gives the block depth.
            Vector2 facet = c + new Vector2(-hx, hy) * 0.12f;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = facet + (outline[i] - c) * 0.62f, b = facet + (outline[(i + 1) % outline.Length] - c) * 0.62f;
                mesh.Triangle(facet, a, b, FlameMesh.Alpha(Color.white, 0.14f * alpha), FlameMesh.Alpha(Light, 0.04f * alpha), FlameMesh.Alpha(Light, 0.04f * alpha));
            }

            // Hard edges: bright where the light catches, deep blue in shadow.
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length], edge = b - a;
                float lit = Vector2.Dot(Vector2.Perpendicular(edge).normalized * -1f, light);
                Color color = FlameMesh.Alpha(Color.Lerp(Deep, Color.white, Mathf.Clamp01(0.5f + lit * 0.7f)), 0.95f * alpha);
                mesh.Bar(a, edge.normalized, edge.magnitude, 0.05f, color, color);
            }

            // Highlight streaks down the lit face.
            Vector2 streak = new Vector2(0.42f, -0.9f).normalized;
            mesh.Bar(c + new Vector2(-hx * 0.62f, hy * 0.72f), streak, hy * 0.9f, 0.07f, FlameMesh.Alpha(Color.white, 0.75f * alpha), FlameMesh.Alpha(Color.white, 0f));
            mesh.Bar(c + new Vector2(-hx * 0.36f, hy * 0.78f), streak, hy * 0.45f, 0.035f, FlameMesh.Alpha(Color.white, 0.6f * alpha), FlameMesh.Alpha(Color.white, 0f));

            // Cracks: a hairline or two from the start, splitting wide open as the thaw nears.
            int cracks = 2 + Mathf.RoundToInt(3f * thaw);
            for (int i = 0; i < cracks; i++)
            {
                float s = FlameMesh.Hash(seed * 0.7f + i, 6.1f);
                Vector2 from = Vector2.Lerp(outline[(i * 3) % outline.Length], outline[(i * 3 + 1) % outline.Length], 0.3f + 0.4f * s);
                Vector2 toward = (c - from).normalized;
                float reach = Mathf.Min(hx, hy) * (0.35f + 0.25f * s + 0.4f * thaw);
                for (int j = 0; j < 3; j++)
                {
                    float bend = (FlameMesh.Hash(seed + i * 3.1f, j + 0.5f) - 0.5f) * 1.4f;
                    Vector2 dir = (toward + Vector2.Perpendicular(toward) * bend).normalized;
                    float len = reach / 3f;
                    mesh.Bar(from, dir, len, 0.03f + 0.02f * thaw, FlameMesh.Alpha(Color.white, (0.45f + 0.4f * thaw) * alpha), FlameMesh.Alpha(Color.white, (0.3f + 0.4f * thaw) * alpha));
                    from += dir * len;
                }
            }

            // Frost shards jut from the base and the corners.
            for (int i = 0; i < 5; i++)
            {
                float s = FlameMesh.Hash(seed + i, 8.3f), u = (i + 0.5f) / 5f;
                Vector2 root = c + new Vector2(Mathf.Lerp(-hx * 1.05f, hx * 1.05f, u), -hy * 0.95f);
                Vector2 up = new Vector2((u - 0.5f) * 1.4f, 1f).normalized;
                float height = Mathf.Min(hx, hy) * (0.35f + 0.3f * s) * alpha;
                mesh.Crystal(root, up, height * 0.55f, height, seed + i, Color.white, Light, Shade, Deep, alpha);
            }
            mesh.Crystal(outline[1], new Vector2(0.5f, 1f).normalized, k * 0.6f, k * 1.1f * alpha, seed + 9f, Color.white, Light, Shade, Deep, alpha);
            mesh.Crystal(outline[7], new Vector2(-1f, 0.4f).normalized, k * 0.5f, k * 0.9f * alpha, seed + 11f, Color.white, Light, Shade, Deep, alpha);

            // A glint sweeps across the face now and then.
            float sweep = Mathf.Repeat((Time.time + seed) / GlintPeriod, 1f) * 2.2f;
            if (sweep <= 1f)
            {
                Vector2 at = c + Vector2.Lerp(new Vector2(-hx * 0.7f, hy * 0.75f), new Vector2(hx * 0.6f, -hy * 0.3f), sweep);
                float size = Mathf.Min(hx, hy) * 0.38f * Mathf.Sin(sweep * Mathf.PI);
                Color glint = FlameMesh.Alpha(Color.white, alpha);
                foreach (var dir in GlintRays)
                    mesh.Bar(at, dir, size, size * 0.22f, glint, FlameMesh.Alpha(Color.white, 0f));
            }
        }

        private static Color EdgeColor(Vector2 outward, Vector2 light, float alpha)
        {
            float lit = Mathf.Clamp01(0.5f + 0.5f * Vector2.Dot(outward.normalized, light));
            return FlameMesh.Alpha(Color.Lerp(Shade, Light, lit), alpha);
        }
    }
}
