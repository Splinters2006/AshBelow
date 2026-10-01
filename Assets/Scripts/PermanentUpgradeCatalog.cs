using System.Collections.Generic;
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
        /// <summary>The world that must be cleared once (by any hero) before this is sold; -1 for none.</summary>
        public int RequiredWorld { get; }
        /// <summary>Another upgrade that must be owned first (a class mechanic, for its R upgrade); null for none.</summary>
        public string RequiredUpgrade { get; }
        /// <summary>True for the upgrade to a class mechanic (see <see cref="PermanentUpgradeCatalog.MechanicUpgradeId"/>).</summary>
        public bool IsMechanicUpgrade => RequiredUpgrade != null;
        public PermanentUpgradeDefinition(string id, string name, string description, int maxRank, int cost, int step, WeaponType? weapon = null,
            int requiredGuardians = 0, int requiredWorld = -1, string requiredUpgrade = null)
        {
            RequiredUpgrade = requiredUpgrade;
            Id = id; Name = name; Description = description; MaxRank = maxRank; BaseCost = cost; CostStep = step; ClassWeapon = weapon;
            RequiredGuardians = requiredGuardians; RequiredWorld = requiredWorld;
        }
        public int Cost(int currentRank) => BaseCost + CostStep * currentRank;
    }

    public static class PermanentUpgradeCatalog
    {
        public static readonly PermanentUpgradeDefinition[] All = AddPassives(new PermanentUpgradeDefinition[]
        {
            new PermanentUpgradeDefinition("health", "Lasting Vitality", "+1 maximum HP for every hero per rank", 5, 30, 20),
            new PermanentUpgradeDefinition("damage", "Tempered Arms", "+1 base damage for every hero per rank", 3, 100, 100),
            new PermanentUpgradeDefinition("speed", "Trailblazer", "+0.2 movement speed for every hero per rank", 5, 25, 25),
            new PermanentUpgradeDefinition("attack", "Combat Training", "+5% attack and charge speed for every hero per rank", 5, 40, 30),
            new PermanentUpgradeDefinition("dodge", "Lightfoot", "3% shorter dodge cooldown for every hero per rank", 5, 35, 25),
            // Rewards for clearing each world for the first time, with any hero.
            WorldReward(EmberHeartId, "Ember Heart", "Start every descent with one random talent already taken", 1, 300, 0, 0),
            WorldReward(BackupDriveId, "Backup Drive", "Once per descent, a hit that would kill you leaves you at 1 HP instead", 1, 800, 0, 1),
            WorldReward(TargetingChipId, "Targeting Chip", "+3% crit and elemental effect chance for every hero per rank", 3, 250, 0, 1),
            WorldReward(InfernalPactId, "Infernal Pact", "+10% damage and -10% maximum HP per rank (up to 90%). Switch it off any time", 9, 400, 50, 2),
            WorldReward(SoulTitheId, "Soul Tithe", "Guardians drop a heart that heals 2 HP", 1, 350, 0, 2),
            WorldReward(ScholarsRerollId, "Scholar's Reroll", "Reroll the floor talent pick once per world", 1, 450, 0, 3),
            WorldReward(DungeonRun.SanctifiedRelicsId, "Sanctified Relics", "Guardians offer 4 abilities to pick from instead of 3", 1, 700, 0, 3),
            WorldReward(BlackMarketPassId, "Black Market Pass", "Crystal shops stock one extra relic", 1, 500, 0, 4),
            WorldReward(SmugglersStashId, "Smuggler's Stash", "Keep 25% of your unspent crystals (up to 100) for the next descent", 1, 600, 0, 4),
            WorldReward(WildGrowthId, "Wild Growth", "+1 maximum HP for each world you clear within a descent", 1, 600, 0, 5),
            WorldReward(ApexPredatorId, "Apex Predator", "+10% damage against guardians and armoured brutes per rank", 3, 450, 150, 5),
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
            new PermanentUpgradeDefinition("demoness_paralysis", "Pressure Points", "+1 damage to immobilized enemies (paralysed, frozen, stunned or rooted) per rank", 3, 45, 35, WeaponType.Tail),
            new PermanentUpgradeDefinition("demoness_health", "Infernal Blood", "+1 Demoness maximum HP per rank", 3, 35, 25, WeaponType.Tail),
            new PermanentUpgradeDefinition("gambler_pockets", "Deep Pockets", "The Gambler's purse never lets him drop below +1 more coin per rank", 3, 35, 25, WeaponType.Coins),
            new PermanentUpgradeDefinition("gambler_luck", "Lady Luck", "+4% odds on All In per rank", 3, 50, 40, WeaponType.Coins),
            new PermanentUpgradeDefinition("augment_capacitor", "Capacitor Bank", "10% shorter plasma cannon cooldown per rank", 3, 45, 35, WeaponType.Beam),
            new PermanentUpgradeDefinition("augment_health", "Titanium Frame", "+1 Augment maximum HP per rank", 3, 35, 25, WeaponType.Beam),
            new PermanentUpgradeDefinition("reaper_souls", "Soul Echo", "Each soul the Reaper gains has a 10% / 25% / 50% chance by rank to bring an extra soul", 3, 45, 35, WeaponType.Scythe),
            new PermanentUpgradeDefinition("reaper_health", "Deathless", "+1 Reaper maximum HP per rank", 3, 35, 25, WeaponType.Scythe),
            AbilityUnlock(AbilityType.WarBanner, 150),
            AbilityUnlock(AbilityType.BearTrap, 150),
            AbilityUnlock(AbilityType.LightningStorm, 180),
            AbilityUnlock(AbilityType.DeathMark, 160),
            AbilityUnlock(AbilityType.ShadowClone, 200),
            AbilityUnlock(AbilityType.DivineIntervention, 200),
            AbilityUnlock(AbilityType.Suplex, 160),
            AbilityUnlock(AbilityType.NightmareSnap, 180),
            AbilityUnlock(AbilityType.Insurance, 160),
            AbilityUnlock(AbilityType.OrbitalLaser, 200),
            AbilityUnlock(AbilityType.ReapersTechnique, 180),
            Mechanic(WeaponType.Sword, "Shield Taunt", "R: turn red with rage for 2.25s. You can walk; you block bolts from every side (+1 ward each) and draw the enemies' attention"),
            Mechanic(WeaponType.Bow, "Elemental Quiver", "R: cycle fire, freeze and shock arrows. Critical hits set off the arrow's element"),
            Mechanic(WeaponType.Staff, "Wild Storm", "R: after 10 elemental effects, summon a storm that hurls fire, lightning and ice, every strike sure to burn, shock or freeze"),
            Mechanic(WeaponType.Daggers, "Sharpened Dagger", "R: +1 damage for 7.5s; every backstab adds +1 more and refreshes it"),
            Mechanic(WeaponType.Hammer, "Heavenly Host", "R: after 50 blessed bonus damage, angels revive the longest-fallen ally or fully heal the weakest"),
            Mechanic(WeaponType.Fists, "Super Angry", "R: after taking 5 damage, erupt with huge speed, reach, charge speed and damage"),
            Mechanic(WeaponType.Tail, "Demonic Power", "R: after immobilizing enemies 7 times (paralysis, freeze, stun or root), split your tail in two for 10s: tail sweeps cover twice the cone and reach 25% farther"),
            Mechanic(WeaponType.Coins, "The Purse", "R: open your purse, a shop paid for in coins: healing, wards or loaded dice"),
            Mechanic(WeaponType.Beam, "Overclock", "R: after your plasma ray strikes 25 enemies, overclock for 8s: fully charged rays at double speed and a vented, faster cannon"),
            Mechanic(WeaponType.Scythe, "Army of the Dead", "R: spend 3 souls to raise a skeleton that fights for you. Frail and weak, but its health and damage grow with yours, and its blows strike fear for 1 second"),
            // R upgrades: one per class mechanic, sold once the Neon Arcology is cleared and the mechanic itself is owned.
            MechanicUpgrade(WeaponType.Sword, "Retribution", "When Shield Taunt ends, every nearby enemy takes your damage multiplied by the hits you blocked or took while it lasted"),
            MechanicUpgrade(WeaponType.Bow, "Elemental Surge", "Hold R to surge for 6 seconds: every arrow sets off its element, crit or not. Tapping R still cycles elements. 30 second cooldown"),
            MechanicUpgrade(WeaponType.Staff, "Cataclysm", "Wild Storm grows wilder: a far bigger thundercloud that also rains meteors and lightning strikes from the sky onto nearby enemies, on top of its fireballs, zaps and ice bolts"),
            MechanicUpgrade(WeaponType.Daggers, "Razor's Edge", "Sharpened Dagger lasts 10s instead of 7.5s, and while it is up every stab is fully charged instantly"),
            MechanicUpgrade(WeaponType.Hammer, "Seraphim", "Heavenly Host helps 2 allies instead of 1. If only one needs it, you gain a +5 damage blessing for 30 seconds instead"),
            MechanicUpgrade(WeaponType.Fists, "Seeing Red", "While Super Angry, your barrages charge 75% faster"),
            MechanicUpgrade(WeaponType.Coins, "The Safe", "Your purse holds a safe: deposit 10 coins per click, every guardian you defeat multiplies its contents by 1.1, and each withdrawal takes out exactly 50% of it"),
            MechanicUpgrade(WeaponType.Beam, "Missile Rack", "While overclocked, every plasma ray you fire also launches a homing mini missile"),
            MechanicUpgrade(WeaponType.Tail, "Dread Presence", "During Demonic Power, every enemy that comes within 5 units of you is paralysed for 2 seconds"),
            MechanicUpgrade(WeaponType.Scythe, "Avatar of Death", "With 99 souls, R spends 99 instead of raising a skeleton: for 5 seconds you are the incarnation of death, dealing double damage and striking 1 second of fear with every hit")
        });

        private static PermanentUpgradeDefinition[] AddPassives(PermanentUpgradeDefinition[] existing)
        {
            var upgrades = new List<PermanentUpgradeDefinition>(existing);
            foreach (var passive in ClassPassiveCatalog.All)
                upgrades.Add(new PermanentUpgradeDefinition(passive.Id, passive.Name, passive.Description, 1,
                    ClassPassiveCatalog.PassiveCost, 0, passive.Weapon, requiredWorld: passive.RequiredWorld));
            return upgrades.ToArray();
        }

        public const string EmberHeartId = "ember_heart", BackupDriveId = "backup_drive", TargetingChipId = "targeting_chip",
            InfernalPactId = "infernal_pact", SoulTitheId = "soul_tithe", ScholarsRerollId = "scholars_reroll",
            BlackMarketPassId = "black_market_pass", SmugglersStashId = "smugglers_stash", WildGrowthId = "wild_growth", ApexPredatorId = "apex_predator";

        private static PermanentUpgradeDefinition WorldReward(string id, string name, string description, int maxRank, int cost, int step, int world)
            => new PermanentUpgradeDefinition(id, name, description, maxRank, cost, step, null, 0, world);

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

        /// <summary>The upgrade to a class mechanic (R): sold once its world is cleared, to heroes who already own the mechanic.</summary>
        public const int MechanicUpgradeCost = 2500, MechanicUpgradeWorld = 1;
        public static string MechanicUpgradeId(WeaponType weapon) => "mechanic_upgrade_" + weapon.ToString().ToLowerInvariant();

        private static PermanentUpgradeDefinition MechanicUpgrade(WeaponType weapon, string name, string description)
            => new PermanentUpgradeDefinition(MechanicUpgradeId(weapon), name, description, 1, MechanicUpgradeCost, 0, weapon, 0, MechanicUpgradeWorld, MechanicId(weapon));

        private static PermanentUpgradeDefinition Mechanic(WeaponType weapon, string name, string description)
            => new PermanentUpgradeDefinition(MechanicId(weapon), name, description, 1, MechanicCost, 0, weapon, MechanicGuardians);

        public static PermanentUpgradeDefinition Get(string id)
        {
            foreach (var upgrade in All) if (upgrade.Id == id) return upgrade;
            return null;
        }
    }
}
