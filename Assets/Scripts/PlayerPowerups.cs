using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class PlayerPowerups : MonoBehaviour
    {
        private readonly Dictionary<PowerupType, int> stacks = new Dictionary<PowerupType, int>();
        private int harvestKills, soulShieldKills;
        public WeaponType ClassWeapon { get; set; }
        public PermanentBonuses Permanent { get; set; } = new PermanentBonuses(null, WeaponType.Sword);
        public PlayerAbilities Abilities { get; set; }
        public float DrawTimeMultiplier => (1f - 0.15f * Count(PowerupType.QuickDraw)) * Permanent.DrawMultiplier;
        public float ArrowChargeMultiplier => 3f + 0.5f * Count(PowerupType.Bodkin);
        public int ReflectionDamage => 2 + Count(PowerupType.Riposte) + Permanent.ReflectionDamage;
        public int ArmorCharges { get; private set; }
        public float AttackIntervalMultiplier => 1f / (1f + 0.2f * Count(PowerupType.AttackSpeed) + Permanent.AttackSpeed);
        /// <summary>The Wizard starts with a 15% base chance (crits and elemental effects); everyone else with 5%.</summary>
        public float CritChance => (ClassWeapon == WeaponType.Staff ? 0.15f : 0.05f) + Count(PowerupType.CriticalHits) * 0.1f;
        public float ElementalEffectChance => Mathf.Min(0.9f, CritChance + Permanent.EffectChance + Count(PowerupType.Stormcraft) * 0.05f);
        public float PhysicalCritChance => Mathf.Min(0.9f, CritChance + Permanent.PhysicalCritChance
            + (ClassWeapon == WeaponType.Daggers ? 0.1f + Count(PowerupType.AssassinCrit) * 0.05f : 0f));
        public float DodgeCooldownMultiplier => (1f - 0.1f * Count(PowerupType.DodgeRecovery)) * Permanent.DodgeMultiplier;
        public int Count(PowerupType type) => stacks.TryGetValue(type, out int count) ? count : 0;
        public bool CanTake(PowerupType type) => Count(type) < PowerupCatalog.Get(type).MaxStacks
            && (!PowerupCatalog.Get(type).ClassWeapon.HasValue || PowerupCatalog.Get(type).ClassWeapon == ClassWeapon)
            && (PowerupCatalog.Get(type).RequiredAbility == AbilityType.None
                || (Abilities != null && Abilities.IsEquipped(PowerupCatalog.Get(type).RequiredAbility)));

        public bool Add(PowerupType type)
        {
            if (!CanTake(type)) return false;
            stacks[type] = Count(type) + 1;
            if (type == PowerupType.Armor || type == PowerupType.PaladinWard) ArmorCharges++;
            return true;
        }

        public float CriticalMultiplier => 2f + Count(PowerupType.DeadlyPrecision) * 0.25f;
        public float RelicCooldownMultiplier => 1f - Count(PowerupType.RelicTraining) * 0.08f;
        public float SkillCooldownMultiplier => 1f - 0.1f * Count(ClassWeapon switch
        {
            WeaponType.Sword => PowerupType.GuardDrills,
            WeaponType.Bow => PowerupType.VolleyDrills,
            WeaponType.Staff => PowerupType.StormRhythm,
            WeaponType.Daggers => PowerupType.ShadowDance,
            WeaponType.Hammer => PowerupType.DivineCadence,
            WeaponType.Fists => PowerupType.SecondRound,
            WeaponType.Tail => PowerupType.TailRhythm,
            WeaponType.Coins => PowerupType.QuickDeal,
            WeaponType.Beam => PowerupType.HeatSink,
            _ => PowerupType.NightCycle
        });
        public int CriticalDamage(int damage) => Mathf.RoundToInt(damage * CriticalMultiplier);
        public int DamageForRoll(int damage, float roll) => roll < PhysicalCritChance ? CriticalDamage(damage) : damage;
        public int RollDamage(int damage) => DamageForRoll(damage, Random.value);
        public void BeginFloor() { ArmorCharges = Count(PowerupType.Armor) + Count(PowerupType.PaladinWard); }
        /// <summary>An extra ward for the rest of this floor (Shield Taunt blocks, the Gambler's Lucky Charm).</summary>
        public void AddWard() => ArmorCharges++;
        public bool AbsorbHit()
        {
            if (ArmorCharges <= 0) return false;
            ArmorCharges--;
            return true;
        }

        public const float KillStreakWindow = 1f, KillCooldownCut = 0.5f, PyreRadius = 2f;
        public const int MassacreKills = 5, MomentumKills = 2;
        private readonly Queue<float> recentKills = new Queue<float>();

        /// <summary>Kill talents, applied only on the killer's machine (<paramref name="enemy"/> is still in place, statuses intact).</summary>
        public void OnKill(DungeonPlayer player, DungeonEnemy enemy)
        {
            if (Count(PowerupType.SoulShield) > 0 && ++soulShieldKills >= 8)
            {
                soulShieldKills = 0;
                if (ArmorCharges < 3) AddWard();
            }
            int rank = Count(PowerupType.LifeSteal);
            if (rank > 0 && ++harvestKills >= 6 - rank) { harvestKills = 0; player.Heal(1); }

            while (recentKills.Count > 0 && Time.time - recentKills.Peek() > KillStreakWindow) recentKills.Dequeue();
            recentKills.Enqueue(Time.time);
            // Each streak pays out once, when it reaches its count, rather than on every kill after that.
            if (recentKills.Count == MomentumKills && Count(PowerupType.Momentum) > 0) player.ResetDodge();
            if (recentKills.Count == MassacreKills && Count(PowerupType.Massacre) > 0) player.ResetClassSkill();

            bool held = enemy != null && enemy.IsHeld;
            if (held && Count(PowerupType.NerveSnap) > 0) player.ResetClassSkill();
            float cut = (Count(PowerupType.Bloodrush) > 0 ? KillCooldownCut : 0f) + (held && Count(PowerupType.StillHunter) > 0 ? KillCooldownCut : 0f);
            if (cut > 0f) player.ReduceCooldowns(cut);

            if (enemy != null && enemy.IsBurning && Count(PowerupType.PyreBurst) > 0) PyreBurst(player, enemy);
        }

        /// <summary>A burning enemy bursts into flame: fire damage to everything around it, which may light them too.</summary>
        private static void PyreBurst(DungeonPlayer player, DungeonEnemy dead)
        {
            var run = player.Run;
            Vector2 center = dead.transform.position;
            var color = CombatDamage.ElementColor(DamageElement.Fire);
            HeroVfx.Pulse(run.ProjectileRoot, center, PyreRadius, color, 0.35f);
            HeroVfx.Sparks(run.ProjectileRoot, center, color, 14, 5f, 0.4f, null, 360f, 1.2f);
            CoopFx.Pulse(run, center, PyreRadius, color, 0.35f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy != dead && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= PyreRadius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, player.Damage, DamageElement.Fire, center, 0.6f);
        }

        /// <summary>Prospector: whether a fallen enemy leaves a second helping of crystals.</summary>
        public bool RollExtraCrystals() => Count(PowerupType.Prospector) > 0 && Random.value <= 0.5f * Count(PowerupType.Prospector);
        /// <summary>Haggler: what the crystal merchant charges as a fraction of his price.</summary>
        public float ShopPriceMultiplier => 1f - 0.25f * Count(PowerupType.Haggler);
        public int ShopRerolls => Count(PowerupType.MerchantsFavor);

        public string Summary
        {
            get
            {
                var names = new List<string>();
                foreach (var powerup in PowerupCatalog.All)
                    if (Count(powerup.Type) > 0) names.Add(powerup.Name + " x" + Count(powerup.Type));
                return names.Count == 0 ? "No boons yet" : string.Join("  /  ", names);
            }
        }
    }
}
