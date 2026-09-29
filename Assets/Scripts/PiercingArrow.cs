using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Archer's Piercing Shot: a heavy, blazing arrow that crosses most of a room, tears through every enemy in
    /// its line and only stops at a wall. Launching it kicks the Archer back and shakes the camera. It carries the element
    /// loaded in the Elemental Quiver (a crit sets it off) and burns in that element's colour.
    /// </summary>
    public sealed class PiercingArrow : MonoBehaviour
    {
        public const float Range = 18f, Speed = 28f, BoomSpacing = 1.8f;
        public static readonly Color Core = new Color(1f, 0.97f, 0.8f);
        private DungeonRun run;
        private DungeonPlayer shooter;
        private Vector2 direction;
        private float remaining, nextBoom;
        private int damage;
        private DamageElement infusion;
        private Color color;
        private bool ghost;
        private readonly HashSet<DungeonEnemy> hits = new HashSet<DungeonEnemy>();
        public bool IsSpent { get; private set; }
        public int HitCount => hits.Count;

        public static PiercingArrow Fire(DungeonPlayer shooter, Vector2 direction, int damage)
        {
            var run = shooter.Run;
            Vector2 origin = shooter.transform.position;
            direction.Normalize();
            var infusion = shooter.Mechanic is ElementalQuiver quiver ? quiver.Element : DamageElement.Physical;
            CoopFx.PiercingShot(run, origin, direction, infusion);
            var arrow = Create(run, origin, direction, infusion);
            arrow.shooter = shooter;
            arrow.damage = damage;
            // Recoil: the shot shoves the Archer back a step.
            shooter.transform.position = run.Map.Move(shooter.transform.position, -direction * 0.35f);
            ScreenFx.Shake(0.14f, 0.18f);
            return arrow;
        }

        /// <summary>A teammate's shot: same flight and effects, no damage.</summary>
        public static PiercingArrow SpawnGhost(DungeonRun run, Vector2 origin, Vector2 direction, DamageElement infusion = DamageElement.Physical)
            => Create(run, origin, direction.normalized, infusion, true);

        /// <summary>Gold for a plain shot, otherwise the quiver's element.</summary>
        public static Color ShotColor(DamageElement infusion)
            => infusion == DamageElement.Physical ? AbilityCatalog.Gold : CombatDamage.ElementColor(infusion);

        private static PiercingArrow Create(DungeonRun run, Vector2 origin, Vector2 direction, DamageElement infusion, bool ghost = false)
        {
            Color color = ShotColor(infusion);
            var body = DungeonVisuals.Create("Piercing shot", run.ProjectileRoot, origin, new Vector2(1.1f, 0.16f), color, 7);
            var arrow = body.gameObject.AddComponent<PiercingArrow>();
            arrow.infusion = infusion;
            arrow.color = color;
            arrow.run = run;
            arrow.direction = direction;
            arrow.remaining = Range;
            arrow.ghost = ghost;
            arrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            // A soft halo and a white-hot core ride along with the shaft.
            var halo = DungeonVisuals.Create("Halo", arrow.transform, origin, new Vector2(1.35f, 3.2f), FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.2f), 0.3f), 6);
            halo.transform.localPosition = Vector2.zero;
            var core = DungeonVisuals.Create("Core", arrow.transform, origin, new Vector2(0.9f, 0.4f), Core, 8);
            core.transform.localPosition = new Vector2(0.06f, 0f);
            var tip = DungeonVisuals.Create("Tip", arrow.transform, origin, new Vector2(0.2f, 1.7f), Color.white, 8);
            tip.transform.localPosition = new Vector2(0.52f, 0f);
            CombatVfx.Trail(arrow.gameObject, color, 0.34f, 0.28f);
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, origin + direction * 0.4f, 1.3f, color, 0.3f);
            HeroVfx.Sparks(root, origin, Core, 14, 6f, 0.3f, -direction, 70f, 1.2f);
            CombatVfx.GlowBolt(root, origin, origin + direction * 1.6f, color);
            return arrow;
        }

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (IsSpent || !run.IsPlaying || deltaTime <= 0f) return;
            float distance = Mathf.Min(Speed * deltaTime, remaining);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 previous = transform.position, next = previous + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f) || HolyBubble.Blocks(previous, next)) { Impact(); return; }
                transform.position = next;
                nextBoom -= distance / steps;
                if (nextBoom <= 0f)
                {
                    // Sonic rings mark the arrow's wake.
                    nextBoom = BoomSpacing;
                    HeroVfx.Pulse(run.ProjectileRoot, next, 0.55f, FlameMesh.Alpha(Color.Lerp(color, Color.white, 0.3f), 0.8f), 0.22f);
                }
                for (int j = run.Enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = run.Enemies[j];
                    if (enemy == null || hits.Contains(enemy) || Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius + 0.1f) continue;
                    hits.Add(enemy);
                    HeroVfx.Sparks(run.ProjectileRoot, next, Core, 12, 5.5f, 0.3f, direction, 80f, 1.3f);
                    CombatVfx.Ring(run.ProjectileRoot, next, 0.5f, color, 0.25f);
                    if (!ghost) CombatDamage.Apply(shooter, enemy, damage, DamageElement.Physical, next - direction, 1.6f, infusion);
                }
            }
            remaining -= distance;
            if (remaining <= 0f) Impact();
        }

        private void Impact()
        {
            HeroVfx.Sparks(run.ProjectileRoot, transform.position, color, 16, 4.5f, 0.35f, -direction, 150f, 1.2f);
            HeroVfx.Pulse(run.ProjectileRoot, transform.position, 0.9f, color, 0.3f);
            IsSpent = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
