using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Ricochet Arrow: an arrow that pierces every enemy in its path and bounces off walls up to three times, hitting
    /// harder after each bounce.
    /// </summary>
    public sealed class RicochetArrow : MonoBehaviour
    {
        public const float Speed = 14f, Range = 22f;
        public const int MaxBounces = 3;
        private static readonly Color Tint = new Color(1f, 0.85f, 0.45f);
        private DungeonPlayer player;
        private Vector2 direction;
        private int damage, bonusPerBounce, bounces;
        private float remaining = Range;
        // Each enemy is struck once per leg of the flight, so a bounce back through a crowd hits it again.
        private readonly HashSet<DungeonEnemy> struck = new HashSet<DungeonEnemy>();

        public static void Fire(DungeonPlayer player, Vector2 aim, int damage, int bonusPerBounce)
        {
            var sprite = DungeonVisuals.Create("Ricochet arrow", player.Run.ProjectileRoot, player.transform.position, new Vector2(0.55f, 0.12f), Tint, 6);
            var arrow = sprite.gameObject.AddComponent<RicochetArrow>();
            arrow.player = player;
            arrow.direction = aim.normalized;
            arrow.damage = damage;
            arrow.bonusPerBounce = bonusPerBounce;
            arrow.Face();
            CombatVfx.Trail(sprite.gameObject, new Color(Tint.r, Tint.g, Tint.b, 0.7f), 0.08f, 0.14f);
        }

        private void Face() => transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || !run.IsPlaying) return;
            float distance = Mathf.Min(Speed * Time.deltaTime, remaining);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 position = transform.position;
                Vector2 next = position + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f))
                {
                    if (bounces >= MaxBounces) { Destroy(gameObject); return; }
                    Bounce(run, position);
                    continue;
                }
                transform.position = next;
                Breakable.SmashAt(run, next, 0.1f);
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || struck.Contains(enemy) || Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    struck.Add(enemy);
                    CombatDamage.Apply(player, enemy, damage + bonusPerBounce * bounces, DamageElement.Physical, next - direction, 0.8f);
                }
            }
            remaining -= distance;
            if (remaining <= 0f) Destroy(gameObject);
        }

        /// <summary>Reflects off whichever face of the wall it met (both, in a corner).</summary>
        private void Bounce(DungeonRun run, Vector2 position)
        {
            bool blockedX = !run.Map.CanStand(position + new Vector2(direction.x * 0.12f, 0f), 0.08f);
            bool blockedY = !run.Map.CanStand(position + new Vector2(0f, direction.y * 0.12f), 0.08f);
            if (blockedX || !blockedY) direction.x = -direction.x;
            if (blockedY || !blockedX) direction.y = -direction.y;
            bounces++;
            struck.Clear();
            Face();
            HeroVfx.Sparks(run.ProjectileRoot, position, Tint, 8, 3.5f, 0.25f, direction, 120f);
            CoopFx.Pulse(run, position, 0.5f, Tint, 0.2f);
        }
    }
}
