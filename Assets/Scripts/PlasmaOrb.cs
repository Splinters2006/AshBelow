using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Augment's charged cannon shot: a spinning plasma orb that flies until it meets an enemy or a wall, then bursts
    /// into a fiery blast. A fully charged orb is sure to set everything it hits burning.
    /// </summary>
    public sealed class PlasmaOrb : MonoBehaviour
    {
        public const float Speed = 13f, Range = 11f;
        private DungeonRun run;
        private DungeonPlayer shooter;
        private Vector2 direction;
        private float remaining, radius, size;
        private int damage;
        private bool ghost, fullCharge;
        public bool IsSpent { get; private set; }

        public static PlasmaOrb Fire(DungeonPlayer shooter, Vector2 origin, Vector2 direction, int damage, float radius, float charge)
        {
            var run = shooter.Run;
            direction.Normalize();
            CoopFx.CannonShot(run, origin, direction, radius, charge);
            var orb = Create(run, origin, direction, radius, charge, false);
            orb.shooter = shooter;
            orb.damage = damage;
            orb.fullCharge = charge >= 1f;
            return orb;
        }

        /// <summary>A teammate's shot: same flight and blast, no damage.</summary>
        public static PlasmaOrb SpawnGhost(DungeonRun run, Vector2 origin, Vector2 direction, float radius, float charge)
            => Create(run, origin, direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right, radius, charge, true);

        private static PlasmaOrb Create(DungeonRun run, Vector2 origin, Vector2 direction, float radius, float charge, bool ghost)
        {
            charge = Mathf.Clamp01(charge);
            float size = 0.3f + 0.25f * charge;
            var body = DungeonVisuals.Create("Plasma orb", run.ProjectileRoot, origin, Vector2.one * size, CyborgAttack.Plasma, 7);
            body.transform.rotation = Quaternion.Euler(0, 0, 45f);
            var core = DungeonVisuals.Create("Core", body.transform, origin, Vector2.one * 0.55f, CyborgAttack.Core, 8);
            core.transform.localPosition = Vector2.zero;
            var orb = body.gameObject.AddComponent<PlasmaOrb>();
            orb.run = run;
            orb.direction = direction;
            orb.remaining = Range;
            orb.radius = radius;
            orb.size = size;
            orb.ghost = ghost;
            CombatVfx.Trail(orb.gameObject, new Color(CyborgAttack.Plasma.r, CyborgAttack.Plasma.g, CyborgAttack.Plasma.b, 0.8f), size * 0.8f, 0.18f);
            HeroVfx.Pulse(run.ProjectileRoot, origin, 0.6f + 0.4f * charge, CyborgAttack.Plasma, 0.25f);
            HeroVfx.Sparks(run.ProjectileRoot, origin, CyborgAttack.Core, 10, 5f, 0.25f, direction, 60f);
            return orb;
        }

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (IsSpent || run == null || !run.IsPlaying || deltaTime <= 0f) return;
            transform.Rotate(0f, 0f, 540f * deltaTime);
            float distance = Mathf.Min(Speed * deltaTime, remaining);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 previous = transform.position, next = previous + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f) || HolyBubble.Blocks(previous, next)) { Burst(previous); return; }
                transform.position = next;
                if (!ghost) Breakable.SmashAt(run, next, size * 0.5f);
                foreach (var enemy in run.Enemies)
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(next, enemy.transform.position) <= enemy.HitRadius + size * 0.5f)
                    { Burst(next); return; }
            }
            remaining -= distance;
            if (remaining <= 0f) Burst(transform.position);
        }

        private void Burst(Vector2 center)
        {
            if (IsSpent) return;
            IsSpent = true;
            var root = run.ProjectileRoot;
            var fire = CombatDamage.ElementColor(DamageElement.Fire);
            HeroVfx.Pulse(root, center, radius, CyborgAttack.Plasma, 0.35f);
            CombatVfx.Ring(root, center, radius, fire, 0.35f);
            HeroVfx.Sparks(root, center, Color.Lerp(fire, CyborgAttack.Core, 0.4f), 18, 5.5f, 0.4f, null, 360f, 1.2f);
            HeroVfx.Sparks(root, center, CyborgAttack.Plasma, 10, 3.5f, 0.35f);
            if (!ghost)
            {
                ScreenFx.Shake(0.1f + 0.05f * radius, 0.18f);
                Breakable.SmashAt(run, center, radius);
                foreach (var enemy in run.Enemies.ToArray())
                    if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= radius + enemy.HitRadius
                        && run.HasLineOfSight(center, enemy.transform.position))
                        CombatDamage.Apply(shooter, enemy, damage, DamageElement.Fire, center, 1.3f, guaranteedEffect: fullCharge);
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
