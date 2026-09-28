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
        private bool ghost;

        public static PlayerProjectile Spawn(DungeonRun run, Vector2 position, Vector2 direction, int damage, float range = MaxRange)
        {
            CoopFx.Arrow(run, position, direction, range);
            return Create(run, position, direction, damage, range);
        }

        /// <summary>A teammate's arrow: flies and stops like theirs, but their machine deals the damage.</summary>
        public static PlayerProjectile SpawnGhost(DungeonRun run, Vector2 position, Vector2 direction, float range)
        {
            var arrow = Create(run, position, direction, 0, range);
            arrow.ghost = true;
            return arrow;
        }

        private static PlayerProjectile Create(DungeonRun run, Vector2 position, Vector2 direction, int damage, float range)
        {
            var arrow = DungeonVisuals.Create("Arrow", run.ProjectileRoot, position, new Vector2(0.5f, 0.1f),
                new Color(0.95f, 1f, 0.65f), 6).gameObject.AddComponent<PlayerProjectile>();
            arrow.run = run;
            arrow.damage = damage;
            arrow.remainingRange = Mathf.Max(0f, range);
            arrow.Direction = direction.normalized;
            arrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            CombatVfx.Trail(arrow.gameObject, new Color(0.95f, 1f, 0.65f, 0.8f), 0.07f, 0.1f);
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
                if (!run.Map.CanStand(next, 0.08f))
                {
                    HeroVfx.Sparks(run.ProjectileRoot, transform.position, new Color(0.85f, 0.85f, 0.7f), 5, 2.5f, 0.2f, -Direction, 140f, 0.7f);
                    Consume();
                    return;
                }
                transform.position = next;
                for (int j = run.Enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = run.Enemies[j];
                    if (Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    if (!ghost) CombatDamage.Apply(run.Player, enemy, damage, DamageElement.Physical, next - Direction);
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
