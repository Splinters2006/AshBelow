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
            readyAt = Time.time + 1f;
        }

        private void Update()
        {
            if (!enemy.Run.IsPlaying || enemy.Health <= 0) return;
            Vector2 offset = enemy.Run.Player.transform.position - transform.position;
            if (IsCharging)
            {
                if (Time.time < fireAt) return;
                EnemyProjectile.Spawn(enemy.Run, transform.parent, transform.position, lockedDirection);
                IsCharging = false;
                readyAt = Time.time + 1.5f;
            }
            else if (Time.time >= readyAt && offset.sqrMagnitude < 64f && offset.sqrMagnitude > 0.01f
                && enemy.Run.HasLineOfSight(transform.position, enemy.Run.Player.transform.position))
            {
                // Lock aim before firing so the white windup gives the player time to evade.
                lockedDirection = offset.normalized;
                IsCharging = true;
                fireAt = Time.time + 0.45f;
            }
        }
    }
}
