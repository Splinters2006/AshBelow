using UnityEngine;

namespace Slopgame
{
    /// <summary>The first guardian: a slow caster alternating an aimed ember fan and a full nova.</summary>
    public sealed class AshWardenBoss : BossBehaviour
    {
        private float readyAt, fireAt;
        private bool charging;
        private int pattern;
        private Vector2 lockedAim;

        public override string Title => "THE ASH WARDEN";
        public override string Tell => charging ? pattern % 2 == 0 ? "EMBER FAN - SIDESTEP" : "NOVA - KEEP MOVING" : IsEnraged ? "ENRAGED" : "GUARDIAN OF THE RELIC";
        public override int BaseHealth(int floor) => 24 + floor * 3;
        public override bool IsCharging => charging;
        public override byte NetState => (byte)(pattern % 2);

        protected override void OnSetup()
        {
            Enemy.Speed = 1.5f;
            readyAt = Enemy.ActionTime + 2f;
            DungeonVisuals.DecorateBoss(transform);
        }

        public override Color BodyColor() => Flashing(IsEnraged ? new Color(1f, 0.26f, 0.28f) : new Color(0.65f, 0.3f, 0.55f), AbilityCatalog.Gold, charging);

        public override void HostTick(Vector2 offset)
        {
            Enemy.Facing.TurnToward(offset, Time.deltaTime * Enemy.ActionSpeedMultiplier);
            if (charging)
            {
                if (Enemy.ActionTime < fireAt) return;
                int shots = pattern % 2 == 0 ? (IsEnraged ? 7 : 5) : (IsEnraged ? 16 : 12);
                for (int i = 0; i < shots; i++)
                {
                    float angle = pattern % 2 == 0 ? (i - (shots - 1) * 0.5f) * 14f : i * 360f / shots;
                    Fire(transform.position, Quaternion.Euler(0, 0, angle) * lockedAim);
                }
                pattern++;
                charging = false;
                readyAt = Enemy.ActionTime + (IsEnraged ? 1.1f : 1.65f);
            }
            else if (Enemy.ActionTime >= readyAt)
            {
                charging = true;
                lockedAim = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector2.down;
                fireAt = Enemy.ActionTime + 0.85f;
                CombatVfx.Ring(Run.ProjectileRoot, transform.position, 1.5f, AbilityCatalog.Gold, 0.85f);
            }
            else if (offset.magnitude > 2f)
                transform.position = Run.Map.Move(transform.position, offset.normalized * Enemy.Speed * Enemy.MoveMultiplier * Time.deltaTime);
        }

        public override void ApplyNetState(bool isCharging, byte state)
        {
            charging = isCharging;
            if (pattern % 2 != state % 2) pattern++;
        }
    }
}
