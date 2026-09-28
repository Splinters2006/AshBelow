using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class PlayerPowerups : MonoBehaviour
    {
        private readonly Dictionary<PowerupType, int> stacks = new Dictionary<PowerupType, int>();
        private int harvestKills;
        public WeaponType ClassWeapon { get; set; }
        public PlayerAbilities Abilities { get; set; }
        public float DrawTimeMultiplier => 1f - 0.15f * Count(PowerupType.QuickDraw);
        public float ArrowChargeMultiplier => 3f + 0.5f * Count(PowerupType.Bodkin);
        public int ReflectionDamage => 2 + Count(PowerupType.Riposte);
        public int ArmorCharges { get; private set; }
        public float AttackIntervalMultiplier => 1f / (1f + 0.2f * Count(PowerupType.AttackSpeed));
        public float CritChance => 0.05f + Count(PowerupType.CriticalHits) * 0.1f;
        public float PhysicalCritChance => Mathf.Min(0.9f, CritChance
            + (ClassWeapon == WeaponType.Daggers ? 0.1f + Count(PowerupType.AssassinCrit) * 0.05f : 0f));
        public float DodgeCooldownMultiplier => 1f - 0.1f * Count(PowerupType.DodgeRecovery);
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

        public int DamageForRoll(int damage, float roll) => roll < PhysicalCritChance ? damage * 2 : damage;
        public int RollDamage(int damage) => DamageForRoll(damage, Random.value);
        public void BeginFloor() { ArmorCharges = Count(PowerupType.Armor) + Count(PowerupType.PaladinWard); }
        public bool AbsorbHit()
        {
            if (ArmorCharges <= 0) return false;
            ArmorCharges--;
            return true;
        }

        public void OnKill(DungeonPlayer player)
        {
            int rank = Count(PowerupType.LifeSteal);
            if (rank == 0) return;
            harvestKills++;
            if (harvestKills >= 6 - rank) { harvestKills = 0; player.Heal(1); }
        }

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
