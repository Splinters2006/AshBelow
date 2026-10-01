using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Reaper's class mechanic: three souls raise a skeleton that fights beside him. Skeletons are frail and hit
    /// softly, but their health and damage come from his own, so every talent that strengthens him strengthens them.
    /// </summary>
    public sealed class ArmyOfTheDead : ClassMechanic
    {
        public const int SoulCost = 3, MaxSkeletons = 4;
        private readonly List<SkeletonMinion> skeletons = new List<SkeletonMinion>();
        public override string Name => "Army of the Dead";
        public override Color Color => ReaperAttack.Soul;
        private ReaperAttack Reaper => Player.Weapon as ReaperAttack;
        private int Souls => DebugMode.Enabled ? SoulCost : Reaper != null ? Reaper.Souls : 0;
        /// <summary>Skeletons still standing (those from an earlier floor crumbled with it).</summary>
        public int Standing
        {
            get
            {
                skeletons.RemoveAll(skeleton => skeleton == null);
                return skeletons.Count;
            }
        }
        public override float Readiness => Standing >= MaxSkeletons ? 0f : Mathf.Clamp01(Souls / (float)SoulCost);
        public override string Status => Standing >= MaxSkeletons ? "ARMY FULL" : Souls >= SoulCost ? "READY" : $"{Souls} / {SoulCost}";

        public override bool TryActivate(Vector2 aim)
        {
            var reaper = Reaper;
            if (!CanAct || reaper == null || Standing >= MaxSkeletons || !reaper.Spend(SoulCost)) return false;
            var run = Player.Run;
            // It claws its way up beside him, on whichever side has room.
            Vector2 hero = transform.position, spot = hero;
            float start = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 8; i++)
            {
                Vector2 candidate = hero + FlameMesh.Polar(start + i * Mathf.PI / 4f, 1.1f);
                if (!run.Map.CanStand(candidate, 0.25f)) continue;
                spot = candidate;
                break;
            }
            skeletons.Add(SkeletonMinion.Raise(Player, spot));
            HeroVfx.Pulse(run.ProjectileRoot, spot, 0.9f, ReaperAttack.Soul, 0.4f);
            HeroVfx.Sparks(run.ProjectileRoot, spot, ReaperAttack.Bone, 12, 3.5f, 0.4f, Vector2.up, 120f);
            CoopFx.Pulse(run, spot, 0.9f, ReaperAttack.Soul, 0.4f);
            return true;
        }
    }
}
