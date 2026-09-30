using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Arcane Spire's second guardian: an ice-crowned archmage. She splits the arena with glacial lances, calls a
    /// blizzard of hail down on each hero and spins icicle spirals from a distance; up close she bursts in a frost nova
    /// and batters heroes with her staff. She also takes the arena away: a whiteout freezes everything but one warm
    /// rune, frozen lanes leave only clear rows (a grid of safe squares when enraged), and a glacier cage closes in from
    /// the walls, herding the party to her. She summons arcane golems and wisps.
    /// </summary>
    public sealed class RimeArchmageBoss : ArcaneBossBehaviour
    {
        public const float WhiteoutTelegraph = 2.8f, WhiteoutDuration = 2.2f, WhiteoutReach = 8f, LaneWidth = 3f, CageWall = 2.5f, CageStep = 1.1f;
        public const float HailRadius = 2.4f, HailInterval = 0.5f, LanceLength = 13f, NovaReach = 3.2f, StaffReach = 3f, StaffCone = 120f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "ISOLDE, THE RIME ARCHMAGE";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(0.55f, 0.92f, 1f);
        protected override Sprite Body => ArcaneBossSprites.Archmage;
        protected override Sprite Details => ArcaneBossSprites.ArchmageDetails;
        protected override Color BodyTint => new Color(0.6f, 0.78f, 0.95f);
        protected override float Scale => 2.1f;
        protected override BoltKind Bolts => BoltKind.Frost;
        protected override HazardStyle Hazards => HazardStyle.Frost;
        protected override float ApproachDistance => 4.5f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "GLACIAL LANCES - FIND THE GAP", "BLIZZARD - WATCH FOR FALLING HAIL", "FROST NOVA - ROLL THROUGH THE RING",
            "ICICLE SPIRAL - WEAVE THROUGH", "SHATTERING STAFF - BACK AWAY FROM HER", "WHITEOUT - GET TO THE WARM RUNE",
            "FROZEN LANES - KEEP TO THE CLEAR ROWS", "GLACIER CAGE - GET TO THE CENTRE"
        };
        protected override byte[] Minions => minions;
        private static readonly byte[] minions = { DungeonRun.MinionBrute, DungeonRun.MinionBasic };
        private int step;
        private float angle, nextShot;
        public override int BaseHealth(int floor) => 26 + floor * 2;

        // Big hailstones: fewer of them, further apart, and a longer warning so each one can be escaped.
        private int HailFalls => IsEnraged ? 8 : 6;
        private int StaffBlows => IsEnraged ? 3 : 2;

        protected override float Attack(int index, Vector2 aim)
        {
            step = 0;
            Vector2 center = transform.position;
            switch (index)
            {
                case 0:
                    angle = Mathf.Atan2(aim.y, aim.x);
                    Lances(center, 0f, 0.9f);
                    return IsEnraged ? 3f : 2.2f;
                case 1: return 0.2f + HailFalls * HailInterval + 1.2f;
                case 2:
                    // A burst around her feet, then a ring of frost rolling outward.
                    for (int i = 0; i < 12; i++) Line(center, FlameMesh.Polar(i * Mathf.PI / 6f, 1f), NovaReach, 1.5f, 0.7f, 0.3f);
                    BurstAfter(0.7f, center, NovaReach);
                    Hazard(HazardShape.Ring, center, aim, 9f, 0.8f, 1f, 1.2f);
                    if (IsEnraged) Hazard(HazardShape.Ring, center, aim, 9f, 0.8f, 1.8f, 1.2f);
                    return IsEnraged ? 2.8f : 2f;
                case 3:
                    angle = Mathf.Atan2(aim.y, aim.x);
                    nextShot = 0.4f;
                    return 2.6f;
                case 5:
                    // The whole arena freezes but one warm rune, a run away from the party.
                    LockdownAwayFromParty(WhiteoutReach, IsEnraged ? 2.2f : 2.6f, WhiteoutTelegraph, WhiteoutDuration);
                    return WhiteoutTelegraph + WhiteoutDuration + 0.4f;
                case 6:
                    Lanes(Random.value < 0.5f, 1.3f, 3.5f);
                    return IsEnraged ? 6f : 5f;
                case 7: return 2f * CageStep + 1f + 2.6f + 0.4f;
                default:
                    PlanLunge(StrikeSpot(Run.NearestHero(center), 1.3f), 0.35f, 0.25f);
                    return 0.6f + StaffBlows * 0.6f + 1f;
            }
        }

        /// <summary>Frozen strips across the whole arena with clear rows between them, at a random offset.</summary>
        private void Lanes(bool horizontal, float telegraph, float duration)
        {
            var arena = DungeonMap.Arena;
            float span = horizontal ? arena.height : arena.width, offset = Random.Range(0f, LaneWidth * 2f);
            for (float d = offset - LaneWidth; d < span + LaneWidth; d += LaneWidth * 2f)
            {
                if (horizontal) Line(new Vector2(arena.xMin - 1f, arena.yMin - 0.5f + d), Vector2.right, arena.width + 1f, LaneWidth, telegraph, duration);
                else Line(new Vector2(arena.xMin - 0.5f + d, arena.yMin - 1f), Vector2.up, arena.height + 1f, LaneWidth, telegraph, duration);
            }
        }

        /// <summary>One ring of the glacier cage: four frost walls <paramref name="inset"/> in from the arena's edges.</summary>
        private void CageWalls(float inset, float telegraph, float duration)
        {
            var arena = DungeonMap.Arena;
            float left = arena.xMin - 0.5f + inset + CageWall * 0.5f, right = arena.xMax - 0.5f - inset - CageWall * 0.5f;
            float bottom = arena.yMin - 0.5f + inset + CageWall * 0.5f, top = arena.yMax - 0.5f - inset - CageWall * 0.5f;
            Line(new Vector2(arena.xMin - 1f, bottom), Vector2.right, arena.width + 1f, CageWall, telegraph, duration);
            Line(new Vector2(arena.xMin - 1f, top), Vector2.right, arena.width + 1f, CageWall, telegraph, duration);
            Line(new Vector2(left, arena.yMin - 1f), Vector2.up, arena.height + 1f, CageWall, telegraph, duration);
            Line(new Vector2(right, arena.yMin - 1f), Vector2.up, arena.height + 1f, CageWall, telegraph, duration);
        }

        /// <summary>Eight lances radiating from her, turned by <paramref name="turn"/> radians.</summary>
        private void Lances(Vector2 center, float turn, float telegraph)
        {
            for (int i = 0; i < 8; i++) Line(center, FlameMesh.Polar(angle + turn + i * Mathf.PI / 4f, 1f), LanceLength, 0.8f, telegraph, 0.4f);
        }

        protected override void AttackTick(float time)
        {
            Vector2 center = transform.position;
            switch (state)
            {
                case 1:
                    // A second wave through the gaps of the first, and enraged a third back through the first's.
                    if (step == 0 && time >= 1.1f) { step = 1; Lances(center, Mathf.PI / 8f, 0.8f); }
                    if (step == 1 && IsEnraged && time >= 2f) { step = 2; Lances(center, 0f, 0.7f); }
                    break;
                case 2:
                    if (step >= HailFalls || time < 0.2f + step * HailInterval) return;
                    step++;
                    foreach (Vector2 hero in LivingHeroPositions()) Scorch(hero + Random.insideUnitCircle * 2f, HailRadius, 1.1f, 0.3f);
                    break;
                case 7:
                    // Icicles chase heroes down the clear rows; enraged, crossing lanes leave only squares.
                    if (step < 3 && time >= 1.6f + step * 0.8f) { FanAt(center, AimAt(Run.NearestHero(center)), 3, 10f, 6f); step++; }
                    if (step == 3 && IsEnraged && time >= 2.4f) { step = 4; Lanes(Random.value < 0.5f, 1.4f, 2f); }
                    break;
                case 8:
                    // The cage closes one ring at a time; every ring holds until the last has struck.
                    if (step < 3 && time >= step * CageStep)
                    {
                        float strikes = step * CageStep + 1f, end = 2f * CageStep + 1f + 2.6f;
                        CageWalls(step * CageWall, 1f, end - strikes);
                        step++;
                    }
                    if (step == 3 && time >= 2f * CageStep + 1.4f) { step = 4; BoltRing(center, 10, 4.5f); }
                    break;
                case 4:
                    if (time < nextShot || time > 2.2f) return;
                    nextShot = time + 0.15f;
                    int arms = IsEnraged ? 4 : 3;
                    for (int k = 0; k < arms; k++) Fire(center, FlameMesh.Polar(angle + k * Mathf.PI * 2f / arms, 1f), 5.5f);
                    angle += 0.24f;
                    break;
                case 5:
                    // Lunge in, swing left and right, and bring the staff down in a shatter of ice.
                    TickLunge(time);
                    center = transform.position;
                    if (time < 0.6f) return;
                    if (step < StaffBlows && time >= 0.6f + step * 0.6f)
                    {
                        Vector2 aim = AimAt(Run.NearestHero(center));
                        Cleave(center, Quaternion.Euler(0, 0, step % 2 == 0 ? 20f : -20f) * aim, StaffReach, StaffCone, 0.4f);
                        step++;
                    }
                    else if (step == StaffBlows && time >= 0.6f + step * 0.6f)
                    {
                        Vector2 slam = center + AimAt(Run.NearestHero(center)) * 1.8f;
                        Scorch(slam, 1.5f, 0.55f, 0.3f);
                        BurstAfter(0.55f, slam, 1.5f);
                        step++;
                    }
                    break;
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            Vector2 face = Enemy.Facing.Direction.sqrMagnitude > 0.01f ? Enemy.Facing.Direction.normalized : Vector2.down;
            Vector2 side = Vector2.Perpendicular(face);
            // Her staff stands at her side, swung overhead through the staff combo.
            float sway = state == 5 ? Mathf.Sin(StateAge * 8f) * 1.1f : Mathf.Sin(Time.time * 1.3f) * 0.06f;
            Vector2 up = FlameMesh.Polar(Mathf.PI * 0.5f + sway, 1f);
            Staff(mesh, center + side * 0.8f + Vector2.down * 0.9f, up, 2.1f, new Color(0.55f, 0.7f, 0.9f), Accent, 0.22f + 0.06f * intensity);
            // Snowflake crystals drift around her in a slow tilted orbit, and frost mist curls at her hem.
            for (int i = 0; i < 5; i++)
            {
                float a = Time.time * 0.9f + i * Mathf.PI * 0.4f;
                Vector2 flake = center + new Vector2(Mathf.Cos(a) * 1.4f, Mathf.Sin(a) * 0.55f + 0.2f);
                float size = 0.1f + 0.05f * intensity;
                mesh.Bar(flake - Vector2.right * size * 1.4f, Vector2.right, size * 2.8f, 0.035f, Color.white, Color.white);
                mesh.Bar(flake - Vector2.up * size * 1.4f, Vector2.up, size * 2.8f, 0.035f, Color.white, Color.white);
                mesh.Diamond(flake, size * 0.6f, FlameMesh.Alpha(Accent, 0.9f));
            }
            mesh.Ellipse(center + Vector2.down * 0.95f, 1.2f, 0.35f, FlameMesh.Alpha(Accent, 0.25f * intensity), FlameMesh.Alpha(Accent, 0f));
        }
    }
}
