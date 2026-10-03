using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A skull the Reaper throws for a soul. It curves in on the nearest enemy in sight, bites the first one it reaches
    /// and can leave it struck with fear.
    /// </summary>
    public sealed class ReaperSkull : MonoBehaviour
    {
        public const float Speed = 9f, TurnRate = 420f, SeekRange = 9f, Size = 0.36f;
        private DungeonRun run;
        private DungeonPlayer shooter;
        private Vector2 direction;
        private float remaining, fear;
        private int damage;
        private bool homing, spent;
        private static Sprite sprite;

        public static Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite("Reaper skull", new[]
        {
            ".WWWWW.", "WWWWWWW", "WNNWNNW", "WNNWNNW", "WWWNWWW", ".WWWWW.", ".WNWNW."
        }, key => key == 'W' ? ReaperAttack.Bone : key == 'N' ? new Color(0.05f, 0.12f, 0.1f) : Color.clear);

        /// <param name="fear">Seconds of fear its bite leaves; 0 for none.</param>
        public static ReaperSkull Launch(DungeonPlayer shooter, Vector2 origin, Vector2 direction, int damage, float range, float fear, bool homing)
        {
            var run = shooter.Run;
            var body = DungeonVisuals.Create("Reaper skull", run.ProjectileRoot, origin, Vector2.one * Size, Color.white, 7);
            body.sprite = Sprite;
            var skull = body.gameObject.AddComponent<ReaperSkull>();
            skull.run = run;
            skull.shooter = shooter;
            skull.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            skull.damage = damage;
            skull.remaining = range;
            skull.fear = fear;
            skull.homing = homing;
            body.flipX = skull.direction.x < 0f;
            CombatVfx.Trail(skull.gameObject, FlameMesh.Alpha(ReaperAttack.Soul, fear > 0f ? 0.8f : 0.5f), 0.14f, 0.18f);
            return skull;
        }

        private void Update()
        {
            if (spent || run == null || !run.IsPlaying) return;
            Vector2 position = transform.position;
            if (homing)
            {
                var target = Nearest(position);
                if (target != null)
                {
                    Vector2 wanted = ((Vector2)target.transform.position - position).normalized;
                    direction = ((Vector2)Vector3.RotateTowards(direction, wanted, TurnRate * Mathf.Deg2Rad * Time.deltaTime, 0f)).normalized;
                }
            }
            float distance = Speed * Time.deltaTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 previous = transform.position, next = previous + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.05f)) { Burst(previous); return; }
                transform.position = next;
                Breakable.SmashAt(run, next, 0.1f);
                foreach (var enemy in run.Enemies)
                {
                    if (enemy == null || enemy.Health <= 0 || Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius + 0.18f) continue;
                    Bite(enemy, next);
                    return;
                }
            }
            remaining -= distance;
            if (remaining <= 0f) Burst(transform.position);
        }

        private DungeonEnemy Nearest(Vector2 from)
        {
            DungeonEnemy best = null;
            float bestDistance = SeekRange;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(from, enemy.transform.position);
                if (distance < bestDistance && run.HasLineOfSight(from, enemy.transform.position)) { bestDistance = distance; best = enemy; }
            }
            return best;
        }

        private void Bite(DungeonEnemy enemy, Vector2 at)
        {
            Vector2 from = shooter != null ? (Vector2)shooter.transform.position : at - direction;
            CombatDamage.Apply(shooter, enemy, damage, DamageElement.Demonic, at - direction, 0.6f);
            if (fear > 0f && enemy != null && enemy.Health > 0) enemy.Fear(from, fear);
            Burst(at);
        }

        private void Burst(Vector2 at)
        {
            if (spent) return;
            spent = true;
            HeroVfx.Sparks(run.ProjectileRoot, at, ReaperAttack.Bone, 7, 3.2f, 0.28f, -direction, 160f, 0.9f);
            HeroVfx.Pulse(run.ProjectileRoot, at, 0.45f, ReaperAttack.Soul, 0.2f);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
