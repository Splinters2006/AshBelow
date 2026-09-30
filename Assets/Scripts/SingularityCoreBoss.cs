using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// An orbital bullet pattern fight: rotating spokes, paired satellites and offset pulse waves. It also phases in
    /// beside heroes and implodes, drifts to the arena's heart and swallows the middle (the edges are safe), and turns
    /// a cross of beams around itself that the party has to follow through the gaps.
    /// </summary>
    public sealed class SingularityCoreBoss : NeonBossBehaviour
    {
        public const float ImplodeRadius = 2.6f, HorizonRadius = 7f, CrossLength = 34f, CrossWidth = 2.2f, CrossStep = 0.35f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "NULL, THE SINGULARITY CORE";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(0.75f, 0.35f, 1f);
        protected override Sprite Chassis => GuardianSprites.Core;
        protected override Sprite Details => GuardianSprites.CoreDetails;
        protected override Color BodyTint => new Color(0.42f, 0.4f, 0.55f);
        protected override HazardStyle Hazards => HazardStyle.Void;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "SPIRAL STREAM - WEAVE THROUGH THE SPOKES", "SATELLITE CROSSFIRE - WATCH BOTH SIDES", "PULSE ECHO - DODGE EACH WAVE",
            "PHASE SHIFT - IT APPEARS BESIDE YOU", "EVENT HORIZON - STAY AT THE EDGE", "ORBITAL CROSS - FOLLOW THE GAP"
        };
        private float nextShot, angle;
        private int volley, step;
        public override int BaseHealth(int floor) => 26 + floor * 2;

        private int Phases => IsEnraged ? 4 : 3;
        private int CrossTurns => IsEnraged ? 10 : 8;

        protected override float Attack(int index, Vector2 aim)
        {
            angle = Mathf.Atan2(aim.y, aim.x); volley = 0; step = 0;
            nextShot = 1f;
            switch (index)
            {
                case 2:
                    for (int i = 0; i < (IsEnraged ? 3 : 2); i++)
                        Hazard(HazardShape.Ring, transform.position, aim, 17f, 0.65f, 1f + i * 0.85f, 2.8f);
                    return 5.6f;
                case 3: return 0.3f + Phases * 0.9f + 0.5f;
                case 4:
                    // Drift to the arena's heart, then the middle collapses.
                    PlanLunge(DungeonMap.Arena.center - Vector2.one * 0.5f, 0f, 0.6f);
                    return 5.2f;
                case 5: return 0.5f + CrossTurns * CrossStep + 0.9f;
                default: return 4f;
            }
        }

        protected override void AttackTick(float time)
        {
            Vector2 center = transform.position;
            switch (state)
            {
                case 1:
                case 2:
                    if (time < nextShot || volley >= (IsEnraged ? 9 : 7)) return;
                    nextShot = time + 0.32f;
                    if (state == 1)
                    {
                        for (int i = 0; i < 4; i++) Fire(center, FlameMesh.Polar(angle + i * Mathf.PI * 0.5f, 1f), 5f);
                        angle += 0.22f;
                    }
                    else
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 origin = center + Vector2.right * side * 2.4f;
                            Fire(origin, (Run.NearestHero(origin) - origin).normalized, IsEnraged ? 7f : 5.5f);
                        }
                    volley++;
                    break;
                case 4:
                    // Out of phase, into the space beside a hero, and it implodes.
                    if (step >= Phases || time < 0.3f + step * 0.9f) return;
                    step++;
                    BlinkTo(StrikeSpot(Run.NearestHero(center), 1.5f));
                    Scorch(transform.position, ImplodeRadius, 0.55f, 0.3f);
                    BurstAfter(0.55f, transform.position, ImplodeRadius);
                    break;
                case 5:
                    if (step == 0 && TickLunge(time))
                    {
                        step = 1;
                        center = transform.position;
                        Scorch(center, HorizonRadius, 1.4f, 2.2f);
                        BurstAfter(1.4f, center, HorizonRadius);
                    }
                    // The collapse throws off rings to roll through at the edges.
                    if (step == 1 && time >= 2.6f) { step = 2; Hazard(HazardShape.Ring, center, Vector2.right, 17f, 0.65f, 0.6f, 2.4f); }
                    if (step == 2 && IsEnraged && time >= 3.4f) { step = 3; BoltRing(center, 16, 5f); }
                    break;
                case 6:
                    // A cross of beams turns around it in steps; each step is marked just before it fires.
                    if (step >= CrossTurns || time < 0.5f + step * CrossStep) return;
                    int arms = IsEnraged ? 3 : 2;
                    float turn = angle + step * 0.3f;
                    for (int k = 0; k < arms; k++) Beam(center, FlameMesh.Polar(turn + k * Mathf.PI / arms, 1f), CrossLength, 0.5f, CrossWidth);
                    step++;
                    break;
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            // A spinning ring and three orbiting shards; they draw in tight while it collapses space.
            float pull = state == 5 ? 0.6f : 1f;
            mesh.Ring(center, 1.1f * pull, 0.09f, FlameMesh.Alpha(Accent, intensity), 48);
            for (int i = 0; i < 3; i++)
            {
                Vector2 orbit = center + FlameMesh.Polar(Time.time * (state == 5 ? 5f : 1.8f) + i * Mathf.PI * 2f / 3f, 1.4f * pull);
                mesh.Diamond(orbit, 0.22f, Accent);
            }
            if (state == 2)
                for (int side = -1; side <= 1; side += 2)
                    mesh.Ring(center + Vector2.right * side * 2.4f, 0.4f, 0.12f, Accent, 20);
            // Phasing leaves a flicker of afterimage rings.
            if (state == 4)
                mesh.Ring(center, 1.6f + Mathf.Repeat(StateAge * 3f, 1f), 0.05f, FlameMesh.Alpha(Color.white, 1f - Mathf.Repeat(StateAge * 3f, 1f)), 40);
        }
    }
}
