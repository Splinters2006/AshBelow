using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Brawler's class mechanic: every point of damage she takes stokes her temper. After 5 she can go Super
    /// Angry, with huge boosts to speed, reach, area, charge speed and damage (see <see cref="HeroBuffs.Fury"/>).
    /// With its R upgrade (Seeing Red, from the Ash shop) her barrages charge 75% faster still while furious.
    /// </summary>
    public sealed class SuperAngry : ChargedMechanic
    {
        public const int DamageNeeded = 5;
        public const float Duration = 8f;
        /// <summary>Seeing Red: how much faster a barrage charges while furious.</summary>
        public const float UpgradedChargeSpeed = 1.75f;
        /// <summary>Scales the Brawler's charge time, on top of fury's own boost.</summary>
        public float ChargeDurationMultiplier => IsUpgraded && Player.Buffs.IsFurious ? 1f / UpgradedChargeSpeed : 1f;
        public override string Name => "Super Angry";
        public override Color Color => HeroBuffs.FuryColor;
        public override int Required => DamageNeeded;
        public override string Status => Player.Buffs.IsFurious ? "FURIOUS  " + Seconds(Player.Buffs.FuryRemaining) : base.Status;

        public override void OnDamaged() => AddCharge(1);

        protected override bool Activate(Vector2 aim)
        {
            Player.Buffs.Fury(Duration);
            var root = Player.Run.ProjectileRoot;
            HeroVfx.Pulse(root, transform.position, 2.6f, HeroBuffs.FuryColor, 0.5f);
            HeroVfx.Sparks(root, transform.position, HeroBuffs.FuryColor, 26, 6f, 0.5f);
            CombatVfx.Ring(root, transform.position, 1.8f, Color.white, 0.4f);
            CoopFx.Pulse(Player.Run, transform.position, 2.6f, HeroBuffs.FuryColor, 0.5f);
            CoopFx.Ring(Player.Run, transform.position, 1.8f, Color.white, 0.4f);
            ScreenFx.Shake(0.3f, 0.3f);
            ScreenFx.Flash(new Color(1f, 0.35f, 0.15f, 0.2f), 0.2f);
            return true;
        }
    }
}
