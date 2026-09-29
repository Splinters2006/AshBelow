using System.Collections.Generic;

namespace Slopgame
{
    public enum PowerupType
    {
        Damage, Vitality, Movement, AttackSpeed, CriticalHits, Armor, LifeSteal, DodgeRecovery, SweepingEdge, Riposte, QuickDraw, Bodkin,
        LightningRange, LightningPower, LightningChains, RushPower, ShatterRadius, AegisDuration, VolleyCount, PiercingPower,
        WindstepDistance, FireballRadius, FrostDuration, BlinkDistance, Backstab, AssassinCrit, KnifeCount, VenomDuration,
        VeilDuration, PaladinWard, HealingPower, JudgmentPower, SanctuarySize,
        RiftReach, AbyssalPower, EclipseRadius, SoulRendPower, ReignDuration,
        Flurry, Adrenaline, ExtraFilling, CraterMaker, Bloodlust,
        NerveStrike, LongTail, Bloodline, BigPaws, HexMastery,
        LooseChange, LongToss, MintCondition, RiggedOdds, HighRoller,
        NerveSnap, PyreBurst, Kindling, Massacre, Momentum, Bloodrush, StillHunter, Prospector, Haggler, MerchantsFavor
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
            new PowerupDefinition(PowerupType.AttackSpeed, "Quick Hands", "+20% base attack and charge speed", 5),
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
            new PowerupDefinition(PowerupType.LightningChains, "Conductivity", "Unlock chaining; +1 chain target per rank", 2, WeaponType.Staff),
            new PowerupDefinition(PowerupType.RushPower, "Battering Ram", "+2 Shield Rush damage", 3, WeaponType.Sword, AbilityType.ShieldRush),
            new PowerupDefinition(PowerupType.ShatterRadius, "Fault Line", "+0.5 Earthshatter radius", 3, WeaponType.Sword, AbilityType.Earthshatter),
            new PowerupDefinition(PowerupType.AegisDuration, "Unbroken", "+0.4 seconds of Aegis protection", 3, WeaponType.Sword, AbilityType.Aegis),
            new PowerupDefinition(PowerupType.VolleyCount, "Rain of Arrows", "+4 arrows in each Arrow Volley", 3, WeaponType.Bow, AbilityType.Volley),
            new PowerupDefinition(PowerupType.PiercingPower, "Armor Breaker", "+2 Piercing Shot damage", 3, WeaponType.Bow, AbilityType.PiercingShot),
            new PowerupDefinition(PowerupType.WindstepDistance, "Tailwind", "+0.5 Windstep distance", 3, WeaponType.Bow, AbilityType.Windstep),
            new PowerupDefinition(PowerupType.FireballRadius, "Wildfire", "+0.4 Inferno Orb explosion radius", 3, WeaponType.Staff, AbilityType.Fireball),
            new PowerupDefinition(PowerupType.FrostDuration, "Deep Freeze", "+0.5 seconds of Frost Nova freeze", 3, WeaponType.Staff, AbilityType.FrostNova),
            new PowerupDefinition(PowerupType.BlinkDistance, "Phasewalker", "+0.5 Arcane Blink distance", 3, WeaponType.Staff, AbilityType.Blink),
            new PowerupDefinition(PowerupType.Backstab, "Hidden Blade", "+1 damage on physical rear hits", 3, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.AssassinCrit, "Killer Instinct", "+5% physical critical chance", 3, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.KnifeCount, "Blade Storm", "+2 knives in Fan of Knives", 3, WeaponType.Daggers, AbilityType.FanOfKnives),
            new PowerupDefinition(PowerupType.VenomDuration, "Lingering Venom", "+1 second of Venom Vial's toxic pool", 3, WeaponType.Daggers, AbilityType.VenomVial),
            new PowerupDefinition(PowerupType.VeilDuration, "Long Shadows", "+0.4 seconds of Shadow Veil", 3, WeaponType.Daggers, AbilityType.ShadowVeil),
            new PowerupDefinition(PowerupType.PaladinWard, "Blessed Guard", "+1 protective ward each floor", 3, WeaponType.Hammer),
            new PowerupDefinition(PowerupType.HealingPower, "Restoration", "+1 HP restored by Healing Light", 3, WeaponType.Hammer, AbilityType.HealingLight),
            new PowerupDefinition(PowerupType.JudgmentPower, "Righteous Fury", "+2 Judgment damage", 3, WeaponType.Hammer, AbilityType.Judgment),
            new PowerupDefinition(PowerupType.SanctuarySize, "Sacred Ground", "+0.5 Sanctuary bubble radius", 3, WeaponType.Hammer, AbilityType.Sanctuary),
            new PowerupDefinition(PowerupType.RiftReach, "Endless Night", "+1.5 shadow rift range", 3, WeaponType.Shadow),
            new PowerupDefinition(PowerupType.AbyssalPower, "Abyssal Power", "+16 shadow rift damage before charge scaling", 3, WeaponType.Shadow),
            new PowerupDefinition(PowerupType.EclipseRadius, "Event Horizon", "+1 Eclipse execution radius", 3, WeaponType.Shadow, AbilityType.Eclipse),
            new PowerupDefinition(PowerupType.SoulRendPower, "Soul Devourer", "+1x fully charged damage to Soul Rend", 3, WeaponType.Shadow, AbilityType.SoulRend),
            new PowerupDefinition(PowerupType.ReignDuration, "Eternal Reign", "+1 second of Shadow Reign", 3, WeaponType.Shadow, AbilityType.ShadowReign),
            new PowerupDefinition(PowerupType.Flurry, "Brawler: Flurry", "+1 punch per charged barrage", 3, WeaponType.Fists),
            new PowerupDefinition(PowerupType.Adrenaline, "Brawler: Adrenaline", "+1 second of Empower", 3, WeaponType.Fists),
            new PowerupDefinition(PowerupType.ExtraFilling, "Extra Filling", "+0.4 Knuckle Sandwich length and width", 3, WeaponType.Fists, AbilityType.KnuckleSandwich),
            new PowerupDefinition(PowerupType.CraterMaker, "Crater Maker", "+0.5 Wild Leap slam radius", 3, WeaponType.Fists, AbilityType.WildLeap),
            new PowerupDefinition(PowerupType.Bloodlust, "Bloodlust", "+1 second of Primal Rage", 3, WeaponType.Fists, AbilityType.PrimalRage),
            new PowerupDefinition(PowerupType.NerveStrike, "Demoness: Nerve Strike", "+0.25 seconds of vital stab paralysis", 3, WeaponType.Tail),
            new PowerupDefinition(PowerupType.LongTail, "Demoness: Long Tail", "+0.3 tail sweep reach", 3, WeaponType.Tail),
            new PowerupDefinition(PowerupType.Bloodline, "Bloodline", "+1 second of Archdemon's Technique", 3, WeaponType.Tail, AbilityType.ArchdemonTechnique),
            new PowerupDefinition(PowerupType.BigPaws, "Good Boy", "+0.4 HEEEELP slam radius", 3, WeaponType.Tail, AbilityType.DemonPaw),
            new PowerupDefinition(PowerupType.HexMastery, "Hex Mastery", "+0.5 seconds of Demon Curse paralysis", 3, WeaponType.Tail, AbilityType.DemonCurse),
            new PowerupDefinition(PowerupType.LooseChange, "Gambler: Loose Change", "+5% chance for a gold coin picked up to be worth an extra coin", 3, WeaponType.Coins),
            new PowerupDefinition(PowerupType.LongToss, "Gambler: Long Toss", "+1 coin throw and volley range", 3, WeaponType.Coins),
            new PowerupDefinition(PowerupType.MintCondition, "Mint Condition", "+2 coins from Windfall", 3, WeaponType.Coins, AbilityType.Windfall),
            new PowerupDefinition(PowerupType.RiggedOdds, "Rigged Odds", "+5% odds on All In", 3, WeaponType.Coins, AbilityType.AllIn),
            new PowerupDefinition(PowerupType.HighRoller, "High Roller", "+2 seconds of Jackpot buffs", 3, WeaponType.Coins, AbilityType.Jackpot),
            // Universal boons: any class can find them as room rewards or buy them as crystal shop relics.
            new PowerupDefinition(PowerupType.NerveSnap, "Nerve Snap", "Killing a paralysed or frozen enemy resets your class skill", 1),
            new PowerupDefinition(PowerupType.PyreBurst, "Pyre Burst", "Burning enemies explode when they die, hurting everything nearby", 1),
            new PowerupDefinition(PowerupType.Kindling, "Kindling", "Every elemental effect you set off also sets the enemy burning", 1),
            new PowerupDefinition(PowerupType.Massacre, "Massacre", "Killing 5 enemies within 1 second resets your class skill", 1),
            new PowerupDefinition(PowerupType.Momentum, "Momentum", "Killing 2 enemies within 1 second resets your dodge", 1),
            new PowerupDefinition(PowerupType.Bloodrush, "Bloodrush", "Every kill takes 0.5 seconds off all your cooldowns", 1),
            new PowerupDefinition(PowerupType.StillHunter, "Still Hunter", "Killing a paralysed or frozen enemy takes 0.5 seconds off all your cooldowns", 1),
            new PowerupDefinition(PowerupType.Prospector, "Prospector", "Kills have a 50% / 100% chance by rank to drop extra crystals", 2),
            new PowerupDefinition(PowerupType.Haggler, "Haggler", "Crystal shop prices 25% / 50% lower by rank", 2),
            new PowerupDefinition(PowerupType.MerchantsFavor, "Merchant's Favor", "One free reroll of every crystal shop's wares", 1)
        };

        public static PowerupDefinition Get(PowerupType type) => All[(int)type];
    }
}
