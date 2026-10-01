using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Infernal Court's first guardian: a witch queen who fills the arena with rings of hex bolts, burns a
    /// pentagram into the floor around each hero, and blinks behind her target to fire at its back. She also takes the
    /// ground away: the Witching Hour burns everything but one warded circle, the Hex Web cuts the arena into wedges
    /// she fires down, and the Coven's Cage rings each hero in smouldering hexfire. She summons imps.
    /// </summary>
    public sealed class HexMatriarchBoss : InfernalBossBehaviour
    {
        public const float StarRadius = 2.4f;
        public const float VigilReach = 6.5f, VigilTelegraph = 2.4f, VigilDuration = 2.4f;
        public const float WebLength = 34f, WebTelegraph = 1f, WebHold = 3.2f, CageRadius = 4.2f, CageTelegraph = 1f, CageHold = 4.2f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "THE HEX MATRIARCH";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(0.8f, 0.35f, 1f);
        protected override Sprite Body => InfernalBossSprites.Matriarch;
        protected override Sprite Details => InfernalBossSprites.MatriarchDetails;
        protected override Color BodyTint => new Color(0.45f, 0.22f, 0.55f);
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "HEX CIRCLE - WEAVE THROUGH THE RINGS", "BINDING SIGIL - LEAVE THE STAR", "MIRROR STEP - SHE STRIKES FROM BEHIND",
            "WITCHING HOUR - REACH THE WARDED CIRCLE", "HEX WEB - PICK A GAP AND SIDESTEP", "COVEN'S CAGE - STAY INSIDE, KEEP MOVING"
        };
        protected override byte[] Minions => minions;
        private static readonly byte[] minions = { DungeonRun.MinionBasic, DungeonRun.MinionSpecialist + 1 };
        private int fired;
        private float webAngle;
        private int Spokes => IsEnraged ? 8 : 6;
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
            if (index == 3)
            {
                // Witching hour: the whole arena burns but one warded circle a run away.
                LockdownAwayFromParty(VigilReach, IsEnraged ? 2.2f : 2.6f, VigilTelegraph, VigilDuration);
                return VigilTelegraph + VigilDuration + 0.3f;
            }
            if (index == 4)
            {
                // Hex web: burning threads from her to the walls cut the arena into wedges, and they stay lit.
                webAngle = Random.value * Mathf.PI * 2f;
                for (int i = 0; i < Spokes; i++)
                    Line(transform.position, FlameMesh.Polar(webAngle + i * Mathf.PI * 2f / Spokes, 1f), WebLength, 0.7f, WebTelegraph, WebHold);
                return WebTelegraph + WebHold + 0.2f;
            }
            if (index == 5)
            {
                // Coven's cage: a ring of smouldering hexfire closes around each hero.
                const int pools = 8;
                foreach (Vector2 hero in LivingHeroPositions())
                    for (int k = 0; k < pools; k++)
                        Scorch(hero + FlameMesh.Polar(k * Mathf.PI * 2f / pools, CageRadius), 1.3f, CageTelegraph, CageHold);
                return CageTelegraph + CageHold;
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
            else if (state == 5)
            {
                // A bolt down the middle of every wedge of the web: there is room to sidestep, but not to run.
                int waves = IsEnraged ? 3 : 2;
                if (fired >= waves || time < WebTelegraph + 0.5f + fired * 0.9f) return;
                fired++;
                for (int i = 0; i < Spokes; i++) Fire(center, FlameMesh.Polar(webAngle + (i + 0.5f) * Mathf.PI * 2f / Spokes, 1f), 5.5f);
            }
            else if (state == 6)
            {
                // Inside the cage her mark falls where each hero stands, again and again.
                int marks = IsEnraged ? 4 : 3;
                if (fired >= marks || time < CageTelegraph + 0.3f + fired * 0.85f) return;
                fired++;
                foreach (Vector2 hero in LivingHeroPositions()) Scorch(hero, 1.3f, 0.7f, 0.8f);
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
