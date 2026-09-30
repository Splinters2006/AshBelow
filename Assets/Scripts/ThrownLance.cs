using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Holy Lance: the Paladin hurls a lance of light. It is drawn as a real jousting lance (a long tapering cone of
    /// gold-white light from a round vamplate guard to a blazing point, with a grip behind the guard) wrapped in a
    /// holy glow, and flies fast along a wide line, piercing every enemy it passes with holy damage. The first enemy it
    /// meets is stunned. It stops at a wall or the end of its reach in a burst of light. Teammates see the same lance.
    /// </summary>
    public sealed class ThrownLance : MonoBehaviour
    {
        public const float Speed = 24f, Range = 10f, Width = 0.6f, Length = 2.6f;
        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f), Pale = new Color(1f, 0.96f, 0.8f);
        private DungeonRun run;
        private DungeonPlayer player;
        private Vector2 origin, direction, position;
        private float travelled, range, age, endedAt = -1f;
        private int damage;
        private bool ghost, stunned;
        private readonly HashSet<DungeonEnemy> struck = new HashSet<DungeonEnemy>();
        private FlameMesh mesh;

        public static void Throw(DungeonPlayer player, Vector2 aim, int damage)
        {
            var lance = Create(player.Run, player.transform.position, aim);
            lance.player = player;
            lance.damage = damage;
            CoopFx.Lance(player.Run, player.transform.position, aim);
        }

        public static void SpawnGhost(DungeonRun run, Vector2 origin, Vector2 aim) => Create(run, origin, aim).ghost = true;

        private static ThrownLance Create(DungeonRun run, Vector2 origin, Vector2 aim)
        {
            var lance = new GameObject("Holy lance").AddComponent<ThrownLance>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            lance.transform.SetParent(run.ProjectileRoot, false);
            lance.run = run;
            lance.direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            lance.origin = lance.position = origin;
            lance.range = Vector2.Distance(origin, PlayerAbilities.FindGroundLanding(run.Map, origin, lance.direction, Range));
            lance.mesh = new FlameMesh(lance.gameObject, 8);
            HeroVfx.Pulse(run.ProjectileRoot, origin + lance.direction * 0.4f, 0.8f, FlameMesh.Alpha(Pale, 0.8f), 0.2f);
            HeroVfx.Sparks(run.ProjectileRoot, origin, Gold, 10, 4f, 0.25f, lance.direction, 50f, 0.9f);
            return lance;
        }

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null || (!ghost && player == null)) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            age += Time.deltaTime;
            if (endedAt >= 0f)
            {
                if (Time.time - endedAt > 0.25f) { Destroy(gameObject); return; }
                Draw();
                return;
            }
            float step = Mathf.Min(Speed * Time.deltaTime, range - travelled);
            travelled += step;
            position = origin + direction * travelled;
            Pierce();
            if (travelled >= range - 0.001f) End();
            Draw();
        }

        /// <summary>Every enemy within the lance's wide line up to its point is struck once; the first is stunned.</summary>
        private void Pierce()
        {
            var hit = new List<DungeonEnemy>();
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || struck.Contains(enemy)) continue;
                Vector2 offset = (Vector2)enemy.transform.position - origin;
                float along = Vector2.Dot(offset, direction);
                if (along < 0f || along > travelled + enemy.HitRadius || Mathf.Abs(Vector2.Dot(offset, Vector2.Perpendicular(direction))) > Width + enemy.HitRadius) continue;
                hit.Add(enemy);
            }
            hit.Sort((a, b) => Vector2.Dot((Vector2)a.transform.position - origin, direction).CompareTo(Vector2.Dot((Vector2)b.transform.position - origin, direction)));
            foreach (var enemy in hit)
            {
                struck.Add(enemy);
                HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, Pale, 8, 4f, 0.25f, direction, 70f, 0.9f);
                HeroVfx.Pulse(run.ProjectileRoot, enemy.transform.position, 0.6f, FlameMesh.Alpha(Gold, 0.7f), 0.15f);
                if (ghost) continue;
                CombatDamage.Apply(player, enemy, damage, DamageElement.Holy, origin, 0.6f);
                if (!stunned && enemy != null && enemy.Health > 0) { enemy.Stun(1f); stunned = true; }
            }
        }

        private void End()
        {
            endedAt = Time.time;
            HeroVfx.Pulse(run.ProjectileRoot, position, 0.9f, FlameMesh.Alpha(Pale, 0.8f), 0.25f);
            HeroVfx.Sparks(run.ProjectileRoot, position, Gold, 12, 4f, 0.3f, -direction, 160f, 1f);
        }

        private void Draw()
        {
            float fade = endedAt >= 0f ? 1f - (Time.time - endedAt) / 0.25f : 1f;
            Vector2 side = Vector2.Perpendicular(direction);
            Vector2 point = position, guard = point - direction * Length * 0.72f, butt = point - direction * Length;
            float shimmer = 1f + 0.1f * Mathf.Sin(age * 40f);
            mesh.Begin();
            // The streak of light left along its path, as wide as what it hits.
            Vector2 tail = origin + direction * Mathf.Max(0f, travelled - 4f);
            mesh.Quad(tail - side * Width * 0.2f, tail + side * Width * 0.2f, point + side * Width, point - side * Width,
                FlameMesh.Alpha(Gold, 0f), FlameMesh.Alpha(Gold, 0f), FlameMesh.Alpha(Gold, 0.3f * fade), FlameMesh.Alpha(Gold, 0.3f * fade));
            // A holy glow around the whole lance.
            Oval(Vector2.Lerp(butt, point, 0.55f), side, Length * 0.65f, Width * 0.9f * shimmer, FlameMesh.Alpha(Pale, 0.35f * fade), FlameMesh.Alpha(Gold, 0f), 24);
            // Grip and pommel behind the guard.
            mesh.Bar(butt, direction, Length * 0.28f, 0.1f, FlameMesh.Alpha(new Color(0.55f, 0.4f, 0.2f), fade), FlameMesh.Alpha(new Color(0.75f, 0.55f, 0.25f), fade));
            mesh.Disc(butt, 0.07f, FlameMesh.Alpha(Gold, fade), FlameMesh.Alpha(Gold, fade), 10);
            // The lance: a long tapering cone from the guard to the point, gold at the edges and white down its spine.
            mesh.Quad(guard - side * 0.2f, guard + side * 0.2f, point, point, FlameMesh.Alpha(Gold, fade), FlameMesh.Alpha(Gold, fade), FlameMesh.Alpha(Pale, fade), FlameMesh.Alpha(Pale, fade));
            mesh.Quad(guard - side * 0.07f, guard + side * 0.07f, point, point, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, fade));
            // Spiral bands wrapped round the cone.
            for (int i = 1; i <= 3; i++)
            {
                float u = i / 4f, half = 0.2f * (1f - u);
                Vector2 c = Vector2.Lerp(guard, point, u);
                mesh.Bar(c - side * half - direction * 0.04f, (side * half * 2f + direction * 0.08f).normalized, (side * half * 2f + direction * 0.08f).magnitude, 0.03f,
                    FlameMesh.Alpha(new Color(0.85f, 0.6f, 0.2f), fade), FlameMesh.Alpha(new Color(0.85f, 0.6f, 0.2f), fade));
            }
            // The vamplate: a round guard seen edge-on, as a flat ellipse across the shaft.
            Oval(guard, side, 0.07f, 0.3f, FlameMesh.Alpha(Gold, fade), FlameMesh.Alpha(new Color(0.8f, 0.6f, 0.2f), fade), 16);
            // A blazing point.
            mesh.Disc(point, 0.2f * shimmer, FlameMesh.Alpha(Color.white, 0.9f * fade), FlameMesh.Alpha(Pale, 0f), 16);
            mesh.Commit();
        }

        /// <summary>An ellipse whose <paramref name="along"/> radius lies on the lance's heading and <paramref name="across"/> radius across it.</summary>
        private void Oval(Vector2 center, Vector2 side, float along, float across, Color inner, Color outer, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 p0 = center + direction * Mathf.Cos(a0) * along + side * Mathf.Sin(a0) * across;
                Vector2 p1 = center + direction * Mathf.Cos(a1) * along + side * Mathf.Sin(a1) * across;
                mesh.Triangle(center, p0, p1, inner, outer, outer);
            }
        }

        private void OnDestroy() => mesh?.Release();
    }
}
