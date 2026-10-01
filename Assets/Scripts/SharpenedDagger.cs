using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Assassin's class mechanic: +1 damage for 7.5 seconds. Every hit on an enemy's back adds another +1 and
    /// refreshes the duration. The cooldown starts once the edge dulls. With its R upgrade (Razor's Edge, from the Ash
    /// shop) the edge lasts 10 seconds and, while it is sharp, every stab is fully charged the moment it starts.
    /// </summary>
    public sealed class SharpenedDagger : ClassMechanic
    {
        public const float Duration = 7.5f, UpgradedDuration = 10f, Cooldown = 15f;
        public float ActiveDuration => IsUpgraded ? UpgradedDuration : Duration;
        /// <summary>Razor's Edge: while the edge is sharp, stabs need no charging.</summary>
        public bool InstantCharge => IsUpgraded && IsActive;
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

        public override void ReduceCooldown(float seconds)
        {
            // The cooldown runs from when the edge dulls; while it is sharp there is nothing to shorten.
            if (!IsActive) until = Cooldowns.Shorten(until + Cooldown, seconds) - Cooldown;
        }

        public override bool TryActivate(Vector2 aim)
        {
            if (!CanAct || IsActive || CooldownRemaining > 0f) return false;
            bonus = 1;
            until = Time.time + ActiveDuration;
            Flash(true);
            return true;
        }

        public override void OnBackstab()
        {
            if (!IsActive) return;
            bonus++;
            until = Time.time + ActiveDuration;
            Flash(false);
        }

        /// <summary>The whetstone scrape over the Assassin's head and the circling blades; a backstab gets a quicker scrape.</summary>
        private void Flash(bool full)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            SharpenVfx.Play(root, transform, full);
            KeenEdgeAura.Show(root, transform, bonus, ActiveDuration);
            HeroVfx.Sparks(root, transform.position, EdgeColor, full ? 10 : 6, 3.5f, 0.3f);
            CoopFx.Sharpen(run, bonus, ActiveDuration, full);
        }
    }
}
