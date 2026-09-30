using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Area denial artillery, with a broadside that leaves a safe central corridor. It also rams across the arena on its
    /// treads, stomps out shockwaves up close, and shells everything but the ground right around itself, so the
    /// party has to get under its guns (where its hull swipe waits).
    /// </summary>
    public sealed class SiegeEngineBoss : NeonBossBehaviour
    {
        public const float RamLength = 16f, RamWidth = 2.2f, StompRadius = 3.2f, PerimeterRadius = 3.4f, PerimeterTelegraph = 2.4f, PerimeterDuration = 2.4f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "BASTION, THE SIEGE ENGINE";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(1f, 0.65f, 0.15f);
        protected override Sprite Chassis => GuardianSprites.Bastion;
        protected override Sprite Details => GuardianSprites.BastionDetails;
        protected override Color BodyTint => new Color(0.55f, 0.52f, 0.5f);
        protected override HazardStyle Hazards => HazardStyle.Artillery;
        protected override float Scale => 2.2f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "MISSILE WALK - KEEP MOVING", "CLUSTER MINES - FIND CLEAR GROUND", "BROADSIDE - STAY BETWEEN THE RAILS",
            "TREAD RAM - SIDESTEP THE TREADS", "SEISMIC STOMP - GET OUT OF RANGE", "SIEGE PERIMETER - GET UNDER ITS GUNS"
        };
        private int step;
        private Vector2 ramFrom;
        public override int BaseHealth(int floor) => 28 + floor * 2;

        private int Rams => IsEnraged ? 2 : 1;

        protected override float Attack(int index, Vector2 aim)
        {
            step = 0;
            Vector2 center = transform.position;
            switch (index)
            {
                case 0:
                    foreach (Vector2 hero in LivingHeroPositions())
                        for (int i = 0; i < (IsEnraged ? 5 : 4); i++)
                            Hazard(HazardShape.Pool, hero + aim * (i - 1) * 2.4f, aim, 1.25f, 0f, 1f + i * 0.4f, 0.6f);
                    return 3.5f;
                case 1:
                {
                    Vector2 target = Run.NearestHero(center);
                    for (int i = 0; i < 6; i++)
                        Hazard(HazardShape.Pool, target + FlameMesh.Polar(i * Mathf.PI / 3f, 3.5f), aim, IsEnraged ? 1.5f : 1.2f, 0f, 1.3f, 2.5f);
                    return 4.2f;
                }
                case 2:
                {
                    Vector2 side = Vector2.Perpendicular(aim);
                    for (int i = -1; i <= 1; i += 2)
                        Hazard(HazardShape.Beam, center + side * i * 2f, aim, 22f, 1.4f, 1.2f, 1.1f);
                    if (IsEnraged)
                        Hazard(HazardShape.Pool, Run.NearestHero(center), aim, 1.2f, 0f, 2.4f, 0.5f);
                    return 3.3f;
                }
                case 3:
                    ramFrom = center;
                    PlanCharge(aim, RamLength, RamWidth, 0f, 1f, 0.45f);
                    return Rams * 1.9f + 0.6f;
                case 4:
                    Stomp(center, 0.8f);
                    return IsEnraged ? 2.8f : 1.8f;
                default:
                    // Shells rain on everything but a circle around the tank itself.
                    Hazard(HazardShape.Inferno, center, Vector2.up, PerimeterRadius, 0f, PerimeterTelegraph, PerimeterDuration);
                    return PerimeterTelegraph + PerimeterDuration + 0.3f;
            }
        }

        /// <summary>A point-blank blast at its feet and a shockwave rolling out across the arena.</summary>
        private void Stomp(Vector2 center, float telegraph)
        {
            Scorch(center, StompRadius, telegraph, 0.3f);
            BurstAfter(telegraph, center, StompRadius);
            After(telegraph, () => ScreenFx.Shake(0.3f, 0.45f));
            Hazard(HazardShape.Ring, center, Vector2.right, 10f, 0.8f, telegraph + 0.3f, 1.3f);
        }

        protected override void AttackTick(float time)
        {
            switch (state)
            {
                case 4:
                    // The ram leaves mines in its tracks; enraged it turns and rams again.
                    if (!TickLunge(time)) return;
                    Vector2 stop = transform.position;
                    ScreenFx.Shake(0.25f, 0.35f);
                    float length = Vector2.Distance(ramFrom, stop);
                    for (float d = 2f; d < length; d += 3.5f)
                        Hazard(HazardShape.Pool, Vector2.Lerp(ramFrom, stop, d / length), Vector2.right, 1.1f, 0f, 0.7f, 0.6f);
                    if (++step < Rams)
                    {
                        ramFrom = stop;
                        PlanCharge(AimAt(Run.NearestHero(stop)), RamLength, RamWidth, time + 0.3f, 0.9f, 0.45f);
                    }
                    break;
                case 5:
                    if (step == 0 && IsEnraged && time >= 1f) { step = 1; Stomp(transform.position, 0.6f); }
                    break;
                case 6:
                    // Anyone who made it under the guns meets a hull swipe; the far side of the circle stays clear.
                    if (step >= 2 || time < PerimeterTelegraph + 0.3f + step * 1f) return;
                    Vector2 here = transform.position;
                    Cleave(here, AimAt(Run.NearestHero(here)), PerimeterRadius, 100f, 0.55f);
                    step++;
                    break;
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            // Exhaust vents on both flanks, roaring while it rams.
            float roar = state == 4 ? 1.8f : 1f;
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    Vector2 start = center + new Vector2(side * 1.25f, i * 0.3f - 0.5f);
                    mesh.Bar(start, Vector2.right * side, (0.35f + 0.15f * Mathf.Sin(Time.time * 12f + i)) * roar, 0.12f,
                        FlameMesh.Alpha(Accent, intensity), FlameMesh.Alpha(Accent, 0.1f));
                }
            // The perimeter's shield dome shimmers over the safe ground.
            if (state == 6 && StateAge < PerimeterTelegraph + PerimeterDuration)
                mesh.Ring(center, PerimeterRadius, 0.06f, FlameMesh.Alpha(Color.white, 0.35f + 0.2f * Mathf.Sin(Time.time * 10f)), 48);
        }
    }
}
