using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Infernal Court's second guardian: a colossal hellhound that charges across the arena leaving burning
    /// ground behind it, sweeps a fan of molten breath, and erupts in shockwaves and falling brimstone. It also floods
    /// three quarters of the arena with magma, and opens a caldera that burns everything but a ring of ground around
    /// itself, too close to its own blast for comfort. It calls its pack.
    /// </summary>
    public sealed class BrimstoneHoundBoss : InfernalBossBehaviour
    {
        public const float ChargeWindup = 0.9f, ChargeTime = 0.35f;
        public const float FloodTelegraph = 2.6f, FloodDuration = 2.2f, CalderaRadius = 6.5f, CalderaBlast = 3.2f, CalderaTelegraph = 2.4f, CalderaDuration = 2.4f;
        /// <summary>The guardian's name (the encyclopedia reads it outside a fight).</summary>
        public const string FixedTitle = "GORGOTH, THE BRIMSTONE HOUND";
        public override string Title => FixedTitle;
        protected override Color Accent => new Color(1f, 0.42f, 0.15f);
        protected override Sprite Body => InfernalBossSprites.Hound;
        protected override Sprite Details => InfernalBossSprites.HoundDetails;
        protected override Color BodyTint => new Color(0.42f, 0.16f, 0.14f);
        protected override float Scale => 2.2f;
        public override float HitRadius => 1f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells =
        {
            "BRIMSTONE CHARGE - SIDESTEP THE LINE", "MOLTEN BREATH - GET BEHIND HIM", "ERUPTION - ROLL THE SHOCKWAVES",
            "MAGMA FLOOD - RUN TO THE DRY GROUND", "CALDERA - CLOSE IN, BUT NOT TOO CLOSE"
        };
        protected override byte[] Minions => minions;
        private static readonly byte[] minions = { DungeonRun.MinionBrute, DungeonRun.MinionBasic };
        private Vector2 chargeFrom, chargeTo;
        private float chargeAt;
        private int charges;
        private bool landed;
        public override int BaseHealth(int floor) => 24 + floor * 2;

        protected override float Attack(int index, Vector2 aim)
        {
            Vector2 center = transform.position;
            if (index == 0)
            {
                charges = 0;
                PlanCharge(aim, 0f);
                return IsEnraged ? 3f : 2f;
            }
            if (index == 1)
            {
                Vector2 mouth = center + aim * 1.2f;
                int count = IsEnraged ? 7 : 5;
                // The breath sweeps from one side to the other.
                for (int i = 0; i < count; i++)
                    Line(mouth, Quaternion.Euler(0, 0, -40f + 80f * i / (count - 1)) * aim, 9f, 0.9f, 0.7f + i * 0.12f, 0.35f);
                return 2.2f;
            }
            if (index == 3)
            {
                // Magma floods three quarters of the arena; brimstone still falls on the dry quarter, so nobody rests there.
                ScreenFx.Shake(0.25f, 0.5f);
                var (right, top) = QuadrantAwayFromParty();
                Vector2 dry = LockAllButQuadrant(right, top, FloodTelegraph, FloodDuration);
                for (int i = 0; i < (IsEnraged ? 3 : 2); i++)
                    Scorch(dry + Random.insideUnitCircle * 3.5f, 1.1f, FloodTelegraph + 0.5f + i * 0.5f, 1.2f);
                return FloodTelegraph + FloodDuration + 0.3f;
            }
            if (index == 4)
            {
                // The caldera burns everything beyond a circle around the hound, and the ground at its feet blows too:
                // the only safe footing is the band between the two.
                ScreenFx.Shake(0.25f, 0.5f);
                Hazard(HazardShape.Inferno, center, Vector2.up, CalderaRadius, 0f, CalderaTelegraph, CalderaDuration);
                Scorch(center, CalderaBlast, CalderaTelegraph + 0.4f, 0.6f);
                BurstAfter(CalderaTelegraph + 0.4f, center, CalderaBlast);
                // Enraged, a shockwave rolls out through the band as well.
                if (IsEnraged) Hazard(HazardShape.Ring, center, aim, CalderaRadius + 1f, 0.8f, CalderaTelegraph + 1.3f, 1f);
                return CalderaTelegraph + CalderaDuration + 0.3f;
            }
            ScreenFx.Shake(0.25f, 0.4f);
            Hazard(HazardShape.Ring, center, aim, 9f, 0.8f, 0.6f, 1.2f);
            Hazard(HazardShape.Ring, center, aim, 9f, 0.8f, 1.5f, 1.2f);
            var arena = DungeonMap.Arena;
            for (int i = 0; i < (IsEnraged ? 7 : 5); i++)
            {
                var spot = new Vector2(Random.Range(arena.xMin + 1f, arena.xMax - 2f), Random.Range(arena.yMin + 1f, arena.yMax - 2f));
                Scorch(spot, 1.2f, 1.3f + i * 0.12f, 3.5f);
            }
            return 3.2f;
        }

        /// <summary>Marks the charge lane (the lane itself is the strike) and when the hound will run it.</summary>
        private void PlanCharge(Vector2 aim, float startsAt)
        {
            chargeFrom = transform.position;
            chargeTo = DungeonMap.ClampToArena(chargeFrom + aim * 14f, 1.2f);
            float length = Vector2.Distance(chargeFrom, chargeTo);
            chargeAt = startsAt + ChargeWindup;
            landed = false;
            if (length > 0.5f) Line(chargeFrom, (chargeTo - chargeFrom) / length, length, 1.4f, ChargeWindup, ChargeTime);
        }

        protected override void AttackTick(float time)
        {
            if (state != 1 || landed || time < chargeAt) return;
            if (time < chargeAt + ChargeTime)
            {
                transform.position = Vector2.Lerp(chargeFrom, chargeTo, (time - chargeAt) / ChargeTime);
                return;
            }
            transform.position = chargeTo;
            landed = true;
            charges++;
            // Burning pawprints along the whole lane.
            float length = Vector2.Distance(chargeFrom, chargeTo);
            for (float d = 1f; d < length; d += 2f) Scorch(Vector2.Lerp(chargeFrom, chargeTo, d / length), 0.9f, 0.25f, 4.5f);
            // Enraged, it wheels round for a second run at the nearest hero.
            if (IsEnraged && charges == 1)
            {
                Vector2 aim = (Run.NearestHero(transform.position) - (Vector2)transform.position).normalized;
                PlanCharge(aim.sqrMagnitude > 0.01f ? aim : Vector2.down, time + 0.2f);
            }
        }

        protected override void DrawAura(FlameMesh mesh, Vector2 center, float intensity)
        {
            // A mane of brimstone flames along its back.
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * 0.32f;
                mesh.Flame(center + new Vector2(x, 0.95f - Mathf.Abs(x) * 0.4f), Vector2.up, 0.34f, 0.55f + 0.35f * intensity + 0.15f * Mathf.Sin(Time.time * 7f + i),
                    FlameMesh.Hash(i, 2.7f), 0.8f);
            }
        }
    }
}
