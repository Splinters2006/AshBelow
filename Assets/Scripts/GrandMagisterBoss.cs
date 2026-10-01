using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Arcane Spire's final guardian: the Grand Magister, master of the spire. He calls arcane meteors down on the
    /// party, sweeps a prismatic beam around the arena and fires rings of bolts from himself and two mirror images; up
    /// close he whips up a great maelstrom around himself and blinks to heroes to bring his staff down where they stand.
    /// His last word is Annihilation: the whole arena is unmade except one sanctum, and anyone caught outside it dies.
    /// He summons blink magi, orbiting eyes and apprentices.
    /// </summary>
    public sealed class GrandMagisterBoss : ArcaneBossBehaviour
    {
        public const float MaelstromRadius = 5.2f, SmashRadius = 1.7f, MirrorOffset = 3f, BeamLength = 12f;
        /// <summary>Annihilation: how far the sanctum lies from the party, how long they have to reach it, and how long the arena kills.</summary>
        public const float AnnihilationReach = 6f, AnnihilationTelegraph = 3f, AnnihilationDuration = 0.9f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "AURELION, THE GRAND MAGISTER";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(0.8f, 0.6f, 1f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.35f);
        protected override Sprite Body => ArcaneBossSprites.Magister;
        protected override Sprite Details => ArcaneBossSprites.MagisterDetails;
        protected override Color BodyTint => new Color(0.4f, 0.25f, 0.7f);
        protected override float Scale => 2.3f;
        public override float HitRadius => 0.95f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "ARCANE METEORS - LEAVE THE CIRCLES", "PRISMATIC SWEEP - STAY BEHIND THE BEAM", "ARCANE MAELSTROM - GET AWAY FROM HIM",
            "STAFF OF AGES - HE STRIKES WHERE YOU STAND", "MIRROR BARRAGE - WEAVE THROUGH THE RINGS",
            "ANNIHILATION - REACH THE SANCTUM OR DIE"
        };
        protected override byte[] Minions => minions;
        private static readonly byte[] minions = { DungeonRun.MinionSpecialist, DungeonRun.MinionSpecialist + 1, DungeonRun.MinionCaster };
        private int step;
        public override int BaseHealth(int floor) => 30 + floor * 2;

        private int MeteorVolleys => IsEnraged ? 4 : 3;
        private int Smashes => IsEnraged ? 3 : 2;

        protected override float Attack(int index, Vector2 aim)
        {
            step = 0;
            Vector2 center = transform.position;
            switch (index)
            {
                case 0:
                    for (int i = 0; i < 4; i++) Scorch(RandomArenaSpot(), 1.6f, 1.2f + i * 0.2f, 0.5f);
                    return MeteorVolleys * 0.7f + 1.4f;
                case 1:
                {
                    // The beam sweeps three-quarters of the way round, starting behind the target's left; enraged a twin beam sweeps opposite.
                    const int beams = 18;
                    float start = Mathf.Atan2(aim.y, aim.x) - Mathf.PI * 0.75f;
                    for (int i = 0; i < beams; i++)
                    {
                        Vector2 direction = FlameMesh.Polar(start + Mathf.PI * 1.5f * i / (beams - 1), 1f);
                        Line(center, direction, BeamLength, 0.9f, 0.6f + i * 0.09f, 0.3f);
                        if (IsEnraged) Line(center, -direction, BeamLength, 0.9f, 0.6f + i * 0.09f, 0.3f);
                    }
                    return 2.8f;
                }
                case 2:
                    Scorch(center, MaelstromRadius, 0.9f, 0.4f);
                    SlashAfter(0.9f, center, aim, MaelstromRadius, 360f);
                    BurstAfter(0.9f, center, MaelstromRadius);
                    return IsEnraged ? 2.6f : 1.8f;
                case 3: return 0.3f + Smashes * 1f + 0.6f;
                case 4: return 0.4f + 4 * 0.5f + 0.2f;
                default:
                    // Everything outside the sanctum is unmade in one killing flash.
                    ScreenFx.Shake(0.3f, 0.6f);
                    LockdownAwayFromParty(AnnihilationReach, IsEnraged ? 2.2f : 2.6f, AnnihilationTelegraph, AnnihilationDuration, true);
                    return AnnihilationTelegraph + AnnihilationDuration + 0.5f;
            }
        }

        protected override void AttackTick(float time)
        {
            Vector2 center = transform.position;
            switch (state)
            {
                case 1:
                    if (step >= MeteorVolleys || time < step * 0.7f) return;
                    step++;
                    foreach (Vector2 hero in LivingHeroPositions()) Scorch(hero, 1.8f, 1.1f, 0.5f);
                    break;
                case 3:
                    // Bolts spray out of the maelstrom, and enraged it whips up a second time.
                    if (step == 0 && time >= 1.1f) { step = 1; BoltRing(center, 20, 6f); }
                    if (step == 1 && IsEnraged && time >= 1.3f)
                    {
                        step = 2;
                        Scorch(center, MaelstromRadius, 0.7f, 0.4f);
                        SlashAfter(0.7f, center, Vector2.right, MaelstromRadius, 360f);
                        BurstAfter(0.7f, center, MaelstromRadius);
                    }
                    break;
                case 4:
                    // Out of a blink beside the target, the staff comes down where it stands and a shockwave rolls out.
                    if (step >= Smashes || time < 0.3f + step * 1f) return;
                    step++;
                    Vector2 target = Run.NearestHero(center);
                    BlinkTo(StrikeSpot(target, 1.3f));
                    Scorch(target, SmashRadius, 0.6f, 0.35f);
                    BurstAfter(0.6f, target, SmashRadius);
                    Hazard(HazardShape.Ring, target, Vector2.right, 6f, 0.7f, 0.6f, 0.9f);
                    break;
                case 5:
                    // Rings of bolts from him and two mirror images at his sides, each wave turned half a step.
                    if (step >= 4 || time < 0.4f + step * 0.5f) return;
                    Vector2 side = Vector2.Perpendicular(Enemy.Facing.Direction.normalized) * MirrorOffset;
                    float offset = step * 15f;
                    BoltRing(center, 10, 5f, offset);
                    BoltRing(center + side, 8, 5f, offset + 22f);
                    BoltRing(center - side, 8, 5f, offset + 22f);
                    step++;
                    break;
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            Vector2 face = Enemy.Facing.Direction.sqrMagnitude > 0.01f ? Enemy.Facing.Direction.normalized : Vector2.down;
            Vector2 side = Vector2.Perpendicular(face);
            // His great staff, raised high while he casts and brought down in the smash.
            float lift = state == 4 ? Mathf.Sin(Mathf.Clamp01(StateAge % 1f / 0.6f) * Mathf.PI) * 0.9f : IsCharging ? 0.3f : 0f;
            Vector2 up = FlameMesh.Polar(Mathf.PI * 0.5f - lift * 0.8f, 1f);
            Staff(mesh, center + side * 1f + Vector2.down * 1f, up, 2.6f, new Color(0.35f, 0.22f, 0.15f), Accent, 0.28f + 0.08f * intensity);
            mesh.Ring(center + side * 1f + Vector2.down * 1f + up * 2.6f, 0.34f, 0.05f, FlameMesh.Alpha(Gold, 0.9f), 20);
            // Two arcane orbs circle him, and gold motes rise from his hat.
            for (int i = 0; i < 2; i++)
            {
                float a = Time.time * 1.6f + i * Mathf.PI;
                Vector2 orb = center + new Vector2(Mathf.Cos(a) * 1.5f, Mathf.Sin(a) * 0.6f + 0.1f);
                mesh.Disc(orb, 0.3f, FlameMesh.Alpha(Accent, 0.5f * intensity), FlameMesh.Alpha(Accent, 0f), 20);
                mesh.Disc(orb, 0.13f, Color.white, FlameMesh.Alpha(Accent, 0.9f), 16);
            }
            for (int i = 0; i < 4; i++)
            {
                float rise = (Time.time * 0.5f + i * 0.25f) % 1f;
                mesh.Diamond(center + new Vector2(Mathf.Sin(i * 2.3f + Time.time) * 0.5f, 0.9f + rise * 1.2f), 0.06f * (1f - rise), FlameMesh.Alpha(Gold, 1f - rise));
            }
            // During the mirror barrage his two images shimmer at his sides.
            if (state == 5)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 image = center + side * MirrorOffset * s;
                    mesh.Disc(image, 0.9f, FlameMesh.Alpha(Accent, 0.35f + 0.15f * Mathf.Sin(Time.time * 12f)), FlameMesh.Alpha(Accent, 0f), 24);
                    mesh.Ring(image, 0.7f, 0.05f, FlameMesh.Alpha(Color.white, 0.6f), 24);
                    mesh.Diamond(image, 0.2f, Color.white);
                }
        }
    }
}
