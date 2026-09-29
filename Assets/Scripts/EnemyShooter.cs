using UnityEngine;

namespace Slopgame
{
    [RequireComponent(typeof(DungeonEnemy))]
    public sealed class EnemyShooter : MonoBehaviour
    {
        public bool IsCharging { get; private set; }
        public int ProjectileCount { get; set; } = 1;
        public float SpreadDegrees { get; set; } = 18f;
        public float Windup { get; set; } = 0.45f;
        public float Recovery { get; set; } = 0.9f;
        private DungeonEnemy enemy;
        private float readyAt, fireAt;
        private Vector2 lockedDirection;

        private void Start()
        {
            enemy = GetComponent<DungeonEnemy>();
            readyAt = enemy.ActionTime + 0.6f;
        }

        /// <summary>Co-op guest: the host decides when casters wind up and fire.</summary>
        public void SetCharging(bool charging) { IsCharging = charging; }

        private void Update()
        {
            if (!enemy.Run.IsPlaying || enemy.Health <= 0 || enemy.Run.IsGuest || enemy.IsHeld) return;
            // A caster that cannot see anyone (Shadow Veil) has no aim: the offset stays zero and it never starts a new windup.
            enemy.Run.TryNearestVisibleHero(transform.position, out Vector2 target);
            Vector2 offset = target - (Vector2)transform.position;
            if (IsCharging)
            {
                if (enemy.ActionTime < fireAt) return;
                for (int i = 0; i < ProjectileCount; i++)
                {
                    Vector2 direction = Quaternion.Euler(0, 0, (i - (ProjectileCount - 1) * 0.5f) * SpreadDegrees) * lockedDirection;
                    EnemyProjectile.Spawn(enemy.Run, transform.parent, transform.position, direction, true, EnemyProjectile.DefaultSpeed, enemy.Run.World.Bolts);
                }
                IsCharging = false;
                readyAt = enemy.ActionTime + Recovery;
            }
            else if (enemy.ActionTime >= readyAt && offset.sqrMagnitude < 64f && offset.sqrMagnitude > 0.01f
                && Vector2.Dot(enemy.Facing.Direction, offset.normalized) >= 0.98f
                && enemy.Run.HasLineOfSight(transform.position, target))
            {
                // Lock aim before firing so the white windup gives the player time to evade.
                lockedDirection = enemy.Facing.Direction;
                IsCharging = true;
                fireAt = enemy.ActionTime + Windup;
            }
        }
    }
}
