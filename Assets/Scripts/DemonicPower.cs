using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Demoness's class mechanic: each enemy she paralyses feeds it. After 7 she channels the Demon Lord himself:
    /// every enemy around her is struck with fear, turns its back to her and is paralysed on the spot.
    /// </summary>
    public sealed class DemonicPower : ChargedMechanic
    {
        public const int ParalysesNeeded = 7;
        public const float Radius = 4.5f, Paralysis = 3f;
        public override string Name => "Demonic Power";
        public override Color Color => DemonessAttack.Violet;
        public override int Required => ParalysesNeeded;

        public override void OnParalyzed() => AddCharge(1);

        protected override bool Activate(Vector2 aim)
        {
            Vector2 center = transform.position;
            var run = Player.Run;
            var targets = run.Enemies.FindAll(enemy => enemy != null && enemy.Health > 0 && !enemy.IsInvulnerable
                && Vector2.Distance(center, enemy.transform.position) <= Radius + enemy.HitRadius);
            if (targets.Count == 0) return false;
            // The fear is not a paralysing strike of her own, so it does not feed the next Demonic Power.
            foreach (var enemy in targets) enemy.Fear(center, Paralysis);
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, center, Radius, HeroBuffs.AscendColor, 0.55f);
            HeroVfx.Motes(root, center, 1.2f, DemonessAttack.Abyss, 24, 1f);
            CombatVfx.Ring(root, center, Radius, DemonessAttack.Pale, 0.5f);
            CoopFx.Pulse(run, center, Radius, HeroBuffs.AscendColor, 0.55f);
            CoopFx.Ring(run, center, Radius, DemonessAttack.Pale, 0.5f);
            PentagramVfx.Play(root, center, 1.4f, 0.3f);
            foreach (var enemy in targets)
                HeroVfx.Sparks(root, enemy.transform.position, DemonessAttack.Violet, 6, 3f, 0.3f, (Vector2)enemy.transform.position - center, 90f);
            ScreenFx.Flash(new Color(0.3f, 0.05f, 0.5f, 0.3f), 0.3f);
            ScreenFx.Shake(0.25f, 0.3f);
            return true;
        }
    }
}
