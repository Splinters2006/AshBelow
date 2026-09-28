using UnityEngine;

namespace Slopgame
{
    public sealed class PermanentUpgradeDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaxRank { get; }
        public int BaseCost { get; }
        public int CostStep { get; }
        public WeaponType? ClassWeapon { get; }
        public PermanentUpgradeDefinition(string id, string name, string description, int maxRank, int cost, int step, WeaponType? weapon = null)
        { Id = id; Name = name; Description = description; MaxRank = maxRank; BaseCost = cost; CostStep = step; ClassWeapon = weapon; }
        public int Cost(int currentRank) => BaseCost + CostStep * currentRank;
    }

    public static class PermanentUpgradeCatalog
    {
        public static readonly PermanentUpgradeDefinition[] All =
        {
            new PermanentUpgradeDefinition("health", "Lasting Vitality", "+1 maximum HP for every hero per rank", 5, 30, 20),
            new PermanentUpgradeDefinition("damage", "Tempered Arms", "+1 base damage for every hero per rank", 3, 100, 100),
            new PermanentUpgradeDefinition("speed", "Trailblazer", "+0.2 movement speed for every hero per rank", 5, 25, 25),
            new PermanentUpgradeDefinition("attack", "Combat Training", "+5% attack and charge speed for every hero per rank", 5, 40, 30),
            new PermanentUpgradeDefinition("dodge", "Lightfoot", "3% shorter dodge cooldown for every hero per rank", 5, 35, 25),
            new PermanentUpgradeDefinition("knight_reflect", "Polished Steel", "+1 reflected bolt damage per rank", 3, 45, 35, WeaponType.Sword),
            new PermanentUpgradeDefinition("knight_health", "Iron Constitution", "+1 Knight maximum HP per rank", 3, 35, 25, WeaponType.Sword),
            new PermanentUpgradeDefinition("archer_draw", "Trained Draw", "5% shorter bow charge time per rank", 3, 45, 35, WeaponType.Bow),
            new PermanentUpgradeDefinition("archer_damage", "Honed Arrows", "+1 Archer base damage per rank", 2, 100, 100, WeaponType.Bow),
            new PermanentUpgradeDefinition("wizard_lightning", "Storm Scholar", "+1 lightning damage per rank", 3, 50, 40, WeaponType.Staff),
            new PermanentUpgradeDefinition("wizard_effect", "Elemental Focus", "+3% elemental effect chance per rank", 3, 45, 35, WeaponType.Staff),
            new PermanentUpgradeDefinition("assassin_crit", "Lethal Training", "+3% physical critical chance per rank", 3, 45, 35, WeaponType.Daggers),
            new PermanentUpgradeDefinition("assassin_speed", "Silent Stride", "+0.15 Assassin movement speed per rank", 3, 35, 25, WeaponType.Daggers),
            new PermanentUpgradeDefinition("paladin_blessing", "Enduring Faith", "+1 second to the damage blessing per rank", 3, 45, 35, WeaponType.Hammer),
            new PermanentUpgradeDefinition("paladin_health", "Selfless Guardian", "+1 Paladin maximum HP per rank", 3, 35, 25, WeaponType.Hammer)
        };

        public static PermanentUpgradeDefinition Get(string id)
        {
            foreach (var upgrade in All) if (upgrade.Id == id) return upgrade;
            return null;
        }
    }
}
