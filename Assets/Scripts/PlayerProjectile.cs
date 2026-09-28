using UnityEngine;

namespace Slopgame
{
    public sealed class PlayerProjectile : MonoBehaviour
    {
        private DungeonRun run;
        private int damage;
        public const float MaxRange = 5f;
        private float remainingRange = MaxRange;
        public float RemainingRange => remainingRange;
        public Vector2 Direction { get; private set; }
        public bool IsSpent { get; private set; }

        public static PlayerProjectile Spawn(DungeonRun run, Vector2 position, Vector2 direction, int damage)
        {
            var arrow = DungeonVisuals.Create("Arrow", run.ProjectileRoot, position, new Vector2(0.5f, 0.1f),
                new Color(0.95f, 1f, 0.65f), 6).gameObject.AddComponent<PlayerProjectile>();
            arrow.run = run;
            arrow.damage = damage;
            arrow.Direction = direction.normalized;
            arrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return arrow;
        }

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (IsSpent || !run.IsPlaying || deltaTime <= 0) return;
            float distance = Mathf.Min(12f * deltaTime, remainingRange);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + Direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f)) { Consume(); return; }
                transform.position = next;
                for (int j = run.Enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = run.Enemies[j];
                    if (Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    CombatDamage.Apply(run.Player, enemy, damage, DamageElement.Physical, next - Direction);
                    Consume();
                    return;
                }
            }
            remainingRange -= distance;
            if (remainingRange <= 0) Consume();
        }

        private void Consume()
        {
            IsSpent = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
