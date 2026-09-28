using UnityEngine;

namespace Slopgame
{
    [RequireComponent(typeof(DungeonEnemy))]
    public sealed class EnemyShooter : MonoBehaviour
    {
        public bool IsCharging { get; private set; }
        private DungeonEnemy enemy;
        private float readyAt, fireAt;
        private Vector2 lockedDirection;

        private void Start()
        {
            enemy = GetComponent<DungeonEnemy>();
            readyAt = enemy.ActionTime + 1f;
        }

        /// <summary>Co-op guest: the host decides when casters wind up and fire.</summary>
        public void SetCharging(bool charging) { IsCharging = charging; }

        private void Update()
        {
            if (!enemy.Run.IsPlaying || enemy.Health <= 0 || enemy.Run.IsGuest) return;
            Vector2 target = enemy.Run.NearestHero(transform.position);
            Vector2 offset = target - (Vector2)transform.position;
            if (IsCharging)
            {
                if (enemy.ActionTime < fireAt) return;
                EnemyProjectile.Spawn(enemy.Run, transform.parent, transform.position, lockedDirection);
                IsCharging = false;
                readyAt = enemy.ActionTime + 1.5f;
            }
            else if (enemy.ActionTime >= readyAt && offset.sqrMagnitude < 64f && offset.sqrMagnitude > 0.01f
                && Vector2.Dot(enemy.Facing.Direction, offset.normalized) >= 0.98f
                && enemy.Run.HasLineOfSight(transform.position, target))
            {
                // Lock aim before firing so the white windup gives the player time to evade.
                lockedDirection = enemy.Facing.Direction;
                IsCharging = true;
                fireAt = enemy.ActionTime + 0.45f;
            }
        }
    }
}
