using UnityEngine;

namespace Slopgame
{
    /// <summary>Locks corridors in sequence; the gaps between scan lines are always traversable.</summary>
    public sealed class GridOverseerBoss : NeonBossBehaviour
    {
        public override string Title => "GRID OVERSEER";
        protected override Color Accent => WorldCatalog.Neon;
        protected override Sprite Chassis => NeonBossSprites.Overseer;
        protected override HazardStyle Hazards => HazardStyle.Circuit;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells = { "SCAN GRID - STEP BETWEEN THE LANES", "TRACE LOCK - LEAVE YOUR MARK", "FIREWALL - FOLLOW THE OPENING" };
        public override int BaseHealth(int floor) => 20 + floor * 2;
        protected override float Attack(int index, Vector2 aim)
        {
            Vector2 target = Run.NearestHero(transform.position);
            if (index == 0)
            {
                for (int i = -2; i <= 2; i++)
                    Beam(target + Vector2.right * i * 3f, Vector2.up, 24f, 1f + (i + 2) * 0.22f);
                if (IsEnraged) Beam(target, Vector2.right, 24f, 2.2f);
                return 3f;
            }
            if (index == 1)
            {
                foreach (Vector2 hero in LivingHeroPositions())
                    for (int i = 0; i < (IsEnraged ? 3 : 2); i++)
                        Beam(hero, Quaternion.Euler(0, 0, i * 60f) * aim, 15f, 1f + i * 0.5f);
                return 2.8f;
            }
            // Parallel lines ignite in order, giving a moving route through the firewall.
            for (int i = 0; i < 5; i++)
                Beam(target + Vector2.up * (i - 2) * 2.8f, Vector2.right, 25f, 0.9f + i * (IsEnraged ? 0.3f : 0.45f));
            return 3.5f;
        }
        protected override void DrawCircuitry(FlameMesh mesh, Vector2 center, float intensity)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = Quaternion.Euler(0, 0, i * 90f) * Vector2.right;
                Vector2 start = center + dir * 1.25f;
                mesh.Bar(start, Vector2.Perpendicular(dir), 0.65f, 0.09f, Accent * intensity, Accent);
                mesh.Diamond(start, 0.13f + 0.04f * Mathf.Sin(Time.time * 8f), Color.white);
            }
        }
    }
}
