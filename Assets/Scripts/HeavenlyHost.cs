using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Paladin's class mechanic: bonus damage dealt through his blessings (by him or by blessed allies) charges it.
    /// At 50 (more in larger parties) he calls the angels down: they revive the ally who has been fallen longest with
    /// half health or, if nobody has fallen, heal the ally with the lowest share of health back to full. With its R
    /// upgrade (Seraphim, from the Ash shop) two angels come and help two allies; when only one needs them, the other
    /// gives the Paladin a +5 damage blessing for 30 seconds.
    /// </summary>
    public sealed class HeavenlyHost : ChargedMechanic
    {
        public const int BlessedDamageNeeded = 50, LoneBlessingDamage = 5;
        public const float LoneBlessingTime = 30f;
        public override string Name => "Heavenly Host";
        public override Color Color => AbilityCatalog.Gold;
        /// <summary>Every extra hero in the party raises the requirement by half.</summary>
        public override int Required => DungeonRun.ScaleHealth(BlessedDamageNeeded, Player.Run.PartySize);

        public override void OnBlessedHit(int bonus) => AddCharge(bonus);

        protected override bool Activate(Vector2 aim)
        {
            var run = Player.Run;
            int wanted = IsUpgraded ? 2 : 1, helped = 0;
            // Revival first: the teammates who fell earliest rise with half health.
            if (run.IsNetworked)
            {
                var fallen = new List<RemoteHero>();
                foreach (var hero in run.Coop.RemoteHeroes)
                    if (hero != null && !hero.IsAlive) fallen.Add(hero);
                fallen.Sort((a, b) => a.DiedAt.CompareTo(b.DiedAt));
                for (int i = 0; i < fallen.Count && helped < wanted; i++, helped++)
                {
                    run.Coop.SendSupport(fallen[i].Id, SupportKind.Revive, 0, 0f);
                    Angels(fallen[i].transform.position);
                }
            }
            // Then the most wounded allies, by share of health, are healed to full.
            var localWounded = new List<DungeonPlayer>();
            var remoteWounded = new List<RemoteHero>();
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
                if (ally.Run == run && ally.Health > 0 && ally.Health < ally.MaxHealth) localWounded.Add(ally);
            if (run.IsNetworked)
                foreach (var hero in run.Coop.RemoteHeroes)
                    if (hero != null && hero.IsAlive && hero.Health < hero.MaxHealth) remoteWounded.Add(hero);
            while (helped < wanted)
            {
                DungeonPlayer localTarget = null;
                RemoteHero remoteTarget = null;
                float lowest = 1f;
                foreach (var ally in localWounded)
                {
                    float share = ally.Health / (float)ally.MaxHealth;
                    if (share >= lowest) continue;
                    lowest = share;
                    localTarget = ally;
                }
                foreach (var hero in remoteWounded)
                {
                    float share = hero.Health / (float)hero.MaxHealth;
                    if (share >= lowest) continue;
                    lowest = share;
                    remoteTarget = hero;
                    localTarget = null;
                }
                if (remoteTarget != null)
                {
                    remoteWounded.Remove(remoteTarget);
                    run.Coop.SendSupport(remoteTarget.Id, SupportKind.Heal, remoteTarget.MaxHealth, 0f);
                    Angels(remoteTarget.transform.position);
                }
                else if (localTarget != null)
                {
                    localWounded.Remove(localTarget);
                    localTarget.Heal(localTarget.MaxHealth);
                    Angels(localTarget.transform.position);
                }
                else break;
                helped++;
            }
            // Nobody has fallen and everyone is already at full health: keep the charge.
            if (helped == 0) return false;
            // Seraphim: with only one ally to help, the second angel blesses the Paladin's arms instead.
            if (IsUpgraded && helped == 1)
            {
                Player.Blessing.Apply(LoneBlessingDamage, LoneBlessingTime, Player);
                var root = run.ProjectileRoot;
                HolyLightVfx.Play(root, transform.position, 1.2f, 0.35f);
                CoopFx.Holy(run, transform.position, 1.2f, 0.35f);
                HeroVfx.Motes(root, transform.position, 0.9f, AbilityCatalog.Gold, 20, 1.2f);
            }
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
