using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Demoness's class mechanic: each enemy she immobilizes (paralyses, freezes, stuns or roots) feeds it. After 7
    /// her tail splits in two for a few seconds: her tail sweep covers twice the cone (a full circle) and reaches 25%
    /// farther, and both tails show in her attacks. With its R upgrade (Dread Presence, from the Ash shop) every enemy
    /// that comes within <see cref="AuraRadius"/> of her while it lasts is paralysed.
    /// </summary>
    public sealed class DemonicPower : ChargedMechanic
    {
        public const int ParalysesNeeded = 7;
        public const float Duration = 10f, SweepConeMultiplier = 2f, SweepReachMultiplier = 1.25f, AuraRadius = 5f, AuraParalysis = 2f;
        private readonly HashSet<DungeonEnemy> inside = new HashSet<DungeonEnemy>(), seen = new HashSet<DungeonEnemy>();
        private float until, nextRing;
        public bool IsActive => Time.time < until;
        public override string Name => "Demonic Power";
        public override Color Color => DemonessAttack.Violet;
        public override int Required => ParalysesNeeded;
        public override string Status => IsActive ? "TWIN TAILS  " + Seconds(until - Time.time) : base.Status;

        public override void OnImmobilized() { if (!IsActive) AddCharge(1); }

        protected override bool Activate(Vector2 aim)
        {
            if (IsActive) return false;
            until = Time.time + Duration;
            inside.Clear();
            Vector2 center = transform.position;
            var run = Player.Run;
            var root = run.ProjectileRoot;
            PentagramVfx.Play(root, center, 2.2f, 0.3f);
            CoopFx.Pentagram(run, center, 2.2f, 0.3f);
            HeroVfx.Pulse(root, center, 2.6f, HeroBuffs.AscendColor, 0.5f);
            HeroVfx.Motes(root, center, 1.4f, DemonessAttack.Abyss, 30, 1.2f);
            HeroVfx.Sparks(root, center, Color.Lerp(DemonessAttack.Violet, DemonessAttack.Pale, 0.3f), 26, 6f, 0.5f, null, 360f, 1.3f);
            CoopFx.Pulse(run, center, 2.6f, HeroBuffs.AscendColor, 0.5f);
            ScreenFx.Flash(new Color(0.35f, 0.04f, 0.55f, 0.3f), 0.35f);
            ScreenFx.Shake(0.25f, 0.35f);
            return true;
        }

        private void Update()
        {
            if (Player == null || Player.Run == null || !IsActive || !IsUpgraded) return;
            var run = Player.Run;
            if (!run.IsPlaying || Player.Health <= 0) return;
            Vector2 center = transform.position;
            var root = run.ProjectileRoot;
            // Dread Presence: whoever steps inside the ring is paralysed; stepping out and back in does it again.
            float hold = AuraParalysis + (Player.Weapon is DemonessAttack tail ? tail.ParalysisBonus : 0f);
            bool lingering = Player.Powerups.Count(PowerupType.LingeringTerror) > 0;
            seen.Clear();
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(center, enemy.transform.position) > AuraRadius + enemy.HitRadius) continue;
                seen.Add(enemy);
                if (inside.Contains(enemy)) continue;
                // The aura is not a hold she lands herself, so it does not feed the next Demonic Power.
                if (enemy.Paralyze(hold, lingering))
                    HeroVfx.Sparks(root, enemy.transform.position, DemonessAttack.Violet, 10, 3.5f, 0.4f, (Vector2)enemy.transform.position - center, 90f);
            }
            inside.Clear();
            inside.UnionWith(seen);
            if (Time.time < nextRing) return;
            nextRing = Time.time + 0.5f;
            CombatVfx.Ring(root, center, AuraRadius, FlameMesh.Alpha(DemonessAttack.Violet, 0.45f), 0.5f);
        }
    }
}
