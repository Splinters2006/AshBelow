using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Infernal Court's final guardian: a judge who chains the arena in a grid of hellfire, pours a double spiral of
    /// hex bolts from his scales, and brings a gavel of brimstone down on each hero in turn. He summons his cultists.
    /// </summary>
    public sealed class InfernalJudgeBoss : InfernalBossBehaviour
    {
        public const float GridSpacing = 5f, GavelInterval = 0.6f;
        public override string Title => "VASSAGO, THE INFERNAL JUDGE";
        protected override Color Accent => new Color(1f, 0.75f, 0.3f);
        protected override Sprite Body => InfernalBossSprites.Judge;
        protected override float Scale => 2.1f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells = { "CHAINS OF JUDGEMENT - FIND THE OPEN SQUARE", "SCALES OF TORMENT - WEAVE THE SPIRAL", "THE GAVEL FALLS - KEEP MOVING" };
        protected override byte[] Minions => minions;
        private static readonly byte[] minions = { DungeonRun.MinionSpecialist, DungeonRun.MinionBasic };
        private float nextShot, angle;
        private int strikes;
        public override int BaseHealth(int floor) => 26 + floor * 2;

        protected override float Attack(int index, Vector2 aim)
        {
            if (index == 0)
            {
                // Chains cross the arena in a grid; the squares between them are safe.
                var arena = DungeonMap.Arena;
                Vector2 target = Run.NearestHero(transform.position);
                int lines = IsEnraged ? 4 : 3;
                for (int i = 0; i < lines; i++)
                {
                    float offset = (i - (lines - 1) * 0.5f) * GridSpacing;
                    Line(new Vector2(target.x + offset, arena.yMin - 1f), Vector2.up, arena.height + 1f, 0.8f, 1f, 0.5f);
                    Line(new Vector2(arena.xMin - 1f, target.y + offset), Vector2.right, arena.width + 1f, 0.8f, 1.6f, 0.5f);
                }
                return 2.8f;
            }
            if (index == 1)
            {
                angle = Mathf.Atan2(aim.y, aim.x);
                nextShot = 0.4f;
                return 2.8f;
            }
            strikes = 0;
            return 0.2f + (IsEnraged ? 4 : 3) * GavelInterval + 0.9f;
        }

        protected override void AttackTick(float time)
        {
            Vector2 center = transform.position;
            if (state == 2)
            {
                if (time < nextShot || time > 2.4f) return;
                nextShot = time + 0.14f;
                int arms = IsEnraged ? 3 : 2;
                for (int k = 0; k < arms; k++) Fire(center, FlameMesh.Polar(angle + k * Mathf.PI * 2f / arms, 1f), 5f);
                angle += 0.26f;
            }
            else if (state == 3)
            {
                // The gavel follows each hero: where they stand now burns, and the scorch lingers.
                if (strikes >= (IsEnraged ? 4 : 3) || time < 0.2f + strikes * GavelInterval) return;
                strikes++;
                foreach (Vector2 hero in LivingHeroPositions()) Scorch(hero, 1.6f, 0.75f, 3f);
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            // A burning halo and a pair of swaying scales.
            mesh.Ring(center + Vector2.up * 1.3f, 0.45f, 0.07f, FlameMesh.Alpha(Accent, 0.6f + 0.4f * intensity), 32);
            float tilt = Mathf.Sin(Time.time * 1.7f) * 0.35f;
            Vector2 beam = FlameMesh.Polar(tilt, 1f);
            Vector2 left = center + Vector2.up * 0.35f - beam * 1.2f, right = center + Vector2.up * 0.35f + beam * 1.2f;
            mesh.Bar(left, beam, 2.4f, 0.06f, FlameMesh.Alpha(Accent, 0.8f), FlameMesh.Alpha(Accent, 0.8f));
            mesh.Disc(left + Vector2.down * 0.35f, 0.22f, FlameMesh.Alpha(Color.white, 0.8f * intensity), FlameMesh.Alpha(Accent, 0.5f), 16);
            mesh.Disc(right + Vector2.down * 0.35f, 0.22f, FlameMesh.Alpha(Color.white, 0.8f * intensity), FlameMesh.Alpha(Accent, 0.5f), 16);
        }
    }
}
