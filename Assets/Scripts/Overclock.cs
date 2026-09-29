using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Augment's class mechanic: every enemy his plasma ray strikes charges it. After 25 he overclocks for 8 seconds:
    /// every ray is fully charged and fires twice as fast, the cannon is vented (ready at once), fires fully charged and
    /// cools down twice as fast.
    /// </summary>
    public sealed class Overclock : ChargedMechanic
    {
        public const int HitsNeeded = 25;
        public const float Duration = 8f;
        public override string Name => "Overclock";
        public override Color Color => CyborgAttack.Plasma;
        public override int Required => HitsNeeded;
        private CyborgAttack Cannon => Player.Weapon as CyborgAttack;
        public bool IsActive => Cannon != null && Cannon.IsOverclocked;
        public override string Status => IsActive ? "OVERCLOCKED  " + Seconds(Cannon.OverclockRemaining) : base.Status;

        /// <summary>Enemies struck by one ray; hits while already overclocked do not count.</summary>
        public void OnRayHits(int count) { if (!IsActive) AddCharge(count); }

        protected override bool Activate(Vector2 aim)
        {
            var cannon = Cannon;
            if (cannon == null || cannon.IsOverclocked) return false;
            cannon.StartOverclock(Duration);
            var run = Player.Run;
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, transform.position, 2.2f, CyborgAttack.Plasma, 0.45f);
            HeroVfx.Sparks(root, transform.position, CyborgAttack.Core, 24, 6f, 0.45f);
            HeroVfx.Motes(root, transform.position, 1f, CyborgAttack.Plasma, 20, 0.8f);
            CombatVfx.Ring(root, transform.position, 1.6f, CyborgAttack.Plasma, 0.4f);
            CoopFx.Pulse(run, transform.position, 2.2f, CyborgAttack.Plasma, 0.45f);
            CoopFx.Ring(run, transform.position, 1.6f, CyborgAttack.Plasma, 0.4f);
            ScreenFx.Shake(0.2f, 0.25f);
            ScreenFx.Flash(new Color(0.35f, 1f, 0.78f, 0.18f), 0.2f);
            return true;
        }
    }
}
