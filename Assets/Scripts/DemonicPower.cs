using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Demoness's class mechanic: each enemy she paralyses feeds it. After 7 she channels the Demon Lord himself,
    /// whose massive head rises behind her and roars: every enemy around her is struck with fear, turns its back to her and is paralysed on the spot.
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
            float paralysis = Paralysis + (Player.Weapon is DemonessAttack tail ? tail.ParalysisBonus : 0f);
            foreach (var enemy in targets) enemy.Fear(center, paralysis);
            var root = run.ProjectileRoot;
            // The Demon Lord's head rises behind her and roars the fear out over everything nearby.
            DemonHeadVfx.Play(root, center, Radius);
            CoopFx.DemonHead(run, center, Radius);
            PentagramVfx.Play(root, center, 2.2f, 0.3f);
            CoopFx.Pentagram(run, center, 2.2f, 0.3f);
            HeroVfx.Pulse(root, center, Radius, HeroBuffs.AscendColor, 0.7f);
            HeroVfx.Motes(root, center, 2f, DemonessAttack.Abyss, 40, 1.4f);
            HeroVfx.Sparks(root, center, Color.Lerp(DemonessAttack.Violet, DemonessAttack.Pale, 0.3f), 30, 6f, 0.6f, null, 360f, 1.4f);
            CombatVfx.Ring(root, center, Radius, DemonessAttack.Pale, 0.7f);
            CoopFx.Pulse(run, center, Radius, HeroBuffs.AscendColor, 0.7f);
            CoopFx.Ring(run, center, Radius, DemonessAttack.Pale, 0.7f);
            foreach (var enemy in targets)
                HeroVfx.Sparks(root, enemy.transform.position, DemonessAttack.Violet, 10, 3.5f, 0.4f, (Vector2)enemy.transform.position - center, 90f);
            ScreenFx.Flash(new Color(0.35f, 0.04f, 0.55f, 0.4f), 0.45f);
            ScreenFx.Shake(0.35f, 0.6f);
            return true;
        }
    }
}
