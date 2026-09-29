using UnityEngine;

namespace Slopgame
{
    /// <summary>Area denial artillery, with a broadside that leaves a safe central corridor.</summary>
    public sealed class SiegeEngineBoss : NeonBossBehaviour
    {
        public override string Title => "BASTION, THE SIEGE ENGINE";
        protected override Color Accent => new Color(1f, 0.65f, 0.15f);
        protected override Sprite Chassis => NeonBossSprites.Bastion;
        protected override HazardStyle Hazards => HazardStyle.Artillery;
        protected override float Scale => 2.2f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells = { "MISSILE WALK - KEEP MOVING", "CLUSTER MINES - FIND CLEAR GROUND", "BROADSIDE - STAY BETWEEN THE RAILS" };
        public override int BaseHealth(int floor) => 28 + floor * 2;
        protected override float Attack(int index, Vector2 aim)
        {
            if (index == 0)
            {
                foreach (Vector2 hero in LivingHeroPositions())
                    for (int i = 0; i < (IsEnraged ? 5 : 4); i++)
                        Hazard(HazardShape.Pool, hero + aim * (i - 1) * 2.4f, aim, 1.25f, 0f, 1f + i * 0.4f, 0.6f);
                return 3.5f;
            }
            if (index == 1)
            {
                Vector2 target = Run.NearestHero(transform.position);
                for (int i = 0; i < 6; i++)
                    Hazard(HazardShape.Pool, target + FlameMesh.Polar(i * Mathf.PI / 3f, 3.5f), aim, IsEnraged ? 1.5f : 1.2f, 0f, 1.3f, 2.5f);
                return 4.2f;
            }
            Vector2 side = Vector2.Perpendicular(aim);
            for (int i = -1; i <= 1; i += 2)
                Hazard(HazardShape.Beam, (Vector2)transform.position + side * i * 2f, aim, 22f, 1.4f, 1.2f, 1.1f);
            if (IsEnraged)
                Hazard(HazardShape.Pool, Run.NearestHero(transform.position), aim, 1.2f, 0f, 2.4f, 0.5f);
            return 3.3f;
        }
        protected override void DrawCircuitry(FlameMesh mesh, Vector2 center, float intensity)
        {
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    Vector2 start = center + new Vector2(side * 1.25f, i * 0.3f - 0.5f);
                    mesh.Bar(start, Vector2.right * side, 0.35f + 0.15f * Mathf.Sin(Time.time * 12f + i), 0.12f,
                        FlameMesh.Alpha(Accent, intensity), FlameMesh.Alpha(Accent, 0.1f));
                }
        }
    }
}
