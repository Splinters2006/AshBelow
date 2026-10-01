using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Reaper's class mechanic: three souls raise a skeleton that fights beside him. Skeletons are frail and hit
    /// softly, but their health and damage come from his own, so every talent that strengthens him strengthens them.
    /// Each of their blows strikes fear into its target. With its R upgrade (Avatar of Death, from the Ash shop), once
    /// he holds <see cref="IncarnationSouls"/> souls the mechanic spends them instead and he becomes the incarnation of
    /// death: for <see cref="IncarnationTime"/> seconds all his damage is doubled and every hit he lands strikes
    /// <see cref="IncarnationFear"/> second of fear.
    /// </summary>
    public sealed class ArmyOfTheDead : ClassMechanic
    {
        public const int SoulCost = 3, MaxSkeletons = 4, IncarnationSouls = 99;
        public const float IncarnationTime = 5f, IncarnationFear = 1f;
        private readonly List<SkeletonMinion> skeletons = new List<SkeletonMinion>();
        private float nextMote;
        public override string Name => IsIncarnate || CanIncarnate ? "Avatar of Death" : "Army of the Dead";
        public override Color Color => ReaperAttack.Soul;
        private ReaperAttack Reaper => Player.Weapon as ReaperAttack;
        private int Souls => DebugMode.Enabled ? SoulCost : Reaper != null ? Reaper.Souls : 0;
        /// <summary>What a skeleton and the incarnation cost him (one soul less with Death's Bargain, his passive).</summary>
        private int SkeletonCost => Reaper != null ? Reaper.Cost(SoulCost) : SoulCost;
        private int IncarnationCost => Reaper != null ? Reaper.Cost(IncarnationSouls) : IncarnationSouls;
        public bool IsIncarnate => Player.Buffs != null && Player.Buffs.IsIncarnate;
        /// <summary>True with the Avatar of Death upgrade and enough souls to become the incarnation of death (and not already it).</summary>
        public bool CanIncarnate => IsUpgraded && !IsIncarnate && Reaper != null && Reaper.Souls >= IncarnationCost;
        /// <summary>Skeletons still standing (those from an earlier floor crumbled with it).</summary>
        public int Standing
        {
            get
            {
                skeletons.RemoveAll(skeleton => skeleton == null);
                return skeletons.Count;
            }
        }
        public override float Readiness => IsIncarnate ? Player.Buffs.IncarnateRemaining / IncarnationTime : CanIncarnate ? 1f
            : Standing >= MaxSkeletons ? 0f : Mathf.Clamp01(Souls / (float)SkeletonCost);
        public override string Status => IsIncarnate ? Seconds(Player.Buffs.IncarnateRemaining) : CanIncarnate ? $"READY  /  {IncarnationCost} SOULS"
            : Standing >= MaxSkeletons ? "ARMY FULL" : Souls >= SkeletonCost ? "READY" : $"{Souls} / {SkeletonCost}";

        public override bool TryActivate(Vector2 aim)
        {
            var reaper = Reaper;
            if (!CanAct || reaper == null) return false;
            if (CanIncarnate) return Incarnate(reaper);
            if (IsIncarnate || Standing >= MaxSkeletons || !reaper.Spend(SkeletonCost)) return false;
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

        /// <summary>Ninety-nine souls burn away at once and death itself walks the floor.</summary>
        private bool Incarnate(ReaperAttack reaper)
        {
            if (!reaper.Spend(IncarnationCost)) return false;
            Player.Buffs.Incarnate(IncarnationTime);
            var run = Player.Run;
            Vector2 at = transform.position;
            AvatarOfDeathVfx.Play(run.ProjectileRoot, transform, IncarnationTime);
            CoopFx.AvatarOfDeath(run, IncarnationTime);
            HeroVfx.Pulse(run.ProjectileRoot, at, 6f, ReaperAttack.Soul, 0.7f);
            HeroVfx.Pulse(run.ProjectileRoot, at, 3f, ReaperAttack.Shade, 0.5f);
            HeroVfx.Sparks(run.ProjectileRoot, at, ReaperAttack.Bone, 30, 6f, 0.7f, null, 360f, 1.4f);
            CoopFx.Pulse(run, at, 6f, ReaperAttack.Soul, 0.7f);
            ScreenFx.Flash(FlameMesh.Alpha(ReaperAttack.Shade, 0.45f), 0.5f);
            ScreenFx.Shake(0.35f, 0.5f);
            return true;
        }

        private void Update()
        {
            // Soul-fire pours off him for as long as the incarnation lasts.
            if (Player == null || !IsIncarnate || !Player.Run.IsPlaying || Time.time < nextMote) return;
            nextMote = Time.time + 0.06f;
            HeroVfx.Motes(Player.Run.ProjectileRoot, transform.position, 0.7f, Random.value < 0.5f ? ReaperAttack.Soul : ReaperAttack.Bone, 3, 0.6f);
        }
    }
}
