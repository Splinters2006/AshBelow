using System.Collections.Generic;

namespace Slopgame
{
    public enum PowerupType
    {
        Damage, Vitality, Movement, AttackSpeed, CriticalHits, Armor, LifeSteal, DodgeRecovery, SweepingEdge, Riposte, QuickDraw, Bodkin,
        LightningRange, LightningPower, LightningChains, RushPower, ShatterRadius, AegisDuration, VolleyCount, PiercingPower,
        WindstepDistance, FireballRadius, FrostDuration, BlinkDistance, Backstab, AssassinCrit, KnifeCount, VenomDuration,
        VeilDuration, PaladinWard, HealingPower, JudgmentPower, SanctuaryDuration
    }

    public sealed class PowerupDefinition
    {
        public PowerupType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaxStacks { get; }
        public WeaponType? ClassWeapon { get; }
        public AbilityType RequiredAbility { get; }
        public PowerupDefinition(PowerupType type, string name, string description, int maxStacks, WeaponType? classWeapon = null, AbilityType requiredAbility = AbilityType.None)
        {
            Type = type; Name = name; Description = description; MaxStacks = maxStacks;
            ClassWeapon = classWeapon;
            RequiredAbility = requiredAbility;
        }
    }

    public static class PowerupCatalog
    {
        public static readonly IReadOnlyList<PowerupDefinition> All = new[]
        {
            new PowerupDefinition(PowerupType.Damage, "Keen Edge", "+1 base attack damage", int.MaxValue),
            new PowerupDefinition(PowerupType.Vitality, "Vitality", "+2 maximum HP and fully heal", int.MaxValue),
            new PowerupDefinition(PowerupType.Movement, "Fleet Foot", "+0.7 movement speed", 5),
            new PowerupDefinition(PowerupType.AttackSpeed, "Quick Hands", "+20% base attack speed", 5),
            new PowerupDefinition(PowerupType.CriticalHits, "Precision", "+10% physical crit / elemental effect chance", 5),
            new PowerupDefinition(PowerupType.Armor, "Ward", "Block one extra hit each floor; refill on descent", 3),
            new PowerupDefinition(PowerupType.LifeSteal, "Soul Harvest", "Heal 1 HP every 5 / 4 / 3 kills by rank", 3),
            new PowerupDefinition(PowerupType.DodgeRecovery, "Second Wind", "10% shorter dodge cooldown per rank", 3),
            new PowerupDefinition(PowerupType.SweepingEdge, "Knight: Sweeping Edge", "+15 degrees to fully charged slash cone", 2, WeaponType.Sword),
            new PowerupDefinition(PowerupType.Riposte, "Knight: Riposte", "+1 damage to reflected bolts", 3, WeaponType.Sword),
            new PowerupDefinition(PowerupType.QuickDraw, "Archer: Quick Draw", "15% shorter bow charge time per rank", 2, WeaponType.Bow),
            new PowerupDefinition(PowerupType.Bodkin, "Archer: Bodkin", "+0.5x maximum charged arrow damage", 2, WeaponType.Bow),
            new PowerupDefinition(PowerupType.LightningRange, "Storm Reach", "+1 lightning cast range and +0.5 jump range", 3, WeaponType.Staff),
            new PowerupDefinition(PowerupType.LightningPower, "High Voltage", "+1 damage per lightning strike", 3, WeaponType.Staff),
            new PowerupDefinition(PowerupType.LightningChains, "Conductivity", "+1 enemy in each lightning chain", 2, WeaponType.Staff),
            new PowerupDefinition(PowerupType.RushPower, "Battering Ram", "+2 Shield Rush damage", 3, WeaponType.Sword, AbilityType.ShieldRush),
            new PowerupDefinition(PowerupType.ShatterRadius, "Fault Line", "+0.4 Earthshatter radius", 3, WeaponType.Sword, AbilityType.Earthshatter),
            new PowerupDefinition(PowerupType.AegisDuration, "Unbroken", "+0.4 seconds of Aegis protection", 3, WeaponType.Sword, AbilityType.Aegis),
            new PowerupDefinition(PowerupType.VolleyCount, "Rain of Arrows", "+2 arrows per Volley", 3, WeaponType.Bow, AbilityType.Volley),
            new PowerupDefinition(PowerupType.PiercingPower, "Armor Breaker", "+2 Piercing Shot damage", 3, WeaponType.Bow, AbilityType.PiercingShot),
            new PowerupDefinition(PowerupType.WindstepDistance, "Tailwind", "+0.5 Windstep distance", 3, WeaponType.Bow, AbilityType.Windstep),
            new PowerupDefinition(PowerupType.FireballRadius, "Wildfire", "+0.4 Inferno Orb explosion radius", 3, WeaponType.Staff, AbilityType.Fireball),
            new PowerupDefinition(PowerupType.FrostDuration, "Deep Freeze", "+1 second of Frost Nova chill", 3, WeaponType.Staff, AbilityType.FrostNova),
            new PowerupDefinition(PowerupType.BlinkDistance, "Phasewalker", "+0.5 Arcane Blink distance", 3, WeaponType.Staff, AbilityType.Blink),
            new PowerupDefinition(PowerupType.Backstab, "Hidden Blade", "+1 damage on physical rear hits", 3, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.AssassinCrit, "Killer Instinct", "+5% physical critical chance", 3, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.KnifeCount, "Blade Storm", "+2 knives in Fan of Knives", 3, WeaponType.Daggers, AbilityType.FanOfKnives),
            new PowerupDefinition(PowerupType.VenomDuration, "Lingering Venom", "+1 Venom Strike poison tick", 3, WeaponType.Daggers, AbilityType.VenomStrike),
            new PowerupDefinition(PowerupType.VeilDuration, "Long Shadows", "+0.4 seconds of Shadow Veil", 3, WeaponType.Daggers, AbilityType.ShadowVeil),
            new PowerupDefinition(PowerupType.PaladinWard, "Blessed Guard", "+1 protective ward each floor", 3, WeaponType.Hammer),
            new PowerupDefinition(PowerupType.HealingPower, "Restoration", "+1 HP restored by Healing Light", 3, WeaponType.Hammer, AbilityType.HealingLight),
            new PowerupDefinition(PowerupType.JudgmentPower, "Righteous Fury", "+2 Judgment damage", 3, WeaponType.Hammer, AbilityType.Judgment),
            new PowerupDefinition(PowerupType.SanctuaryDuration, "Sacred Ground", "+0.4 seconds of Sanctuary protection", 3, WeaponType.Hammer, AbilityType.Sanctuary)
        };

        public static PowerupDefinition Get(PowerupType type) => All[(int)type];
    }
}
