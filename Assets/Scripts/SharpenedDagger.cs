using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Assassin's class mechanic: +1 damage for 7.5 seconds. Every hit on an enemy's back adds another +1 and
    /// refreshes the duration. The cooldown starts once the edge dulls.
    /// </summary>
    public sealed class SharpenedDagger : ClassMechanic
    {
        public const float Duration = 7.5f, Cooldown = 15f;
        public static readonly Color EdgeColor = new Color(0.85f, 0.95f, 1f);
        private int bonus;
        private float until = float.NegativeInfinity;
        public bool IsActive => Time.time < until;
        public float Remaining => Mathf.Max(0f, until - Time.time);
        public float CooldownRemaining => IsActive ? Cooldown : DebugMode.Cooldown(Mathf.Max(0f, until + Cooldown - Time.time));
        public override string Name => "Sharpened Dagger";
        public override Color Color => EdgeColor;
        public override int BonusDamage => IsActive ? bonus : 0;
        public override float Readiness => IsActive ? 0f : 1f - CooldownRemaining / Cooldown;
        public override string Status => IsActive ? $"+{bonus}  {Seconds(Remaining)}" : CooldownRemaining > 0f ? Seconds(CooldownRemaining) : "READY";

        public override bool TryActivate(Vector2 aim)
        {
            if (!CanAct || IsActive || CooldownRemaining > 0f) return false;
            bonus = 1;
            until = Time.time + Duration;
            Flash(1f);
            return true;
        }

        public override void OnBackstab()
        {
            if (!IsActive) return;
            bonus++;
            until = Time.time + Duration;
            Flash(0.7f);
        }

        private void Flash(float size)
        {
            var root = Player.Run.ProjectileRoot;
            HeroVfx.Pulse(transform, transform.position, size, EdgeColor, 0.25f);
            HeroVfx.Sparks(root, transform.position, EdgeColor, 6, 3f, 0.25f);
        }
    }
}
