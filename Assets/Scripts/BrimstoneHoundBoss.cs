using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Infernal Court's second guardian: a colossal hellhound that charges across the arena leaving burning
    /// ground behind it, sweeps a fan of molten breath, and erupts in shockwaves and falling brimstone. It calls its pack.
    /// </summary>
    public sealed class BrimstoneHoundBoss : InfernalBossBehaviour
    {
        public const float ChargeWindup = 0.9f, ChargeTime = 0.35f;
        public override string Title => "GORGOTH, THE BRIMSTONE HOUND";
        protected override Color Accent => new Color(1f, 0.42f, 0.15f);
        protected override Sprite Body => InfernalBossSprites.Hound;
        protected override float Scale => 2.2f;
        public override float HitRadius => 1f;
        protected override string[] AttackTells => tells;
        private static readonly string[] tells = { "BRIMSTONE CHARGE - SIDESTEP THE LINE", "MOLTEN BREATH - GET BEHIND HIM", "ERUPTION - ROLL THE SHOCKWAVES" };
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
