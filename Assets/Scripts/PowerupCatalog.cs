using System.Collections.Generic;

namespace Slopgame
{
    public enum PowerupType { Damage, Vitality, Movement, AttackSpeed, CriticalHits, Armor, LifeSteal, DodgeRecovery, SweepingEdge, Riposte, QuickDraw, Bodkin }

    public sealed class PowerupDefinition
    {
        public PowerupType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaxStacks { get; }
        public WeaponType? ClassWeapon { get; }
        public PowerupDefinition(PowerupType type, string name, string description, int maxStacks, WeaponType? classWeapon = null)
        {
            Type = type; Name = name; Description = description; MaxStacks = maxStacks;
            ClassWeapon = classWeapon;
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
            new PowerupDefinition(PowerupType.DodgeRecovery, "Second Wind", "10% shorter dodge cooldown per rank", 3),
            new PowerupDefinition(PowerupType.SweepingEdge, "Knight: Sweeping Edge", "+15 degrees to fully charged slash cone", 2, WeaponType.Sword),
            new PowerupDefinition(PowerupType.Riposte, "Knight: Riposte", "+1 damage to reflected bolts", 3, WeaponType.Sword),
            new PowerupDefinition(PowerupType.QuickDraw, "Archer: Quick Draw", "15% shorter bow charge time per rank", 2, WeaponType.Bow),
            new PowerupDefinition(PowerupType.Bodkin, "Archer: Bodkin", "+0.5x maximum charged arrow damage", 2, WeaponType.Bow)
        };

        public static PowerupDefinition Get(PowerupType type) => All[(int)type];
    }
}
