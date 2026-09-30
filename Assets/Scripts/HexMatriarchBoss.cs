using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Infernal Court's first guardian: a witch queen who fills the arena with rings of hex bolts, burns a
    /// pentagram into the floor around each hero, and blinks behind her target to fire at its back. She summons imps.
    /// </summary>
    public sealed class HexMatriarchBoss : InfernalBossBehaviour
    {
        public const float StarRadius = 2.4f;
        public override string Title => "THE HEX MATRIARCH";
        protected override Color Accent => new Color(0.8f, 0.35f, 1f);
        protected override Sprite Body => InfernalBossSprites.Matriarch;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells = { "HEX CIRCLE - WEAVE THROUGH THE RINGS", "BINDING SIGIL - LEAVE THE STAR", "MIRROR STEP - SHE STRIKES FROM BEHIND" };
        protected override byte[] Minions => minions;
        private static readonly byte[] minions = { DungeonRun.MinionBasic, DungeonRun.MinionSpecialist + 1 };
        private int fired;
        public override int BaseHealth(int floor) => 18 + floor * 2;

        protected override float Attack(int index, Vector2 aim)
        {
            fired = 0;
            if (index == 0) return IsEnraged ? 3.2f : 2.6f;
            if (index == 1)
            {
                foreach (Vector2 hero in LivingHeroPositions())
                {
                    var points = new Vector2[5];
                    for (int k = 0; k < 5; k++) points[k] = hero + FlameMesh.Polar(Mathf.PI * 0.5f + k * Mathf.PI * 0.4f, StarRadius);
                    for (int k = 0; k < 5; k++)
                    {
                        Vector2 a = points[k], b = points[(k + 2) % 5];
                        Line(a, (b - a).normalized, Vector2.Distance(a, b), 0.5f, 1.1f, 0.5f);
                        // The star's points keep smouldering after it flares.
                        Scorch(a, 0.9f, 1.1f, 4f);
                    }
                    if (IsEnraged) Scorch(hero, 1.2f, 1.8f, 1f);
                }
                return 2.8f;
            }
            // Mirror step: out of hexfire on the far side of the target.
            Vector2 target = Run.NearestHero(transform.position);
            Vector2 through = (target - (Vector2)transform.position).normalized;
            BlinkTo(DungeonMap.ClampToArena(target + through * 3.5f, 1.5f));
            return IsEnraged ? 2.2f : 1.6f;
        }

        protected override void AttackTick(float time)
        {
            Vector2 center = transform.position;
            if (state == 1)
            {
                int waves = IsEnraged ? 4 : 3;
                if (fired >= waves || time < 0.5f + fired * 0.6f) return;
                for (int i = 0; i < 10; i++) Fire(center, FlameMesh.Polar((i * 36f + fired * 18f) * Mathf.Deg2Rad, 1f), 5.5f);
                fired++;
            }
            else if (state == 3)
            {
                // A fan at the hero's back, and when enraged a second blink and fan.
                if (fired == 0 && time >= 0.45f) { fired = 1; FanAt(center, (Run.NearestHero(center) - center).normalized, 5, 14f, 8f); }
                if (!IsEnraged) return;
                if (fired == 1 && time >= 1.1f)
                {
                    fired = 2;
                    Vector2 target = Run.NearestHero(center);
                    BlinkTo(DungeonMap.ClampToArena(target + (target - center).normalized * 3.5f, 1.5f));
                }
                if (fired == 2 && time >= 1.5f) { fired = 3; FanAt(center, (Run.NearestHero(center) - center).normalized, 5, 14f, 8f); }
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            mesh.Ring(center, 1.5f, 0.05f, FlameMesh.Alpha(Accent, 0.35f * intensity), 40);
            for (int i = 0; i < 3; i++)
            {
                Vector2 sigil = center + FlameMesh.Polar(Time.time * 1.8f + i * Mathf.PI * 2f / 3f, 1.5f);
                mesh.Diamond(sigil, 0.16f + 0.05f * Mathf.Sin(Time.time * 6f + i), FlameMesh.Alpha(Accent, 0.6f + 0.4f * intensity));
                mesh.Diamond(sigil, 0.07f, Color.white);
            }
        }
    }
}
