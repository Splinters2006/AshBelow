using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Augment's Sentry Turret: planted on the ground, it fires a piercing plasma ray at the nearest enemy in sight
    /// every <see cref="Interval"/> seconds until its battery runs out. It opens fire the moment it lands.
    /// Teammates see a cosmetic copy; the rays it fires reach them like the Augment's own.
    /// </summary>
    public sealed class SentryTurret : MonoBehaviour
    {
        public const float Interval = 0.6f, Range = 7f, RayWidth = 0.14f;
        private DungeonRun run;
        private DungeonPlayer owner;
        private Transform barrel;
        private float expiresAt, nextShot;
        private int damage;
        private bool ghost;
        public int Shots { get; private set; }

        public static SentryTurret Deploy(DungeonPlayer owner, Vector2 position, float duration, int damage)
        {
            var run = owner.Run;
            CoopFx.Turret(run, position, duration);
            var turret = Create(run, position, duration, false);
            turret.owner = owner;
            turret.damage = damage;
            turret.Shoot();
            return turret;
        }

        public static SentryTurret SpawnGhost(DungeonRun run, Vector2 position, float duration) => Create(run, position, duration, true);

        private static SentryTurret Create(DungeonRun run, Vector2 position, float duration, bool ghost)
        {
            var body = CyborgVfx.TurretBody(run.ProjectileRoot, position, out var barrel);
            var turret = body.gameObject.AddComponent<SentryTurret>();
            turret.run = run;
            turret.barrel = barrel;
            turret.ghost = ghost;
            turret.expiresAt = Time.time + duration;
            turret.nextShot = Time.time + Interval;
            HeroVfx.Pulse(run.ProjectileRoot, position, 0.9f, CyborgAttack.Plasma, 0.3f);
            HeroVfx.Sparks(run.ProjectileRoot, position + Vector2.down * 0.2f, new Color(0.7f, 0.75f, 0.8f), 10, 3f, 0.3f, Vector2.up, 160f);
            return turret;
        }

        private void Update()
        {
            if (run == null || !run.IsPlaying) return;
            if (Time.time >= expiresAt || (!ghost && (owner == null || owner.Health <= 0))) { Expire(); return; }
            if (ghost) return;
            // Missile Turret: every 2 seconds it also looses a micro-missile at the nearest enemy.
            if (owner != null && owner.Powerups.Count(PowerupType.MissileTurret) > 0 && Time.time >= nextMissile)
            {
                nextMissile = Time.time + MissileInterval;
                var target = Nearest();
                if (target != null)
                {
                    Vector2 origin = transform.position;
                    Vector2 toward = ((Vector2)target.transform.position - origin).normalized;
                    MicroMissile.Launch(owner, origin + Vector2.up * 0.2f, toward, target, damage);
                }
            }
            if (Time.time < nextShot) return;
            nextShot = Time.time + Interval;
            Shoot();
        }

        public const float MissileInterval = 2f;
        private float nextMissile;

        private DungeonEnemy Nearest()
        {
            DungeonEnemy best = null;
            float nearest = Range;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance > nearest || !run.HasLineOfSight(transform.position, enemy.transform.position)) continue;
                nearest = distance;
                best = enemy;
            }
            return best;
        }

        /// <summary>Fires at the nearest living enemy in range and in sight; false when there is none.</summary>
        public bool Shoot()
        {
            if (ghost || owner == null) return false;
            Vector2 origin = transform.position;
            DungeonEnemy target = null;
            float nearest = Range;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0) continue;
                float distance = Vector2.Distance(origin, enemy.transform.position);
                if (distance > nearest || !run.HasLineOfSight(origin, enemy.transform.position)) continue;
                nearest = distance;
                target = enemy;
            }
            if (target == null) return false;
            Vector2 aim = (Vector2)target.transform.position - origin;
            if (aim.sqrMagnitude < 0.0001f) aim = Vector2.right;
            aim.Normalize();
            if (barrel != null) barrel.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            CyborgAttack.FireRay(owner, origin + aim * 0.15f + Vector2.up * 0.08f, aim, Range, RayWidth, damage, 0.2f);
            Shots++;
            return true;
        }

        private void Expire()
        {
            if (run != null && run.ProjectileRoot != null)
            {
                HeroVfx.Motes(run.ProjectileRoot, transform.position, 0.5f, CyborgAttack.Plasma, 10, 0.5f);
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, new Color(0.7f, 0.75f, 0.8f), 8, 2.5f, 0.3f);
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
