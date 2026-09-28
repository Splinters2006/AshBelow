using UnityEngine;

namespace Slopgame
{
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private DungeonRun run;
        private Vector2 direction;
        private float lifetime = 4f;
        private bool spent;
        public bool IsSpent => spent;

        public static EnemyProjectile Spawn(DungeonRun run, Transform parent, Vector2 position, Vector2 direction)
        {
            var projectile = DungeonVisuals.Create("Ember bolt", parent, position, Vector2.one * 0.22f,
                new Color(1f, 0.8f, 0.25f), 6).gameObject.AddComponent<EnemyProjectile>();
            projectile.run = run;
            projectile.direction = direction.normalized;
            return projectile;
        }

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (spent || !run.IsPlaying) return;
            lifetime -= deltaTime;
            if (lifetime <= 0) { Consume(); return; }
            // Small steps prevent fast projectiles from skipping walls or the player.
            Vector2 movement = direction * 6f * deltaTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + movement / steps;
                if (!run.Map.CanStand(next, 0.11f)) { Consume(); return; }
                transform.position = next;
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
