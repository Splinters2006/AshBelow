using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Paladin's class mechanic: bonus damage dealt through his blessings (by him or by blessed allies) charges it.
    /// At 50 (more in larger parties) he calls the angels down: they revive the ally who has been fallen longest with
    /// half health or, if nobody has fallen, heal the ally with the lowest share of health back to full.
    /// </summary>
    public sealed class HeavenlyHost : ChargedMechanic
    {
        public const int BlessedDamageNeeded = 50;
        public override string Name => "Heavenly Host";
        public override Color Color => AbilityCatalog.Gold;
        /// <summary>Every extra hero in the party raises the requirement by half.</summary>
        public override int Required => DungeonRun.ScaleHealth(BlessedDamageNeeded, Player.Run.PartySize);

        public override void OnBlessedHit(int bonus) => AddCharge(bonus);

        protected override bool Activate(Vector2 aim)
        {
            var run = Player.Run;
            // Revival first: the teammate who fell earliest rises with half health.
            RemoteHero fallen = null;
            if (run.IsNetworked)
                foreach (var hero in run.Coop.RemoteHeroes)
                    if (hero != null && !hero.IsAlive && (fallen == null || hero.DiedAt < fallen.DiedAt)) fallen = hero;
            if (fallen != null)
            {
                run.Coop.SendSupport(fallen.Id, SupportKind.Revive, 0, 0f);
                Angels(fallen.transform.position);
                return true;
            }
            // Otherwise the most wounded ally, by share of health, is healed to full.
            DungeonPlayer localTarget = null;
            RemoteHero remoteTarget = null;
            float lowest = 1f;
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
            {
                float share = ally.Health / (float)ally.MaxHealth;
                if (ally.Run != run || ally.Health <= 0 || share >= lowest) continue;
                lowest = share;
                localTarget = ally;
            }
            if (run.IsNetworked)
                foreach (var hero in run.Coop.RemoteHeroes)
                {
                    if (hero == null || !hero.IsAlive) continue;
                    float share = hero.Health / (float)hero.MaxHealth;
                    if (share >= lowest) continue;
                    lowest = share;
                    remoteTarget = hero;
                    localTarget = null;
                }
            if (remoteTarget != null)
            {
                run.Coop.SendSupport(remoteTarget.Id, SupportKind.Heal, remoteTarget.MaxHealth, 0f);
                Angels(remoteTarget.transform.position);
                return true;
            }
            // Everyone is already at full health: keep the charge.
            if (localTarget == null) return false;
            localTarget.Heal(localTarget.MaxHealth);
            Angels(localTarget.transform.position);
            return true;
        }

        private void Angels(Vector2 target)
        {
            var run = Player.Run;
            var root = run.ProjectileRoot;
            HolyLightVfx.Play(root, target, 1.4f, 0.35f);
            AngelVfx.Play(root, target);
            CoopFx.Angel(run, target);
            CoopFx.Holy(run, target, 1.4f, 0.35f);
            HeroVfx.Motes(root, target, 1.1f, AbilityCatalog.Gold, 30, 1.4f);
            HeroVfx.Pulse(root, target, 1.8f, Color.white, 0.5f);
            CombatVfx.Ring(root, transform.position, 1.2f, AbilityCatalog.Gold, 0.45f);
            CoopFx.Pulse(run, target, 1.8f, Color.white, 0.5f);
            ScreenFx.Flash(new Color(1f, 0.95f, 0.7f, 0.25f), 0.3f);
        }
    }
}
