using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Arcane Spire's first guardian: a hooded battle mage with a runic sword. He fights up close, lunging into
    /// cleaves and blinking beside a hero to thrust, and between blows fires volleys of arcane bolts, rains conjured
    /// swords on the party and whirls in a storm of blade light. He summons wisps and blink magi.
    /// </summary>
    public sealed class SpellbladeBoss : ArcaneBossBehaviour
    {
        public const float CleaveReach = 3.2f, CleaveCone = 110f, ThrustLength = 7f, WhirlRadius = 2.8f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "SELWYN, THE SPELLBLADE";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(0.45f, 0.82f, 1f);
        protected override Sprite Body => ArcaneBossSprites.Spellblade;
        protected override Sprite Details => ArcaneBossSprites.SpellbladeDetails;
        protected override Color BodyTint => new Color(0.3f, 0.42f, 0.85f);
        protected override float Scale => 1.9f;
        protected override float ApproachDistance => 2.4f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "RUNIC CLEAVE - STAY OUT OF HIS REACH", "BLINK THRUST - SIDESTEP THE BLADE", "SPELL VOLLEY - SLIP BETWEEN THE BOLTS",
            "SWORD RAIN - KEEP MOVING", "ARCANE WHIRL - GET CLEAR OF HIM"
        };
        protected override byte[] Minions => minions;
        private static readonly byte[] minions = { DungeonRun.MinionBasic, DungeonRun.MinionSpecialist };
        private int step;
        public override int BaseHealth(int floor) => 22 + floor * 2;

        private int Swings => IsEnraged ? 3 : 2;
        private int Thrusts => IsEnraged ? 2 : 1;
        private int Volleys => IsEnraged ? 4 : 3;
        private int Rains => IsEnraged ? 5 : 4;

        protected override float Attack(int index, Vector2 aim)
        {
            step = 0;
            Vector2 center = transform.position;
            switch (index)
            {
                case 0:
                    PlanLunge(StrikeSpot(Run.NearestHero(center), 1.4f), 0.35f, 0.22f);
                    return Swings * 1.05f + 0.3f;
                case 1: return 0.3f + Thrusts * 1f + 0.4f;
                case 2: return 0.4f + Volleys * 0.5f + 0.3f;
                case 3:
                    for (int i = 0; i < 4; i++) Scorch(RandomArenaSpot(), 1.1f, 0.9f + i * 0.15f, 0.35f);
                    return 0.3f + Rains * 0.45f + 0.9f;
                default:
                    Scorch(center, WhirlRadius, 0.8f, 0.35f);
                    SlashAfter(0.8f, center, aim, WhirlRadius, 360f);
                    if (IsEnraged) PlanLunge(StrikeSpot(Run.NearestHero(center), 0.5f), 1.1f, 0.3f);
                    return IsEnraged ? 2.6f : 1.4f;
            }
        }

        protected override void AttackTick(float time)
        {
            Vector2 center = transform.position;
            switch (state)
            {
                case 1:
                    // Lunge, cleave, and lunge again at whoever is nearest.
                    if (!TickLunge(time)) return;
                    Vector2 landed = transform.position;
                    Cleave(landed, AimAt(Run.NearestHero(landed)), CleaveReach, CleaveCone, 0.45f);
                    if (++step < Swings) PlanLunge(StrikeSpot(Run.NearestHero(landed), 1.4f), time + 0.6f, 0.22f);
                    break;
                case 2:
                    // Out of a blink at the target's side, a thrust of blade light straight through it.
                    if (step >= Thrusts || time < 0.3f + step * 1f) return;
                    step++;
                    Vector2 target = Run.NearestHero(center);
                    Vector2 side = Vector2.Perpendicular(AimAt(target)) * (Random.value < 0.5f ? -1f : 1f);
                    BlinkTo(DungeonMap.ClampToArena(target + side * 3f, 1.2f));
                    Vector2 from = transform.position, thrust = AimAt(target);
                    Line(from, thrust, ThrustLength, 0.9f, 0.55f, 0.25f);
                    SlashAfter(0.55f, from + thrust * 1.2f, thrust, 3f, 40f);
                    break;
                case 3:
                    if (step >= Volleys || time < 0.4f + step * 0.5f) return;
                    FanAt(center, AimAt(Run.NearestHero(center)), 7, 12f, 7.5f);
                    step++;
                    break;
                case 4:
                    // Swords fall where each hero stands, a beat behind them.
                    if (step >= Rains || time < 0.3f + step * 0.45f) return;
                    step++;
                    foreach (Vector2 hero in LivingHeroPositions()) Scorch(hero + Random.insideUnitCircle * 0.6f, 1.1f, 0.8f, 0.35f);
                    break;
                case 5:
                    if (step == 0 && time >= 0.8f) { step = 1; BoltRing(center, 12, 6f); }
                    if (step == 1 && TickLunge(time))
                    {
                        step = 2;
                        Scorch(transform.position, WhirlRadius, 0.6f, 0.35f);
                        SlashAfter(0.6f, transform.position, Vector2.right, WhirlRadius, 360f);
                    }
                    break;
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            Vector2 face = Enemy.Facing.Direction.sqrMagnitude > 0.01f ? Enemy.Facing.Direction.normalized : Vector2.down;
            float held = Mathf.Atan2(face.y, face.x) + 0.9f;
            // The runic sword: held forward at rest, swinging through cleaves and thrusts, spinning in the whirl.
            float angle = state == 5 ? StateAge * 14f
                : state == 1 || state == 2 ? held + Mathf.Sin(StateAge * 9f) * 1.3f
                : held + Mathf.Sin(Time.time * 2f) * 0.08f;
            Vector2 blade = FlameMesh.Polar(angle, 1f);
            Vector2 hilt = center + Vector2.Perpendicular(face) * -0.55f + Vector2.down * 0.15f;
            Color glow = FlameMesh.Alpha(Accent, 0.5f + 0.5f * intensity);
            mesh.Bar(hilt, blade, 1.7f, 0.3f, FlameMesh.Alpha(Accent, 0.35f * intensity), FlameMesh.Alpha(Accent, 0f));
            mesh.Bar(hilt, blade, 1.5f, 0.13f, glow, FlameMesh.Alpha(Color.white, 0.9f));
            mesh.Bar(hilt, blade, 1.35f, 0.05f, Color.white, Color.white);
            mesh.Bar(hilt - Vector2.Perpendicular(blade) * 0.2f, Vector2.Perpendicular(blade), 0.4f, 0.07f, new Color(0.95f, 0.78f, 0.35f), new Color(0.95f, 0.78f, 0.35f));
            // Three runes turn around him, brighter as he winds up.
            for (int i = 0; i < 3; i++)
            {
                Vector2 rune = center + new Vector2(Mathf.Cos(Time.time * 1.4f + i * 2.09f) * 1.3f, Mathf.Sin(Time.time * 1.4f + i * 2.09f) * 0.5f - 0.7f);
                mesh.Diamond(rune, 0.13f + 0.04f * intensity, FlameMesh.Alpha(Accent, 0.5f + 0.5f * intensity));
                mesh.Diamond(rune, 0.05f, Color.white);
            }
        }
    }
}
