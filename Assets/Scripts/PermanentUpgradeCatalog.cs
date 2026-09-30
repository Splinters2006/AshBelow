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
        /// <summary>How many guardians must have fallen in one descent (as this class, for a class upgrade) before this can be bought.</summary>
        public int RequiredGuardians { get; }
        public PermanentUpgradeDefinition(string id, string name, string description, int maxRank, int cost, int step, WeaponType? weapon = null,
            int requiredGuardians = 0)
        {
            Id = id; Name = name; Description = description; MaxRank = maxRank; BaseCost = cost; CostStep = step; ClassWeapon = weapon;
            RequiredGuardians = requiredGuardians;
        }
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
            new PermanentUpgradeDefinition("paladin_health", "Selfless Guardian", "+1 Paladin maximum HP per rank", 3, 35, 25, WeaponType.Hammer),
            new PermanentUpgradeDefinition("brawler_barrage", "Heavy Bag Drills", "+1 punch per charged barrage per rank", 2, 60, 50, WeaponType.Fists),
            new PermanentUpgradeDefinition("brawler_health", "Thick Fur", "+1 Brawler maximum HP per rank", 3, 35, 25, WeaponType.Fists),
            new PermanentUpgradeDefinition("demoness_paralysis", "Pressure Points", "+1 damage to paralysed enemies per rank", 3, 45, 35, WeaponType.Tail),
            new PermanentUpgradeDefinition("demoness_health", "Infernal Blood", "+1 Demoness maximum HP per rank", 3, 35, 25, WeaponType.Tail),
            new PermanentUpgradeDefinition("gambler_pockets", "Deep Pockets", "The Gambler's purse never lets him drop below +1 more coin per rank", 3, 35, 25, WeaponType.Coins),
            new PermanentUpgradeDefinition("gambler_luck", "Lady Luck", "+4% odds on All In per rank", 3, 50, 40, WeaponType.Coins),
            new PermanentUpgradeDefinition("augment_capacitor", "Capacitor Bank", "10% shorter plasma cannon cooldown per rank", 3, 45, 35, WeaponType.Beam),
            new PermanentUpgradeDefinition("augment_health", "Titanium Frame", "+1 Augment maximum HP per rank", 3, 35, 25, WeaponType.Beam),
            AbilityUnlock(AbilityType.WarBanner, 150),
            AbilityUnlock(AbilityType.BearTrap, 150),
            AbilityUnlock(AbilityType.LightningStorm, 180),
            AbilityUnlock(AbilityType.DeathMark, 160),
            AbilityUnlock(AbilityType.ShadowClone, 200),
            Mechanic(WeaponType.Sword, "Shield Taunt", "R: turn red with rage for 2.25s. You can walk; you block bolts from every side (+1 ward each) and draw the enemies' attention"),
            Mechanic(WeaponType.Bow, "Elemental Quiver", "R: cycle fire, freeze and shock arrows. Critical hits set off the arrow's element"),
            Mechanic(WeaponType.Staff, "Wild Storm", "R: after 10 elemental effects, summon a storm that hurls fire, lightning and ice, every strike sure to burn, shock or freeze"),
            Mechanic(WeaponType.Daggers, "Sharpened Dagger", "R: +1 damage for 7.5s; every backstab adds +1 more and refreshes it"),
            Mechanic(WeaponType.Hammer, "Heavenly Host", "R: after 50 blessed bonus damage, angels revive the longest-fallen ally or fully heal the weakest"),
            Mechanic(WeaponType.Fists, "Super Angry", "R: after taking 5 damage, erupt with huge speed, reach, charge speed and damage"),
            Mechanic(WeaponType.Tail, "Demonic Power", "R: after 7 paralyses, terrify everything nearby: they turn their backs and freeze in place"),
            Mechanic(WeaponType.Coins, "The Purse", "R: open your purse, a shop paid for in coins: healing, wards or loaded dice"),
            Mechanic(WeaponType.Beam, "Overclock", "R: after your plasma ray strikes 25 enemies, overclock for 8s: fully charged rays at double speed and a vented, faster cannon")
        };

        /// <summary>The Ash shop's hefty class mechanic (R), sold only once that class has felled the third guardian.</summary>
        public const int MechanicCost = 600, MechanicGuardians = 3;
        public static string MechanicId(WeaponType weapon) => "mechanic_" + weapon.ToString().ToLowerInvariant();

        /// <summary>
        /// An ability guardians only offer once it is bought here (see <see cref="AbilityDefinition.ShopUnlock"/>).
        /// Once bought, it can turn up in every later descent.
        /// </summary>
        private static PermanentUpgradeDefinition AbilityUnlock(AbilityType type, int cost)
        {
            var ability = AbilityCatalog.Get(type);
            return new PermanentUpgradeDefinition(ability.UnlockId, "Ability: " + ability.Name, ability.Description + " Guardians can offer it once bought.",
                1, cost, 0, ability.ClassWeapon);
        }

        private static PermanentUpgradeDefinition Mechanic(WeaponType weapon, string name, string description)
            => new PermanentUpgradeDefinition(MechanicId(weapon), name, description, 1, MechanicCost, 0, weapon, MechanicGuardians);

        public static PermanentUpgradeDefinition Get(string id)
        {
            foreach (var upgrade in All) if (upgrade.Id == id) return upgrade;
            return null;
        }
    }
}
