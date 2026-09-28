using UnityEngine;

namespace Slopgame
{
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private const float Speed = 7.5f;
        private DungeonRun run;
        private Vector2 direction;
        private float lifetime = 4f;
        private bool spent;
        public bool IsReflected { get; private set; }
        private int reflectedDamage;
        public bool IsSpent => spent;

        public static EnemyProjectile Spawn(DungeonRun run, Transform parent, Vector2 position, Vector2 direction)
        {
            var projectile = DungeonVisuals.CreateEmberBolt(parent, position).gameObject.AddComponent<EnemyProjectile>();
            projectile.run = run;
            projectile.direction = direction.normalized;
            projectile.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return projectile;
        }

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (spent || !run.IsPlaying) return;
            lifetime -= deltaTime;
            if (lifetime <= 0) { Consume(); return; }
            // Small steps prevent fast projectiles from skipping walls or the player.
            Vector2 movement = direction * Speed * deltaTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + movement / steps;
                if (!run.Map.CanStand(next, 0.11f)) { Consume(); return; }
                transform.position = next;
                if (IsReflected)
                {
                    for (int j = run.Enemies.Count - 1; j >= 0; j--)
                    {
                        var enemy = run.Enemies[j];
                        if (Vector2.Distance(next, enemy.transform.position) > 0.38f) continue;
                        enemy.Hit(reflectedDamage, next - direction);
                        Consume();
                        return;
                    }
                    continue;
                }
                var shield = run.Player.Shield;
                if (shield != null && shield.CanReflect(next, direction))
                {
                    IsReflected = true;
                    reflectedDamage = run.Player.Powerups.ReflectionDamage;
                    direction = -direction;
                    lifetime = 4f;
                    transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                    GetComponent<SpriteRenderer>().color = new Color(0.55f, 0.85f, 1f);
                    return;
                }
                if (Vector2.Distance(next, run.Player.transform.position) <= 0.42f)
                {
                    run.Player.Hit();
                    Consume();
                    return;
                }
            }
        }

        private void Consume()
        {
            spent = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
