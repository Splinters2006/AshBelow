using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Infernal Court's final guardian: a judge who chains the arena in a tight grid of hellfire and fires into the
    /// squares it leaves, pours a double spiral of hex bolts from his scales while chains cross each hero, brings a
    /// gavel of brimstone down on each hero in turn, and closes bars in from both walls until only the centre aisle is
    /// left. He summons his cultists.
    /// </summary>
    public sealed class InfernalJudgeBoss : InfernalBossBehaviour
    {
        public const float GridSpacing = 3.5f, EnragedGridSpacing = 3f, GavelInterval = 0.6f;
        /// <summary>Closing bars: the aisle left open down the arena's middle, and the pace the bars close in at.</summary>
        public const float AisleWidth = 4f, EnragedAisleWidth = 3f, BarWidth = 2.4f, BarTelegraph = 1.1f, BarStep = 0.45f, BarHold = 1.9f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "VASSAGO, THE INFERNAL JUDGE";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(1f, 0.75f, 0.3f);
        protected override Sprite Body => InfernalBossSprites.Judge;
        protected override Sprite Details => InfernalBossSprites.JudgeDetails;
        protected override Color BodyTint => new Color(0.55f, 0.12f, 0.14f);
        protected override float Scale => 2.1f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "CHAINS OF JUDGEMENT - FIND THE OPEN SQUARE", "SCALES OF TORMENT - WEAVE THE SPIRAL", "THE GAVEL FALLS - KEEP MOVING",
            "CLOSING BARS - RUN TO THE CENTRE AISLE"
        };
        protected override byte[] Minions => minions;
        private static readonly byte[] minions = { DungeonRun.MinionSpecialist, DungeonRun.MinionBasic };
        private float nextShot, angle, barsClosed;
        private int strikes;
        public override int BaseHealth(int floor) => 26 + floor * 2;

        protected override float Attack(int index, Vector2 aim)
        {
            strikes = 0;
            if (index == 0)
            {
                // Chains cross the whole arena in a tight grid; only the small squares between them are safe.
                var arena = DungeonMap.Arena;
                Vector2 target = Run.NearestHero(transform.position);
                int lines = IsEnraged ? 9 : 7;
                float spacing = IsEnraged ? EnragedGridSpacing : GridSpacing;
                for (int i = 0; i < lines; i++)
                {
                    float offset = (i - (lines - 1) * 0.5f) * spacing;
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
            if (index == 3)
            {
                // Bars close in from both side walls, one after another, and stay shut: only the centre aisle is left.
                var arena = DungeonMap.Arena;
                float left = arena.xMin - 0.5f, right = arena.xMax - 0.5f;
                float span = (arena.width - (IsEnraged ? EnragedAisleWidth : AisleWidth)) * 0.5f;
                int bars = Mathf.CeilToInt(span / BarWidth);
                float width = span / bars;
                barsClosed = BarTelegraph + (bars - 1) * BarStep;
                for (int i = 0; i < bars; i++)
                {
                    float telegraph = BarTelegraph + i * BarStep, hold = barsClosed + BarHold - telegraph;
                    Line(new Vector2(left + (i + 0.5f) * width, arena.yMin - 1f), Vector2.up, arena.height + 1f, width, telegraph, hold);
                    Line(new Vector2(right - (i + 0.5f) * width, arena.yMin - 1f), Vector2.up, arena.height + 1f, width, telegraph, hold);
                }
                return barsClosed + BarHold + 0.3f;
            }
            return 0.2f + (IsEnraged ? 4 : 3) * GavelInterval + 0.9f;
        }

        protected override void AttackTick(float time)
        {
            Vector2 center = transform.position;
            if (state == 1)
            {
                // While the chains tighten he fires into the open squares.
                if (strikes >= (IsEnraged ? 3 : 2) || time < 0.6f + strikes * 0.8f) return;
                strikes++;
                FanAt(center, AimAt(Run.NearestHero(center)), IsEnraged ? 5 : 3, 16f, 6f);
            }
            else if (state == 2)
            {
                // Midway through the spiral a pair of chains crosses each hero.
                if (strikes == 0 && time >= 1f)
                {
                    strikes = 1;
                    var arena = DungeonMap.Arena;
                    foreach (Vector2 hero in LivingHeroPositions())
                    {
                        Line(new Vector2(hero.x, arena.yMin - 1f), Vector2.up, arena.height + 1f, 0.8f, 0.9f, 0.5f);
                        Line(new Vector2(arena.xMin - 1f, hero.y), Vector2.right, arena.width + 1f, 0.8f, 0.9f, 0.5f);
                    }
                }
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
            else if (state == 4)
            {
                // Once the bars are shut the gavel falls in the aisle, and a volley follows it down.
                if (strikes >= (IsEnraged ? 3 : 2) || time < barsClosed + 0.1f + strikes * 0.7f) return;
                strikes++;
                foreach (Vector2 hero in LivingHeroPositions()) Scorch(hero, 1.4f, 0.7f, 0.8f);
                if (IsEnraged) FanAt(center, AimAt(Run.NearestHero(center)), 3, 14f, 6f);
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
