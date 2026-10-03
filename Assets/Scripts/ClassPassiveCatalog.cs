namespace Slopgame
{
    /// <summary>
    /// A hero's passive: always active once purchased after its world has been cleared (by any hero). Each hero also has
    /// a second passive, sold once their first is owned and Aurelion, the Grand Magister, has fallen.
    /// </summary>
    public sealed class ClassPassiveDefinition
    {
        public WeaponType Weapon { get; }
        /// <summary>True for a hero's second passive.</summary>
        public bool IsSecond { get; }
        public string Id => (IsSecond ? "passive2_" : "passive_") + Weapon.ToString().ToLowerInvariant();
        public string Name { get; }
        public string Description { get; }
        /// <summary>The world that must be cleared once before the passive can be purchased (-1 for none).</summary>
        public int RequiredWorld { get; }
        public ClassPassiveDefinition(WeaponType weapon, string name, string description, int requiredWorld = ClassPassiveCatalog.PassiveWorld, bool second = false)
        {
            Weapon = weapon; Name = name; Description = description; RequiredWorld = requiredWorld; IsSecond = second;
        }
    }

    public static class ClassPassiveCatalog
    {
        /// <summary>Passives become available to buy when the Infernal Court's third guardian falls.</summary>
        public const int PassiveWorld = 2;
        public const int PassiveCost = 5000;
        /// <summary>Second passives cost the same and unlock once this guardian has been slain (by any hero).</summary>
        public const int SecondPassiveCost = 5000;
        public const string SecondPassiveGuardian = GrandMagisterBoss.FixedTitle, SecondPassiveGuardianName = "Aurelion, the Grand Magister";

        // Both lists follow the hero roster's order (see HeroRoster).
        public static readonly ClassPassiveDefinition[] All =
        {
            new ClassPassiveDefinition(WeaponType.Sword, "Turnabout", "Gain 2 wards whenever a bolt you reflected kills an enemy or strikes a guardian"),
            new ClassPassiveDefinition(WeaponType.Bow, "Steady Hand", "Every arrow and arrow ability has a 20% chance to be fully charged. Abilities and Triple Shot deal 25% more damage when they are"),
            new ClassPassiveDefinition(WeaponType.Staff, "Elemental Mastery", "Your shocks start with 1.5x the base radius, your burns last 1.5x the base ticks and your freezes last 1.5x as long"),
            new ClassPassiveDefinition(WeaponType.Daggers, "Killer Instinct", "Backstabs with basic attacks deal double damage; every other backstab deals 15% more"),
            new ClassPassiveDefinition(WeaponType.Hammer, "Overflowing Faith", "Keep holding a fully charged blessing to overcharge it: +3 damage instead of +2, over a wider area"),
            new ClassPassiveDefinition(WeaponType.Fists, "Mastered Technique", "Rolling no longer cuts your barrage short: you keep punching straight through the dodge"),
            new ClassPassiveDefinition(WeaponType.Tail, "Demonic Runes", "Enemies that die while immobilized have a 25% chance to drop a demonic rune, and so does every hit you land on an immobilized guardian. Pick it up to reset your Tail Sweep cooldown, and every hit you land paralyses for 5 seconds"),
            new ClassPassiveDefinition(WeaponType.Coins, "Compound Interest", "Every guardian you defeat multiplies the contents of The Safe by 1.25 instead of 1.1"),
            new ClassPassiveDefinition(WeaponType.Beam, "Salvage", "Enemies you land the final hit on have a 5% chance to drop 1 scrap. Every 3 scrap heals you for 1 HP"),
            new ClassPassiveDefinition(WeaponType.Scythe, "Death's Bargain", "Skulls, Feast! and Army of the Dead cost 1 soul less. Avatar of Death costs 50 souls"),
            new ClassPassiveDefinition(WeaponType.Katana, "Crimson Bloom", "Enemies that die while bleeding burst: every enemy within 2.5 units takes the bleed damage they had left and starts bleeding. The more maximum health the fallen had, the wider the burst"),
        };

        public static readonly ClassPassiveDefinition[] Second =
        {
            Two(WeaponType.Sword, "Bulwark", "Deal +1 damage for every HP and every ward you currently have"),
            Two(WeaponType.Bow, "Barbed Arrows", "Your critical strikes make the target bleed: 10 ticks over 5 seconds, each for 10% of the hit"),
            Two(WeaponType.Staff, "Brittle Ice", "Freezing an enemy makes it brittle: the next hit it takes deals double damage"),
            Two(WeaponType.Daggers, "Fade Away", "Killing an enemy with a backstab briefly hides you from enemies"),
            Two(WeaponType.Hammer, "Shared Shelter", "An overcharged blessing also grants 4 wards, split evenly between everyone it blesses; you keep any left over (with 3 heroes: 2 for you, 1 each for the others)"),
            Two(WeaponType.Fists, "Final Blow", "Every barrage ends in an even stronger punch that hits harder over a bigger area"),
            Two(WeaponType.Tail, "Rune Burst", "Picking up a demonic rune sets off a blast that paralyses everything within 5 units, even through walls"),
            Two(WeaponType.Coins, "Card Shark", "Your purse sells a deck of 52 cards for 52 coins: every coin you throw (left or right click) throws a card with it for half that coin's damage, until the deck runs out"),
            Two(WeaponType.Beam, "Scavenger", "Salvage's scrap drops twice as often: a 10% chance from every enemy you finish"),
            Two(WeaponType.Scythe, "Death's Discount", "Avatar of Death costs only 25 souls"),
            Two(WeaponType.Katana, "Trail of Blood", "Killing a bleeding enemy gives +1 base damage for 5 seconds. Each kill adds its own stack, and stacks never refresh"),
        };

        private static ClassPassiveDefinition Two(WeaponType weapon, string name, string description) => new ClassPassiveDefinition(weapon, name, description, -1, true);

        /// <summary>The hero's passive; null while they have none.</summary>
        public static ClassPassiveDefinition Get(WeaponType weapon)
        {
            foreach (var passive in All) if (passive.Weapon == weapon) return passive;
            return null;
        }

        /// <summary>The hero's second passive; null while they have none.</summary>
        public static ClassPassiveDefinition GetSecond(WeaponType weapon)
        {
            foreach (var passive in Second) if (passive.Weapon == weapon) return passive;
            return null;
        }

        /// <summary>True when this upgrade id is one of the passives, first or second.</summary>
        public static bool IsPassive(string id)
        {
            foreach (var passive in All) if (passive.Id == id) return true;
            foreach (var passive in Second) if (passive.Id == id) return true;
            return false;
        }

        public static bool IsUnlocked(PermanentProgress progress, WeaponType weapon)
        {
            var passive = Get(weapon);
            return passive != null && progress != null && progress.HasClearedWorld(passive.RequiredWorld) && progress.Rank(passive.Id) > 0;
        }

        /// <summary>The second passive works once owned, as long as the first one is owned too.</summary>
        public static bool IsSecondUnlocked(PermanentProgress progress, WeaponType weapon)
        {
            var passive = GetSecond(weapon);
            return passive != null && IsUnlocked(progress, weapon) && progress.Rank(passive.Id) > 0;
        }
    }
}
