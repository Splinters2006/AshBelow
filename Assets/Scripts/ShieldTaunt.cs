using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Knight's class mechanic: he raises a great shield all around him for 2.25 seconds. He can walk while he holds
    /// it but do nothing else. It does not reflect, but every bolt it stops from any side grants a ward. In co-op,
    /// enemies target him first for a few seconds.
    /// </summary>
    public sealed class ShieldTaunt : ClassMechanic
    {
        public const float Duration = 2.25f, Cooldown = 12f, Reach = 2.025f, Cone = 360f, AggroDuration = 6f;
        public static readonly Color ShieldColor = new Color(0.4f, 0.75f, 1f);
        private float until, readyAt, aggroUntil;
        public override string Name => "Shield Taunt";
        public override Color Color => ShieldColor;
        public float CooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, readyAt - Time.time));
        public override void ReduceCooldown(float seconds) => readyAt = Cooldowns.Shorten(readyAt, seconds);
        public override float Readiness => IsTaunting ? 0f : 1f - CooldownRemaining / Cooldown;
        public override string Status => IsTaunting ? "HOLDING" : CooldownRemaining > 0f ? Seconds(CooldownRemaining) : "READY";
        public bool IsTaunting => Player.Run.IsPlaying && Player.Health > 0 && Time.time < until;
        /// <summary>While true, enemies choose this Knight over closer heroes.</summary>
        public bool DrawsAggro => Player.Health > 0 && Time.time < aggroUntil;
        public Vector2 Direction { get; private set; } = Vector2.right;

        public override bool TryActivate(Vector2 aim)
        {
            if (!CanAct || CooldownRemaining > 0f || aim.sqrMagnitude < 0.001f) return false;
            Direction = aim.normalized;
            until = Time.time + Duration;
            aggroUntil = Time.time + AggroDuration;
            readyAt = Time.time + Cooldown;
            Player.Weapon?.Hide();
            Player.Charge.Cancel();
            // He can walk with the shield up, but no attacks, abilities or dodges (see DungeonPlayer.IsHoldingShield).
            var root = Player.Run.ProjectileRoot;
            HeroVfx.Pulse(root, transform.position, Reach * 1.2f, ShieldColor, 0.35f);
            CombatVfx.Ring(root, transform.position, Reach * 1.6f, AbilityCatalog.Gold, 0.45f);
            CoopFx.Ring(Player.Run, transform.position, Reach * 1.6f, AbilityCatalog.Gold, 0.45f);
            ScreenFx.Shake(0.1f, 0.12f);
            return true;
        }

        /// <summary>Stops a bolt at <paramref name="position"/> heading in from any side within reach; each block adds a ward.</summary>
        public bool TryBlock(Vector2 position, Vector2 incoming)
        {
            Vector2 offset = position - (Vector2)transform.position;
            if (!IsTaunting || offset.sqrMagnitude > Reach * Reach || Vector2.Dot(incoming, offset) >= 0f) return false;
            Player.Powerups.AddWard();
            HeroVfx.Sparks(Player.Run.ProjectileRoot, position, ShieldColor, 8, 3.5f, 0.25f, -incoming, 100f);
            HeroVfx.Pulse(transform, transform.position, 0.95f, AbilityCatalog.Ice, 0.2f);
            return true;
        }
    }
}
