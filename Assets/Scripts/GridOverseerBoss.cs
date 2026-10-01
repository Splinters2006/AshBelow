using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Locks corridors in sequence; the gaps between scan lines are always traversable. It also rushes heroes down a
    /// marked line, sweeps shock arcs up close, and locks three sectors of the arena before jumping into the open one.
    /// It runs at double clock speed: every attack and the vent after it take half the time (see <see cref="Pace"/>).
    /// </summary>
    public sealed class GridOverseerBoss : NeonBossBehaviour
    {
        public const float RushLength = 12f, SweepReach = 3.4f, SweepCone = 140f, LockTelegraph = 1.6f, LockDuration = 1.3f;
        /// <summary>Every attack timing is scaled by this: half, so the Overseer attacks twice as fast.</summary>
        public const float Pace = 0.5f;
        /// <summary>The shortest warning any of its strikes gives, so the fastest ones can still be reacted to.</summary>
        public const float MinWarning = 0.3f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "GRID OVERSEER";
        public override string Title => FixedTitle;
        protected override Color Accent => WorldCatalog.Neon;
        protected override Sprite Chassis => GuardianSprites.Overseer;
        protected override Sprite Details => GuardianSprites.OverseerDetails;
        protected override Color BodyTint => new Color(0.5f, 0.55f, 0.66f);
        protected override HazardStyle Hazards => HazardStyle.Circuit;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "SCAN GRID - STEP BETWEEN THE LANES", "TRACE LOCK - LEAVE YOUR MARK", "FIREWALL - FOLLOW THE OPENING",
            "OVERRIDE RUSH - SIDESTEP THE LINE", "SECTOR LOCKDOWN - GET TO THE OPEN SECTOR", "SHOCK SWEEP - GET BEHIND IT"
        };
        private int step;
        private Vector2 openSector;
        public override int BaseHealth(int floor) => 20 + floor * 2;

        protected override float SpentTime => base.SpentTime * Pace;
        protected override float ApproachPause => base.ApproachPause * Pace;

        /// <summary>A strike's warning at the Overseer's pace.</summary>
        private static float Warn(float seconds) => Mathf.Max(MinWarning, seconds * Pace);

        private int Rushes => IsEnraged ? 2 : 1;
        private int Sweeps => IsEnraged ? 3 : 2;

        protected override float Attack(int index, Vector2 aim)
        {
            step = 0;
            Vector2 target = Run.NearestHero(transform.position);
            switch (index)
            {
                case 0:
                    for (int i = -2; i <= 2; i++)
                        Beam(target + Vector2.right * i * 3f, Vector2.up, 24f, Warn(1f + (i + 2) * 0.22f));
                    if (IsEnraged) Beam(target, Vector2.right, 24f, Warn(2.2f));
                    return 3f * Pace;
                case 1:
                    foreach (Vector2 hero in LivingHeroPositions())
                        for (int i = 0; i < (IsEnraged ? 3 : 2); i++)
                            Beam(hero, Quaternion.Euler(0, 0, i * 60f) * aim, 15f, Warn(1f + i * 0.5f));
                    return 2.8f * Pace;
                case 2:
                    // Parallel lines ignite in order, giving a moving route through the firewall.
                    for (int i = 0; i < 5; i++)
                        Beam(target + Vector2.up * (i - 2) * 2.8f, Vector2.right, 25f, Warn(0.9f + i * (IsEnraged ? 0.3f : 0.45f)));
                    return 3.5f * Pace;
                case 3:
                    PlanCharge(aim, RushLength, 1.4f, 0f, 0.7f * Pace, 0.3f * Pace);
                    return (Rushes * 1.4f + 0.5f) * Pace;
                case 4:
                {
                    // Three sectors lock; the Overseer jumps into the open one and pulses once the lock strikes. The warning
                    // is cut by less than half: the party still has to cross the arena on foot.
                    var (right, top) = QuadrantAwayFromParty();
                    openSector = LockAllButQuadrant(right, top, LockTelegraph, LockDuration);
                    return LockTelegraph + LockDuration + 0.3f;
                }
                default:
                    PlanLunge(StrikeSpot(target, 1.4f), 0.3f * Pace, 0.2f * Pace);
                    return (0.5f + Sweeps * 0.75f + 0.4f) * Pace;
            }
        }

        protected override void AttackTick(float time)
        {
            switch (state)
            {
                case 4:
                    // Each rush ends in a shock pulse where it stops; enraged it turns and rushes again.
                    if (!TickLunge(time)) return;
                    Vector2 stop = transform.position;
                    Scorch(stop, 2.2f, Warn(0.4f), 0.3f);
                    BurstAfter(Warn(0.4f), stop, 2.2f);
                    if (++step < Rushes) PlanCharge(AimAt(Run.NearestHero(stop)), RushLength, 1.4f, time + 0.5f * Pace, 0.6f * Pace, 0.3f * Pace);
                    break;
                case 5:
                    if (step == 0 && time >= LockTelegraph - 0.4f) { step = 1; BlinkTo(openSector); }
                    if (step == 1 && time >= LockTelegraph + 0.2f)
                    {
                        step = 2;
                        Scorch(transform.position, 2.4f, Warn(0.7f), 0.3f);
                        BurstAfter(Warn(0.7f), transform.position, 2.4f);
                    }
                    break;
                case 6:
                    // Lunge in, then sweep shock arcs, turning to follow the target.
                    TickLunge(time);
                    if (step >= Sweeps || time < (0.5f + step * 0.75f) * Pace) return;
                    Vector2 here = transform.position;
                    Cleave(here, AimAt(Run.NearestHero(here)), SweepReach, SweepCone, Warn(0.45f));
                    step++;
                    break;
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            // Four emitter arms; they splay outward and spark while it sweeps or rushes.
            float splay = state == 4 || state == 6 ? 0.35f + 0.25f * Mathf.Sin(StateAge * 14f) : 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = Quaternion.Euler(0, 0, i * 90f + Time.time * (IsCharging ? 90f : 20f)) * Vector2.right;
                Vector2 start = center + dir * (1.25f + splay);
                mesh.Bar(start, Vector2.Perpendicular(dir), 0.65f, 0.09f, Accent * intensity, Accent);
                mesh.Diamond(start, 0.13f + 0.04f * Mathf.Sin(Time.time * 8f), Color.white);
            }
        }
    }
}
