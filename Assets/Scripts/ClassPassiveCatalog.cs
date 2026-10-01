namespace Slopgame
{
    /// <summary>
    /// A hero's passive: always on, never bought. It unlocks for good once its world has been cleared (by any hero).
    /// </summary>
    public sealed class ClassPassiveDefinition
    {
        public WeaponType Weapon { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>The world that must be cleared once before the passive is active.</summary>
        public int RequiredWorld { get; }
        public ClassPassiveDefinition(WeaponType weapon, string name, string description, int requiredWorld = ClassPassiveCatalog.PassiveWorld)
        {
            Weapon = weapon; Name = name; Description = description; RequiredWorld = requiredWorld;
        }
    }

    public static class ClassPassiveCatalog
    {
        /// <summary>Passives unlock when the Infernal Court's third guardian falls.</summary>
        public const int PassiveWorld = 2;

        public static readonly ClassPassiveDefinition[] All =
        {
            new ClassPassiveDefinition(WeaponType.Sword, "Turnabout", "Gain 2 wards whenever a bolt you reflected kills an enemy or strikes a guardian"),
            new ClassPassiveDefinition(WeaponType.Bow, "Steady Hand", "Every arrow and arrow ability has a 20% chance to be fully charged. Abilities and Triple Shot deal 25% more damage when they are"),
            new ClassPassiveDefinition(WeaponType.Staff, "Elemental Mastery", "Your shocks start with 1.5x the base radius, your burns last 1.5x the base ticks and your freezes last 1.5x as long"),
            new ClassPassiveDefinition(WeaponType.Daggers, "Killer Instinct", "Backstabs with basic attacks deal double damage; every other backstab deals 15% more"),
            new ClassPassiveDefinition(WeaponType.Hammer, "Overflowing Faith", "Keep holding a fully charged blessing to overcharge it: +3 damage instead of +2, over a wider area"),
            new ClassPassiveDefinition(WeaponType.Tail, "Demonic Runes", "Enemies that die while immobilized have a 10% chance to drop a demonic rune. Pick it up to reset your Tail Sweep cooldown, and every hit you land paralyses for 5 seconds"),
            new ClassPassiveDefinition(WeaponType.Beam, "Salvage", "Enemies you land the final hit on have a 5% chance to drop 1 scrap. Every 3 scrap heals you for 1 HP"),
            new ClassPassiveDefinition(WeaponType.Coins, "Compound Interest", "Every guardian you defeat multiplies the contents of The Safe by 1.25 instead of 1.1"),
            new ClassPassiveDefinition(WeaponType.Scythe, "Death's Bargain", "Everything that costs souls costs 1 soul less: skulls, Feast!, Army of the Dead and Avatar of Death"),
            new ClassPassiveDefinition(WeaponType.Fists, "Mastered Technique", "Rolling no longer cuts your barrage short: you keep punching straight through the dodge"),
        };

        /// <summary>The hero's passive; null while they have none.</summary>
        public static ClassPassiveDefinition Get(WeaponType weapon)
        {
            foreach (var passive in All) if (passive.Weapon == weapon) return passive;
            return null;
        }

        public static bool IsUnlocked(PermanentProgress progress, WeaponType weapon)
        {
            var passive = Get(weapon);
            return passive != null && progress != null && progress.HasClearedWorld(passive.RequiredWorld);
        }
    }
}
