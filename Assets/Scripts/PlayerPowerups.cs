using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public sealed class PlayerPowerups : MonoBehaviour
    {
        private readonly Dictionary<PowerupType, int> stacks = new Dictionary<PowerupType, int>();
        private int harvestKills;
        public int ArmorCharges { get; private set; }
        public float AttackIntervalMultiplier => 1f / (1f + 0.2f * Count(PowerupType.AttackSpeed));
        public float CritChance => Count(PowerupType.CriticalHits) * 0.1f;
        public float DodgeCooldownMultiplier => 1f - 0.2f * Count(PowerupType.DodgeRecovery);
        public int Count(PowerupType type) => stacks.TryGetValue(type, out int count) ? count : 0;
        public bool CanTake(PowerupType type) => Count(type) < PowerupCatalog.Get(type).MaxStacks;

        public bool Add(PowerupType type)
        {
            if (!CanTake(type)) return false;
            stacks[type] = Count(type) + 1;
            if (type == PowerupType.Armor) ArmorCharges++;
            return true;
        }

        public int DamageForRoll(int damage, float roll) => roll < CritChance ? damage * 2 : damage;
        public int RollDamage(int damage) => DamageForRoll(damage, Random.value);
        public void BeginFloor() { ArmorCharges = Count(PowerupType.Armor); }
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
