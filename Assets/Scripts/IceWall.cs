using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Ice Wall: a wall of ice raised across the Wizard's aim for a few seconds, built from separate blocks of ice. Every
    /// block stops enemies, heroes and bolts alike, and each can be broken on its own: enemies chip a block by pushing
    /// against it, enemy bolts chip it, and heroes chip it with their swings and shots. A breaking block's shards freeze
    /// the enemies right beside it, and leave a gap in the wall. Every co-op machine raises its own copy (so the host's
    /// enemies are really held back); a block broken on one machine is broken on every machine.
    /// </summary>
    public sealed class IceWall : MonoBehaviour
    {
        public const float HalfLength = 2.2f, Thickness = 0.35f, Distance = 2f, Duration = 7f, ShatterReach = 1.2f, ShatterFreeze = 1.5f;
        public const float SegmentLength = 0.55f, BlockHeight = 0.75f, HeroRadius = 0.25f;
        /// <summary>Blows each block takes to break (ranks add more).</summary>
        public const int BaseHealth = 2;
        private const float RiseTime = 0.2f, MeltTime = 0.3f;
        private static readonly List<IceWall> walls = new List<IceWall>();
        private static readonly Color Deep = new Color(0.4f, 0.66f, 0.95f, 0.85f), Body = new Color(0.68f, 0.9f, 1f, 0.9f),
            Top = new Color(0.9f, 0.98f, 1f, 0.95f), Crack = new Color(0.2f, 0.4f, 0.75f, 0.9f), Shard = new Color(0.7f, 0.92f, 1f);

        private sealed class Block
        {
            public Vector2 Center;
            public int Health, MaxHealth;
            public bool Broken;
            public float FlashUntil, Height;
            public readonly Dictionary<DungeonEnemy, float> LastPush = new Dictionary<DungeonEnemy, float>();
        }

        private DungeonRun run;
        private Vector2 center, along, normal;
        private float age, duration;
        private bool melting;
        private readonly List<Block> blocks = new List<Block>();
        private FlameMesh mesh;

        public static void Raise(DungeonPlayer player, Vector2 aim, float duration, int health)
        {
            var run = player.Run;
            Vector2 center = PlayerAbilities.FindGroundLanding(run.Map, player.transform.position, aim, Distance);
            Vector2 along = Vector2.Perpendicular(aim.normalized);
            Create(run, center, along, HalfLength, duration, health);
            CoopFx.IceWall(run, center, along, HalfLength, duration, health);
        }

        public static void Create(DungeonRun run, Vector2 center, Vector2 along, float halfLength, float duration, int health)
        {
            var wall = new GameObject("Ice wall").AddComponent<IceWall>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            wall.transform.SetParent(run.ProjectileRoot, false);
            wall.run = run;
            wall.center = center;
            wall.along = along.normalized;
            wall.normal = Vector2.Perpendicular(wall.along);
            wall.duration = duration;
            wall.mesh = new FlameMesh(wall.gameObject, 5);
            int count = Mathf.Max(1, Mathf.RoundToInt(halfLength * 2f / SegmentLength));
            float span = halfLength * 2f / count;
            for (int i = 0; i < count; i++)
            {
                Vector2 at = center + wall.along * (-halfLength + span * (i + 0.5f));
                // Blocks on walls or out in the void are never raised.
                if (!run.Map.CanStand(at, 0.05f)) continue;
                wall.blocks.Add(new Block
                {
                    Center = at, Health = Mathf.Max(1, health), MaxHealth = Mathf.Max(1, health),
                    Height = BlockHeight * (0.85f + 0.3f * FlameMesh.Hash(i, center.x + center.y)),
                });
            }
            HeroVfx.Sparks(run.ProjectileRoot, center, Shard, 16, 4f, 0.4f, null, 360f, 1.2f);
        }

        private void OnEnable() => walls.Add(this);
        private void OnDisable() => walls.Remove(this);

        private const float HalfSpan = SegmentLength * 0.5f;

        /// <summary>The intact block that moving from <paramref name="from"/> to <paramref name="to"/> would cross or stand in, if any.</summary>
        private Block Crossing(Vector2 from, Vector2 to, float radius)
        {
            foreach (var block in blocks)
            {
                if (block.Broken || melting) continue;
                float sideways = Mathf.Abs(Vector2.Dot(to - block.Center, along));
                if (sideways > HalfSpan + radius) continue;
                float before = Vector2.Dot(from - block.Center, normal), after = Vector2.Dot(to - block.Center, normal);
                // Anyone a block rose on top of may still step out of it.
                bool inside = Mathf.Abs(before) < Thickness + radius && Mathf.Abs(Vector2.Dot(from - block.Center, along)) <= HalfSpan + radius;
                if (inside && Mathf.Abs(after) > Mathf.Abs(before) && before * after > 0f) continue;
                if (before * after <= 0f || Mathf.Abs(after) < Thickness + radius) return block;
            }
            return null;
        }

        /// <summary>An enemy's step is refused at the wall, and it chips the block roughly once a second while it pushes.</summary>
        public static bool BlocksEnemy(DungeonEnemy enemy, Vector2 from, Vector2 to)
        {
            foreach (var wall in walls)
            {
                var block = wall != null ? wall.Crossing(from, to, enemy.MoveRadius) : null;
                if (block == null) continue;
                if (!block.LastPush.TryGetValue(enemy, out float last) || Time.time - last >= 0.8f)
                {
                    block.LastPush[enemy] = Time.time;
                    wall.Chip(block, true);
                }
                return true;
            }
            return false;
        }

        /// <summary>A hero's step is refused at the wall; heroes break it by attacking it.</summary>
        public static bool BlocksHero(Vector2 from, Vector2 to)
        {
            foreach (var wall in walls)
                if (wall != null && wall.Crossing(from, to, HeroRadius) != null) return true;
            return false;
        }

        /// <summary>An enemy bolt shatters against the wall, chipping it.</summary>
        public static bool StopsBolt(Vector2 from, Vector2 to) => StopsShot(from, to, true);

        /// <summary>
        /// A projectile (an enemy's bolt or a hero's shot) is stopped by the wall. A real one chips the block; a
        /// teammate's ghost shot just stops, since their machine chips its own copy.
        /// </summary>
        public static bool StopsShot(Vector2 from, Vector2 to, bool chips)
        {
            foreach (var wall in walls)
            {
                var block = wall != null ? wall.Crossing(from, to, 0.05f) : null;
                if (block == null) continue;
                if (chips) wall.Chip(block, true);
                return true;
            }
            return false;
        }

        /// <summary>A hero's swing chips every block in reach ahead of them (or right beside them).</summary>
        public static void HitInArc(DungeonRun run, Vector2 origin, Vector2 facing, float reach)
        {
            foreach (var wall in walls.ToArray())
            {
                if (wall == null || wall.run != run) continue;
                foreach (var block in wall.blocks)
                {
                    if (block.Broken || wall.melting) continue;
                    Vector2 offset = block.Center - origin;
                    float distance = offset.magnitude;
                    if (distance > reach + HalfSpan) continue;
                    if (distance > 0.9f && Vector2.Dot(offset / distance, facing) < 0.35f) continue;
                    wall.Chip(block, true);
                }
            }
        }

        /// <summary>Another machine broke the block nearest <paramref name="point"/>; break it here too.</summary>
        public static void BreakAt(DungeonRun run, Vector2 point)
        {
            foreach (var wall in walls.ToArray())
            {
                if (wall == null || wall.run != run) continue;
                foreach (var block in wall.blocks)
                    if (!block.Broken && Vector2.Distance(block.Center, point) < 0.3f) { wall.Shatter(block, false); return; }
            }
        }

        private void Chip(Block block, bool local)
        {
            if (block.Broken || melting) return;
            block.Health--;
            block.FlashUntil = Time.time + 0.12f;
            HeroVfx.Sparks(run.ProjectileRoot, block.Center + Vector2.up * block.Height * 0.5f, Shard, 5, 2.5f, 0.25f, null, 360f, 0.7f);
            if (block.Health <= 0) Shatter(block, local);
        }

        /// <summary>A block breaks: shards burst out and freeze every enemy right beside it; the gap opens.</summary>
        private void Shatter(Block block, bool local)
        {
            block.Broken = true;
            Vector2 at = block.Center + Vector2.up * block.Height * 0.4f;
            HeroVfx.Sparks(run.ProjectileRoot, at, Shard, 16, 5f, 0.45f, null, 360f, 1.3f);
            HeroVfx.Sparks(run.ProjectileRoot, at, Color.white, 6, 3f, 0.3f, Vector2.up, 140f, 0.8f);
            HeroVfx.Pulse(run.ProjectileRoot, block.Center, ShatterReach, FlameMesh.Alpha(Shard, 0.5f), 0.3f);
            if (local)
            {
                CoopFx.IceBreak(run, block.Center);
                foreach (var enemy in run.Enemies.ToArray())
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(enemy.transform.position, block.Center) <= ShatterReach + enemy.HitRadius)
                        enemy.Freeze(ShatterFreeze);
            }
            if (blocks.TrueForAll(b => b.Broken)) Destroy(gameObject, 0.05f);
        }

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null) { Destroy(gameObject); return; }
            age += Time.deltaTime;
            if (age >= duration + MeltTime) { Destroy(gameObject); return; }
            // Once it starts to melt it no longer blocks anything.
            melting = age >= duration;
            Draw();
        }

        /// <summary>
        /// Each intact block as a little prism of ice seen from above and in front: its side faces (farthest first, so
        /// nearer ones cover them), a bright top, a highlight streak and cracks that spread as it is chipped.
        /// </summary>
        private void Draw()
        {
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / RiseTime));
            float melt = age >= duration ? 1f - (age - duration) / MeltTime : 1f;
            mesh.Begin();
            // Farther blocks (higher on screen) first.
            var order = new List<Block>(blocks);
            order.Sort((a, b) => b.Center.y.CompareTo(a.Center.y));
            foreach (var block in order)
            {
                if (block.Broken) continue;
                float alpha = melt;
                float height = block.Height * rise * (0.4f + 0.6f * melt);
                bool flash = Time.time < block.FlashUntil;
                Vector2 a = block.Center - along * HalfSpan * 0.94f, b = block.Center + along * HalfSpan * 0.94f, n = normal * Thickness;
                Vector2[] foot = { a - n, b - n, b + n, a + n };
                Vector2 up = Vector2.up * height;
                mesh.Ellipse(block.Center + Vector2.down * 0.05f, HalfSpan * 1.1f, 0.2f, new Color(0f, 0f, 0f, 0.25f * alpha), new Color(0f, 0f, 0f, 0f), 12);
                // Side faces, back to front.
                var sides = new List<int> { 0, 1, 2, 3 };
                sides.Sort((i, j) => ((foot[j] + foot[(j + 1) % 4]).y).CompareTo((foot[i] + foot[(i + 1) % 4]).y));
                foreach (int i in sides)
                {
                    Vector2 p = foot[i], q = foot[(i + 1) % 4];
                    // Faces turned toward the viewer (downward on screen) are lit; the rest are in shade.
                    Vector2 outward = (p + q) * 0.5f - block.Center;
                    Color face = outward.y < 0f ? Body : Deep;
                    if (flash) face = Color.Lerp(face, Color.white, 0.7f);
                    mesh.Quad(p, p + up, q + up, q, FlameMesh.Alpha(Deep, alpha), FlameMesh.Alpha(face, alpha), FlameMesh.Alpha(face, alpha), FlameMesh.Alpha(Deep, alpha));
                }
                Color top = flash ? Color.white : Top;
                mesh.Quad(foot[0] + up, foot[1] + up, foot[2] + up, foot[3] + up, FlameMesh.Alpha(top, alpha), FlameMesh.Alpha(top, alpha), FlameMesh.Alpha(top, alpha), FlameMesh.Alpha(top, alpha));
                // Edges of the top, and a highlight streak down the front.
                for (int i = 0; i < 4; i++) Line(foot[i] + up, foot[(i + 1) % 4] + up, 0.02f, FlameMesh.Alpha(Color.white, 0.8f * alpha));
                Vector2 front = Vector2.Lerp(foot[0], foot[1], 0.25f);
                Line(front + up * 0.15f, front + up * 0.85f, 0.035f, FlameMesh.Alpha(Color.white, 0.6f * alpha));
                // Cracks spread over the block as it's chipped.
                int cracks = Mathf.CeilToInt((1f - block.Health / (float)block.MaxHealth) * 4f);
                for (int c = 0; c < cracks; c++)
                {
                    float seed = FlameMesh.Hash(c, block.Center.x * 3.1f + block.Center.y);
                    Vector2 start = Vector2.Lerp(foot[0], foot[1], 0.2f + 0.6f * seed) + up * (0.2f + 0.5f * FlameMesh.Hash(c, 1.7f));
                    Vector2 mid = start + new Vector2((seed - 0.5f) * 0.2f, 0.12f), end = mid + new Vector2((0.5f - seed) * 0.15f, 0.1f);
                    Line(start, mid, 0.025f, FlameMesh.Alpha(Crack, alpha));
                    Line(mid, end, 0.02f, FlameMesh.Alpha(Crack, alpha));
                }
            }
            mesh.Commit();
        }

        private void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            float length = Vector2.Distance(from, to);
            if (length < 0.001f) return;
            mesh.Bar(from, (to - from) / length, length, width, color, color);
        }

        private void OnDestroy() => mesh?.Release();
    }
}
