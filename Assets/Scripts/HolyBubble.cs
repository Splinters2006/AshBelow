using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A shimmering bubble. Aegis wraps the Knight in one that follows them; Sanctuary surrounds the Paladin with a
    /// bubble of holy light that moves with them and destroys every projectile, friend or foe, that is inside it or
    /// crosses its edge. It shoves enemies out as it forms and slows any that come back in.
    /// Projectiles ask <see cref="Blocks"/> each step; enemies ask <see cref="SlowsAt"/>.
    /// </summary>
    public sealed class HolyBubble : MonoBehaviour
    {
        public static readonly Color Holy = new Color(1f, 0.88f, 0.5f);
        /// <summary>Enemies inside a Sanctuary move and act at this fraction of their speed.</summary>
        public const float SanctuarySlow = 0.5f;
        private static readonly List<HolyBubble> blockers = new List<HolyBubble>();
        private FlameMesh mesh;
        private Transform follow;
        private Vector2 center;
        private float radius, duration, age, rippleAt = -1f, rippleAngle;
        private Color color;
        public bool BlocksProjectiles { get; private set; }
        public Vector2 Center => follow != null ? (Vector2)follow.position : center;
        public float Radius => radius;
        public bool IsActive => age < duration;

        /// <summary>A bubble that follows a hero for the given time (Aegis). Cosmetic only.</summary>
        public static HolyBubble Wrap(Transform root, Transform hero, float duration, Color color)
            => Create(root, hero, hero.position, 0.85f, duration, color, false);

        /// <summary>
        /// A bubble that destroys projectiles in or crossing it (Sanctuary). It follows the Paladin when one is given.
        /// Every machine plants one; only the host (or a solo run) shoves the enemies out.
        /// </summary>
        public static HolyBubble Sanctuary(DungeonRun run, Vector2 center, float radius, float duration, Transform follow = null)
        {
            var bubble = Create(run.ProjectileRoot, follow, center, radius, duration, Holy, true);
            if (bubble != null && !run.IsGuest) bubble.PushOut(run);
            return bubble;
        }

        /// <summary>Drops the Sanctuary following <paramref name="hero"/> early, with its usual fade.</summary>
        public static void EndFollowing(Transform hero)
        {
            foreach (var bubble in blockers)
                if (bubble != null && bubble.follow == hero && bubble.IsActive) bubble.duration = bubble.age;
        }

        private void PushOut(DungeonRun run)
        {
            Vector2 c = Center;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || enemy.IsInvulnerable) continue;
                Vector2 position = enemy.transform.position, offset = position - c;
                float distance = offset.magnitude, clear = radius + enemy.MoveRadius + 0.15f;
                if (distance >= clear) continue;
                Vector2 away = distance > 0.01f ? offset / distance : Random.insideUnitCircle.normalized;
                enemy.transform.position = run.Map.Move(position, away * (clear - distance), enemy.MoveRadius);
                HeroVfx.Sparks(transform.parent, enemy.transform.position, color, 6, 3f, 0.3f, away, 70f);
            }
        }

        private static HolyBubble Create(Transform root, Transform follow, Vector2 center, float radius, float duration, Color color, bool blocks)
        {
            if (root == null) return null;
            var bubble = new GameObject(blocks ? "Sanctuary bubble" : "Aegis bubble").AddComponent<HolyBubble>();
            bubble.transform.SetParent(root, false);
            bubble.follow = follow;
            bubble.center = center;
            bubble.radius = radius;
            bubble.duration = Mathf.Max(0.05f, duration);
            bubble.color = color;
            bubble.BlocksProjectiles = blocks;
            bubble.mesh = new FlameMesh(bubble.gameObject, blocks ? 7 : 8);
            if (blocks) blockers.Add(bubble);
            HeroVfx.Pulse(root, center, radius * 1.15f, color, 0.35f);
            return bubble;
        }

        public bool Contains(Vector2 point) => Vector2.Distance(point, Center) < radius;

        /// <summary>
        /// True when a projectile stepping from <paramref name="from"/> to <paramref name="to"/> touches a Sanctuary:
        /// crossing its edge either way, or flying anywhere inside (even one loosed from within).
        /// </summary>
        public static bool Blocks(Vector2 from, Vector2 to)
        {
            for (int i = blockers.Count - 1; i >= 0; i--)
            {
                var bubble = blockers[i];
                if (bubble == null || !bubble.IsActive) continue;
                bool fromInside = bubble.Contains(from), toInside = bubble.Contains(to);
                if (!fromInside && !toInside) continue;
                if (fromInside != toInside) bubble.Ripple(to);
                else HeroVfx.Sparks(bubble.transform.parent, to, bubble.color, 5, 2f, 0.22f);
                return true;
            }
            return false;
        }

        /// <summary>True when <paramref name="point"/> lies inside an active Sanctuary, which slows enemies there.</summary>
        public static bool SlowsAt(Vector2 point)
        {
            for (int i = blockers.Count - 1; i >= 0; i--)
                if (blockers[i] != null && blockers[i].IsActive && blockers[i].Contains(point)) return true;
            return false;
        }

        private void Ripple(Vector2 point)
        {
            Vector2 offset = point - Center;
            rippleAngle = Mathf.Atan2(offset.y, offset.x);
            rippleAt = age;
            HeroVfx.Sparks(transform.parent, point, color, 6, 2.6f, 0.25f, -offset, 120f);
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= duration + 0.3f || (follow == null && !BlocksProjectiles && age > 0.1f)) { Destroy(gameObject); return; }
            Draw();
        }

        private void Draw()
        {
            float fade = age < duration ? Mathf.Clamp01(age / 0.15f) : Mathf.Clamp01(1f - (age - duration) / 0.3f);
            // It flickers in its final second so everyone can tell it is about to drop.
            if (age < duration && duration - age < 1f) fade *= 0.65f + 0.35f * Mathf.Sin(age * 28f);
            float wobble = 1f + 0.025f * Mathf.Sin(age * 5f);
            Vector2 c = Center;
            float r = radius * wobble;
            mesh.Begin();
            mesh.Disc(c, r, FlameMesh.Alpha(color, 0.08f * fade), FlameMesh.Alpha(color, 0.22f * fade), 56);
            mesh.Ring(c, r, 0.07f, FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.5f), 0.9f * fade), 64);
            mesh.Ring(c, r * 0.94f, 0.04f, FlameMesh.Alpha(color, 0.35f * fade), 64);
            // A sheen slides across the top-left, like light on a soap bubble.
            for (int i = 0; i < 10; i++)
            {
                float a = 1.9f + i * 0.09f + Mathf.Sin(age * 1.3f) * 0.2f;
                mesh.Bar(c + FlameMesh.Polar(a, r * 0.8f), FlameMesh.Polar(a + Mathf.PI * 0.5f, 1f), r * 0.1f, 0.05f,
                    FlameMesh.Alpha(Color.white, 0.5f * fade * (1f - Mathf.Abs(i - 4.5f) / 5f)), FlameMesh.Alpha(Color.white, 0f));
            }
            if (BlocksProjectiles)
            {
                // Slow motes of holy light drift inside the sanctuary.
                for (int i = 0; i < 18; i++)
                {
                    float seed = FlameMesh.Hash(i, 3.3f), rise = Mathf.Repeat(age * (0.2f + seed * 0.25f) + seed, 1f);
                    Vector2 p = c + new Vector2((FlameMesh.Hash(i, 8.1f) - 0.5f) * r * 1.5f, (rise - 0.5f) * r * 1.5f);
                    if (Vector2.Distance(p, c) < r * 0.92f) mesh.Diamond(p, 0.07f, FlameMesh.Alpha(color, fade * Mathf.Sin(rise * Mathf.PI)));
                }
            }
            if (rippleAt >= 0f && age - rippleAt < 0.35f)
            {
                float t = (age - rippleAt) / 0.35f;
                for (int i = -4; i <= 4; i++)
                {
                    float a = rippleAngle + i * 0.06f * (1f + t * 2f);
                    mesh.Bar(c + FlameMesh.Polar(a, r - 0.08f), FlameMesh.Polar(a, 1f), 0.16f, 0.09f,
                        FlameMesh.Alpha(Color.white, (1f - t) * (1f - Mathf.Abs(i) / 5f)), FlameMesh.Alpha(color, 0f));
                }
            }
            mesh.Commit();
        }

        private void OnDestroy()
        {
            blockers.Remove(this);
            mesh?.Release();
        }
    }
}
