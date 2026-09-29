using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// One of the Augment's Micro-Missiles. It leaves the pod on a spread heading, then curves in on its target (tighter
    /// the longer it flies, so it never circles) and bursts on impact, a wall, or when its fuel runs out.
    /// Teammates see a copy fly to where the target stood at launch.
    /// </summary>
    public sealed class MicroMissile : MonoBehaviour
    {
        public const float Speed = 9f, Lifetime = 2.2f, BlastRadius = 0.9f;
        private DungeonRun run;
        private DungeonPlayer shooter;
        private DungeonEnemy target;
        private Vector2 direction, goal;
        private float age;
        private int damage;
        private bool ghost;
        public bool IsSpent { get; private set; }

        public static MicroMissile Launch(DungeonPlayer shooter, Vector2 origin, Vector2 direction, DungeonEnemy target, int damage)
        {
            var run = shooter.Run;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            bool tracking = target != null && target.Health > 0;
            Vector2 goal = tracking ? (Vector2)target.transform.position : origin + direction * 6f;
            CoopFx.Missile(run, origin, direction, goal);
            var missile = Create(run, origin, direction, goal, false);
            missile.shooter = shooter;
            missile.damage = damage;
            // With nobody to chase, a missile flies straight out and bursts 6 units away.
            missile.target = tracking ? target : null;
            return missile;
        }

        public static MicroMissile SpawnGhost(DungeonRun run, Vector2 origin, Vector2 direction, Vector2 goal)
            => Create(run, origin, direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right, goal, true);

        private static MicroMissile Create(DungeonRun run, Vector2 origin, Vector2 direction, Vector2 goal, bool ghost)
        {
            var body = DungeonVisuals.Create("Micro-missile", run.ProjectileRoot, origin, new Vector2(0.26f, 0.09f), new Color(0.85f, 0.88f, 0.92f), 7);
            var tip = DungeonVisuals.Create("Tip", body.transform, origin, new Vector2(0.3f, 1f), CyborgAttack.MissileColor, 8);
            tip.transform.localPosition = new Vector2(0.45f, 0f);
            var missile = body.gameObject.AddComponent<MicroMissile>();
            missile.run = run;
            missile.direction = direction;
            missile.goal = goal;
            missile.ghost = ghost;
            missile.Face();
            CombatVfx.Trail(missile.gameObject, new Color(1f, 0.7f, 0.4f, 0.7f), 0.08f, 0.2f);
            return missile;
        }

        private void Face() => transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (IsSpent || run == null || !run.IsPlaying || deltaTime <= 0f) return;
            age += deltaTime;
            if (target != null && target.Health > 0) goal = target.transform.position;
            else target = null;
            Vector2 position = transform.position;
            Vector2 wanted = goal - position;
            if (wanted.sqrMagnitude > 0.0001f)
            {
                // Turns harder the longer it flies, so it closes in instead of orbiting.
                float turn = (240f + 900f * age) * Mathf.Deg2Rad * deltaTime;
                direction = ((Vector2)Vector3.RotateTowards(direction, wanted.normalized, turn, 0f)).normalized;
                Face();
            }
            float distance = Speed * deltaTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 previous = transform.position, next = previous + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.05f) || HolyBubble.Blocks(previous, next)) { Explode(previous); return; }
                transform.position = next;
                if (target == null && Vector2.Distance(next, goal) < 0.25f) { Explode(next); return; }
                foreach (var enemy in run.Enemies)
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(next, enemy.transform.position) <= enemy.HitRadius + 0.15f)
                    { Explode(next); return; }
            }
            if (age >= Lifetime) Explode(transform.position);
        }

        private void Explode(Vector2 center)
        {
            if (IsSpent) return;
            IsSpent = true;
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, center, BlastRadius, CyborgAttack.MissileColor, 0.25f);
            HeroVfx.Sparks(root, center, Color.Lerp(CyborgAttack.MissileColor, Color.white, 0.3f), 8, 3.5f, 0.28f);
            if (!ghost && shooter != null)
                foreach (var enemy in run.Enemies.ToArray())
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= BlastRadius + enemy.HitRadius
                        && run.HasLineOfSight(center, enemy.transform.position))
                        CombatDamage.Apply(shooter, enemy, damage, DamageElement.Physical, center, 0.6f);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
