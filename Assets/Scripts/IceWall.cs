using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Ice Wall: a wall of ice raised across the Wizard's aim for a few seconds. It stops enemies and their bolts; every
    /// blow it takes chips it, and when it breaks the shards freeze the enemies right beside it. Every co-op machine raises
    /// its own copy, so the host's enemies are held back wherever it was cast.
    /// </summary>
    public sealed class IceWall : MonoBehaviour
    {
        public const float HalfLength = 2.2f, Thickness = 0.35f, Distance = 2f, Duration = 4f, ShatterReach = 1.2f, ShatterFreeze = 1.5f;
        public const int BaseHealth = 6;
        private static readonly List<IceWall> walls = new List<IceWall>();
        private static readonly Color Ice = new Color(0.7f, 0.92f, 1f);
        private DungeonRun run;
        private Vector2 center, along, normal;
        private float halfLength, until;
        private int health;
        private readonly Dictionary<DungeonEnemy, float> lastPushAt = new Dictionary<DungeonEnemy, float>();

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
            var root = new GameObject("Ice wall");
            root.transform.SetParent(run.ProjectileRoot, false);
            root.transform.position = center;
            var wall = root.AddComponent<IceWall>();
            wall.run = run;
            wall.center = center;
            wall.along = along.normalized;
            wall.normal = Vector2.Perpendicular(wall.along);
            wall.halfLength = halfLength;
            wall.until = Time.time + duration;
            wall.health = health;
            for (float d = -halfLength; d <= halfLength + 0.01f; d += 0.45f)
            {
                var block = DungeonVisuals.Create("Ice block", root.transform, center + wall.along * d, new Vector2(0.5f, 0.7f), Ice, 5);
                block.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(wall.along.y, wall.along.x) * Mathf.Rad2Deg);
            }
            HeroVfx.Sparks(run.ProjectileRoot, center, Ice, 16, 4f, 0.4f, null, 360f, 1.2f);
        }

        private void OnEnable() => walls.Add(this);
        private void OnDisable() => walls.Remove(this);

        /// <summary>True when moving from <paramref name="from"/> to <paramref name="to"/> would cross (or stand in) a wall.</summary>
        private bool Crosses(Vector2 from, Vector2 to, float radius)
        {
            float along = Mathf.Abs(Vector2.Dot(to - center, this.along));
            if (along > halfLength + radius) return false;
            float before = Vector2.Dot(from - center, normal), after = Vector2.Dot(to - center, normal);
            return before * after <= 0f || Mathf.Abs(after) < Thickness + radius;
        }

        /// <summary>An enemy's step is refused at the wall, which it chips roughly once a second while it pushes.</summary>
        public static bool BlocksEnemy(DungeonEnemy enemy, Vector2 from, Vector2 to)
        {
            foreach (var wall in walls)
            {
                if (wall == null || !wall.Crosses(from, to, enemy.MoveRadius)) continue;
                if (!wall.lastPushAt.TryGetValue(enemy, out float last) || Time.time - last >= 0.8f)
                {
                    wall.lastPushAt[enemy] = Time.time;
                    wall.Chip();
                }
                return true;
            }
            return false;
        }

        /// <summary>An enemy bolt shatters against the wall, chipping it.</summary>
        public static bool StopsBolt(Vector2 from, Vector2 to)
        {
            foreach (var wall in walls)
            {
                if (wall == null || !wall.Crosses(from, to, 0.05f)) continue;
                wall.Chip();
                return true;
            }
            return false;
        }

        private void Chip()
        {
            if (health <= 0) return;
            health--;
            HeroVfx.Sparks(run.ProjectileRoot, center, Ice, 5, 2.5f, 0.25f);
            if (health <= 0) Shatter();
        }

        /// <summary>Broken by the enemies: the shards freeze everything right beside the wall.</summary>
        private void Shatter()
        {
            HeroVfx.Sparks(run.ProjectileRoot, center, Ice, 24, 6f, 0.5f, null, 360f, 1.5f);
            CombatVfx.Ring(run.ProjectileRoot, center, halfLength, Ice, 0.4f);
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0) continue;
                Vector2 offset = (Vector2)enemy.transform.position - center;
                float along = Mathf.Clamp(Vector2.Dot(offset, this.along), -halfLength, halfLength);
                if (Vector2.Distance(enemy.transform.position, center + this.along * along) <= ShatterReach + enemy.HitRadius) enemy.Freeze(ShatterFreeze);
            }
            Destroy(gameObject);
        }

        private void Update()
        {
            if (run == null || Time.time >= until) Destroy(gameObject);
        }
    }
}
