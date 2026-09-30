using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// One of the Augment's Micro-Missiles. It leaves the pod on a spread heading, then curves hard in on its target
    /// (tighter the longer it flies and the closer it gets, so it never circles), switching to the nearest enemy if its
    /// target falls, and bursts on impact, a wall, or when its fuel runs out.
    /// Teammates see a copy fly to where the target stood at launch.
    /// </summary>
    public sealed class MicroMissile : MonoBehaviour
    {
        public const float Speed = 9f, Lifetime = 2.2f, BlastRadius = 0.9f, TurnRate = 540f, TurnGrowth = 1600f, RetargetRange = 7f;
        private DungeonRun run;
        private DungeonPlayer shooter;
        private DungeonEnemy target;
        private Vector2 direction, goal;
        private float age;
        private int damage;
        private bool ghost, tracking;
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
            missile.tracking = tracking;
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
            // If its target falls first, it locks onto whoever is nearest instead of flying on to an empty spot.
            if ((target == null || target.Health <= 0) && !ghost && tracking) target = Nearest(transform.position);
            if (target != null && target.Health > 0) goal = target.transform.position;
            else target = null;
            Vector2 position = transform.position;
            Vector2 wanted = goal - position;
            if (wanted.sqrMagnitude > 0.0001f)
            {
                // Turns hard from the start and harder the longer it flies, so it closes in instead of orbiting;
                // close to its prey it all but snaps onto it.
                float close = Mathf.Clamp01(1f - wanted.magnitude / 2.5f);
                float turn = (TurnRate + TurnGrowth * age + 720f * close) * Mathf.Deg2Rad * deltaTime;
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

        private DungeonEnemy Nearest(Vector2 from)
        {
            DungeonEnemy best = null;
            float bestDistance = RetargetRange;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(from, enemy.transform.position);
                if (distance < bestDistance && run.HasLineOfSight(from, enemy.transform.position)) { bestDistance = distance; best = enemy; }
            }
            return best;
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
