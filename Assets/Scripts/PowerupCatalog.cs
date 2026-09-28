using System.Collections.Generic;

namespace Slopgame
{
    public enum PowerupType { Damage, Vitality, Movement, AttackSpeed, CriticalHits, Armor, LifeSteal, DodgeRecovery }

    public sealed class PowerupDefinition
    {
        public PowerupType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaxStacks { get; }
        public PowerupDefinition(PowerupType type, string name, string description, int maxStacks)
        {
            Type = type; Name = name; Description = description; MaxStacks = maxStacks;
        }
    }

    public static class PowerupCatalog
    {
        public static readonly IReadOnlyList<PowerupDefinition> All = new[]
        {
            new PowerupDefinition(PowerupType.Damage, "Keen Edge", "+1 damage to slashes and arrows", int.MaxValue),
            new PowerupDefinition(PowerupType.Vitality, "Vitality", "+2 maximum HP and fully heal", int.MaxValue),
            new PowerupDefinition(PowerupType.Movement, "Fleet Foot", "+0.7 movement speed", 5),
            new PowerupDefinition(PowerupType.AttackSpeed, "Quick Hands", "+20% base attack speed", 5),
            new PowerupDefinition(PowerupType.CriticalHits, "Deadeye", "+10% chance to deal double damage", 5),
            new PowerupDefinition(PowerupType.Armor, "Ward", "Block one extra hit each floor; refill on descent", 3),
            new PowerupDefinition(PowerupType.LifeSteal, "Soul Harvest", "Heal 1 HP every 5 / 4 / 3 kills by rank", 3),
            new PowerupDefinition(PowerupType.DodgeRecovery, "Second Wind", "20% shorter dodge cooldown per rank", 3)
        };

        public static PowerupDefinition Get(PowerupType type) => All[(int)type];
    }
}
