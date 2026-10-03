using System.Collections.Generic;

namespace Slopgame
{
    public enum PowerupType
    {
        Damage, Vitality, Movement, AttackSpeed, CriticalHits, Armor, LifeSteal, DodgeRecovery, SweepingEdge, Riposte, QuickDraw, Bodkin,
        LightningRange, LightningPower, LightningChains, RushPower, ShatterRadius, AegisDuration, VolleyCount, PiercingPower,
        WindstepDistance, FireballRadius, FrostDuration, BlinkDistance, Backstab, AssassinCrit, KnifeCount, VenomDuration,
        VeilDuration, PaladinWard, HealingPower, JudgmentPower, SanctuarySize,
        // The Specimen's Bulk talents took over the retired Admin's slots (their numbers are their IDs).
        BulkUp, IronForearms, BoneBreaker, Aftershock, ReturnToSender,
        Flurry, Adrenaline, ExtraFilling, CraterMaker, Bloodlust,
        NerveStrike, LongTail, Bloodline, BigPaws, HexMastery,
        LooseChange, LongToss, MintCondition, RiggedOdds, HighRoller,
        NerveSnap, PyreBurst, Kindling, Massacre, Momentum, Bloodrush, StillHunter, Prospector, Haggler, MerchantsFavor,
        FocusedLens, Overcharge, Payload, Afterburner, ExtendedBattery,
        DeadlyPrecision, SlowBurn, Permafrost, StaticField, RelicTraining, SoulShield,
        GuardDrills, Longsword, VolleyDrills, Farshot, StormRhythm, Stormcraft, ShadowDance, LongDaggers,
        DivineCadence, PatientFaith, SecondRound, HeavyGloves, TailRhythm, CruelTouch, QuickDeal, LuckyStreak,
        HeatSink, WideBeam, Immovable, RootedStance,
        // Universal talents from the talents & abilities expansion.
        GlassCannon, LastStand, Berserker, OpeningStrike, Executioner, Rhythm, Spellblade, Overkill, ElementalKills, Thorns,
        CloseCall, CheatDeath,
        // Knight.
        Retaliation, Counterweight, Cleave, Bastion, GlacialRush, AegisBurst,
        // Archer.
        HuntersMark, Sniper, PointBlank, QuickNock, StormVolley, SplittingShot,
        // Wizard.
        ElementalClash, ArcaneEcho, Shatter, Pyromancer, StormChargedOrb, Frostblink,
        // Assassin.
        Bleed, Vanish, Ambush, Poisoner, BloodTrail, VenomKnives,
        // Paladin.
        Zeal, HolyGround, Retribution, Shepherd, BlessedJudgment, HealingSanctuary,
        // Brawler.
        ComboCounter, Footwork, Brawl, Knockout, MeteorLeap, SandwichSpecial,
        // Demoness.
        LingeringTerror, Torment, DreadAura, BloodPact, CursedPaw, InfernalTechnique,
        // Gambler.
        Ricochet, GoldCoin, CompoundInterest, HeadsOrTails, TipJar, SnakeEyes,
        // Augment.
        ThermalVent, Overheat, TargetingArray, ReactivePlating, Coolant, MissileTurret,
        // Universal talents built around immobilized enemies (paralysed, frozen, stunned or rooted).
        SittingDuck, IronGrip, SearingHold, StaticHold, NumbingHold, Domino,
        // Universal: a big one-off attack speed boost. Always append new talents here; their numbers are their IDs.
        Frenzy,
        // Reaper.
        GraveTithe,
        // Universal: elements set enemies up to be held.
        ElementalImmobilization,
        // Reaper.
        SoulFury,
        // Samurai.
        DeepWounds, BloodInTheWater, JaggedBlade, FineDicing, BledDry,
        // The Specimen's Edge talents, then his hybrids.
        RazorTip, LongChain, WeightedTip, ReelIn, Featherweight, BleedingEdge, LightOnHisFeet,
        FreightTrain, RubbleWall, Hardened, Zipline, LowBlow, Shackles,
        // Universal: pickups home in harder.
        Lodestone,
        // Reaper.
        SoulBurst, SoulHoard, GrimHarvest,
        // Samurai.
        PinnedWounds,
        // Universal: a kill quickens the next charge.
        HotStreak,
        // Universal: bleeding freezes.
        ColdBlooded,
        // Universal: shocks freeze the enemies they arc into.
        FlashFreeze,
        // Universal: burning and bleeding combine.
        Scorchblood,
        // Samurai.
        BloodDebt
    }

    public sealed class PowerupDefinition
    {
        public PowerupType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public int MaxStacks { get; }
        public WeaponType? ClassWeapon { get; }
        public AbilityType RequiredAbility { get; }
        /// <summary>
        /// A talent taken out of the game: it keeps its slot (talent numbers are their IDs) but is never offered, sold or
        /// listed, and cannot be taken.
        /// </summary>
        public bool Retired { get; }
        public PowerupDefinition(PowerupType type, string name, string description, int maxStacks, WeaponType? classWeapon = null, AbilityType requiredAbility = AbilityType.None,
            bool retired = false)
        {
            Type = type; Name = name; Description = description; MaxStacks = maxStacks;
            ClassWeapon = classWeapon;
            RequiredAbility = requiredAbility;
            Retired = retired;
        }
    }

    public static class PowerupCatalog
    {
        public static readonly IReadOnlyList<PowerupDefinition> All = new[]
        {
            new PowerupDefinition(PowerupType.Damage, "Keen Edge", "+1 base attack damage", 5),
            new PowerupDefinition(PowerupType.Vitality, "Vitality", "+2 maximum HP and fully heal", 5),
            new PowerupDefinition(PowerupType.Movement, "Fleet Foot", "+0.7 movement speed", 5),
            new PowerupDefinition(PowerupType.AttackSpeed, "Quick Hands", "+20% base attack and charge speed", 5),
            new PowerupDefinition(PowerupType.CriticalHits, "Precision", "+10% physical crit / elemental effect chance", 5),
            new PowerupDefinition(PowerupType.Armor, "Ward", "Block one extra hit each floor; refill on descent", 3),
            // Soul Harvest is retired: its slot stays so later talents keep their numbers.
            new PowerupDefinition(PowerupType.LifeSteal, "Soul Harvest", "Retired", 3, retired: true),
            new PowerupDefinition(PowerupType.DodgeRecovery, "Second Wind", "10% shorter dodge cooldown per rank", 3),
            new PowerupDefinition(PowerupType.SweepingEdge, "Knight: Sweeping Edge", "+15 degrees to fully charged slash cone", 2, WeaponType.Sword),
            new PowerupDefinition(PowerupType.Riposte, "Knight: Riposte", "+1 damage to reflected bolts", 3, WeaponType.Sword),
            new PowerupDefinition(PowerupType.QuickDraw, "Archer: Quick Draw", "15% shorter bow charge time per rank", 2, WeaponType.Bow),
            new PowerupDefinition(PowerupType.Bodkin, "Archer: Bodkin", "+0.5x maximum charged arrow damage", 2, WeaponType.Bow),
            new PowerupDefinition(PowerupType.LightningRange, "Storm Reach", "+1 Zap cast range and +0.5 jump range", 3, WeaponType.Staff),
            new PowerupDefinition(PowerupType.LightningPower, "High Voltage", "+1 damage per Zap", 3, WeaponType.Staff),
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
            new PowerupDefinition(PowerupType.BulkUp, "Bulk: Bulk Up", "+2 maximum HP and heal 2", 3, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.IronForearms, "Bulk: Iron Forearms", "Arm Guard holds 1 more Force and lasts 1 second longer", 3, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.BoneBreaker, "Bulk: Bone Breaker", "Wall slams deal +1 damage", 3, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.Aftershock, "Bulk: Aftershock", "+0.5 Ground Pound radius", 3, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.ReturnToSender, "Bulk: Return to Sender", "Bolts thrown back by Repel pierce through enemies and deal +1 damage", 2, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.Flurry, "Brawler: Flurry", "+1 punch per charged barrage", 3, WeaponType.Fists),
            new PowerupDefinition(PowerupType.Adrenaline, "Brawler: Adrenaline", "+1 second of Empower", 3, WeaponType.Fists),
            new PowerupDefinition(PowerupType.ExtraFilling, "Extra Filling", "+0.4 Knuckle Sandwich length and width", 3, WeaponType.Fists, AbilityType.KnuckleSandwich),
            new PowerupDefinition(PowerupType.CraterMaker, "Crater Maker", "+0.5 Wild Leap slam radius", 3, WeaponType.Fists, AbilityType.WildLeap),
            new PowerupDefinition(PowerupType.Bloodlust, "Bloodlust", "+1 second of Primal Rage", 3, WeaponType.Fists, AbilityType.PrimalRage),
            new PowerupDefinition(PowerupType.NerveStrike, "Demoness: Nerve Strike", "+0.25 seconds on all your paralysis", 3, WeaponType.Tail),
            new PowerupDefinition(PowerupType.LongTail, "Demoness: Long Tail", "+0.5 tail sweep reach", 3, WeaponType.Tail),
            new PowerupDefinition(PowerupType.Bloodline, "Bloodline", "+1 second of Archdemon's Technique", 3, WeaponType.Tail, AbilityType.ArchdemonTechnique),
            new PowerupDefinition(PowerupType.BigPaws, "Good Boy", "+0.4 HEEEELP slam radius", 3, WeaponType.Tail, AbilityType.DemonPaw),
            new PowerupDefinition(PowerupType.HexMastery, "Hex Mastery", "Enemies under your Demon Curse take 25% more damage per rank", 3, WeaponType.Tail, AbilityType.DemonCurse),
            new PowerupDefinition(PowerupType.LooseChange, "Gambler: Loose Change", "+5% chance for a gold coin picked up to be worth an extra coin", 3, WeaponType.Coins),
            new PowerupDefinition(PowerupType.LongToss, "Gambler: Long Toss", "+1 coin throw and volley range", 3, WeaponType.Coins),
            new PowerupDefinition(PowerupType.MintCondition, "Mint Condition", "+2 coins from Windfall", 3, WeaponType.Coins, AbilityType.Windfall),
            new PowerupDefinition(PowerupType.RiggedOdds, "Rigged Odds", "+5% odds on All In", 3, WeaponType.Coins, AbilityType.AllIn),
            new PowerupDefinition(PowerupType.HighRoller, "High Roller", "+2 seconds of Jackpot buffs", 3, WeaponType.Coins, AbilityType.Jackpot),
            // Universal boons: any class can find them as room rewards or buy them as crystal shop relics.
            new PowerupDefinition(PowerupType.NerveSnap, "Nerve Snap", "Killing an immobilized enemy (paralysed, frozen, stunned or rooted) resets your class skill", 1),
            new PowerupDefinition(PowerupType.PyreBurst, "Pyre Burst", "Burning enemies explode when they die, hurting everything nearby. Each rank widens the blast (1.5 / 2.25 / 3 units)", 3),
            new PowerupDefinition(PowerupType.Kindling, "Kindling", "Every elemental effect you set off also sets the enemy burning", 1),
            new PowerupDefinition(PowerupType.Massacre, "Massacre", "Killing 3 enemies with one attack or skill resets your class skill", 1),
            new PowerupDefinition(PowerupType.Momentum, "Momentum", "Killing 2 enemies within 1 second resets your dodge", 1),
            new PowerupDefinition(PowerupType.Bloodrush, "Bloodrush", "Every kill takes 0.5 seconds off all your cooldowns", 1),
            new PowerupDefinition(PowerupType.StillHunter, "Still Hunter", "Killing an immobilized enemy (paralysed, frozen, stunned or rooted) takes 1 second off all your cooldowns", 1),
            new PowerupDefinition(PowerupType.Prospector, "Prospector", "Kills have a 50% / 100% chance by rank to drop extra crystals", 2),
            new PowerupDefinition(PowerupType.Haggler, "Haggler", "Crystal shop prices 25% / 50% lower by rank", 2),
            new PowerupDefinition(PowerupType.MerchantsFavor, "Merchant's Favor", "One free reroll of every crystal shop's wares", 1),
            // The Augment's talents; they sit after the universal boons so the catalog keeps PowerupType's order.
            new PowerupDefinition(PowerupType.FocusedLens, "Augment: Focused Lens", "+1 plasma ray range", 3, WeaponType.Beam),
            new PowerupDefinition(PowerupType.Overcharge, "Augment: Overcharge", "+1 plasma cannon damage and +0.3 blast radius", 3, WeaponType.Beam),
            new PowerupDefinition(PowerupType.Payload, "Payload", "+2 Micro-Missiles", 3, WeaponType.Beam, AbilityType.MicroMissiles),
            new PowerupDefinition(PowerupType.Afterburner, "Afterburner", "+0.6 Rocket Boost distance", 3, WeaponType.Beam, AbilityType.RocketBoost),
            new PowerupDefinition(PowerupType.ExtendedBattery, "Extended Battery", "+2 seconds of Sentry Turret", 3, WeaponType.Beam, AbilityType.SentryTurret),
            new PowerupDefinition(PowerupType.DeadlyPrecision, "Deadly Precision", "Physical critical hits deal +25% of base hit damage per rank", 3),
            new PowerupDefinition(PowerupType.SlowBurn, "Slow Burn", "Elemental burns last +1 tick per rank", 3),
            new PowerupDefinition(PowerupType.Permafrost, "Permafrost", "Elemental freezes last +0.3 seconds per rank", 3),
            new PowerupDefinition(PowerupType.StaticField, "Static Field", "Lightning shock splash reaches +0.4 farther per rank", 3),
            new PowerupDefinition(PowerupType.RelicTraining, "Relic Training", "Equipped relic cooldowns are 8% shorter per rank", 3),
            new PowerupDefinition(PowerupType.SoulShield, "Soul Shield", "Every 8 kills grants a ward; stores up to 3 wards", 1),
            new PowerupDefinition(PowerupType.GuardDrills, "Knight: Guard Drills", "Shield cooldown is 10% shorter per rank", 3, WeaponType.Sword),
            new PowerupDefinition(PowerupType.Longsword, "Knight: Longsword", "Basic slash reach +0.25 per rank", 3, WeaponType.Sword),
            new PowerupDefinition(PowerupType.VolleyDrills, "Archer: Volley Drills", "Triple Shot cooldown is 10% shorter per rank", 3, WeaponType.Bow),
            new PowerupDefinition(PowerupType.Farshot, "Archer: Farshot", "Basic arrows and Triple Shot travel +1 farther per rank", 3, WeaponType.Bow),
            new PowerupDefinition(PowerupType.StormRhythm, "Wizard: Storm Rhythm", "Zap cooldown is 10% shorter per rank", 3, WeaponType.Staff),
            new PowerupDefinition(PowerupType.Stormcraft, "Wizard: Stormcraft", "Elemental effect chance +5% per rank", 3, WeaponType.Staff),
            new PowerupDefinition(PowerupType.ShadowDance, "Assassin: Shadow Dance", "Shadowstep cooldown is 10% shorter per rank", 3, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.LongDaggers, "Assassin: Long Daggers", "Basic stab reach +0.25 per rank", 3, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.DivineCadence, "Paladin: Divine Cadence", "Holy Swords cooldown is 10% shorter per rank", 3, WeaponType.Hammer),
            new PowerupDefinition(PowerupType.PatientFaith, "Paladin: Patient Faith", "Damage blessings last +1 second per rank", 3, WeaponType.Hammer),
            new PowerupDefinition(PowerupType.SecondRound, "Brawler: Second Round", "Empower cooldown is 10% shorter per rank", 3, WeaponType.Fists),
            new PowerupDefinition(PowerupType.HeavyGloves, "Brawler: Heavy Gloves", "Punch, barrage and Knuckle Sandwich hit areas are 10% larger per rank", 3, WeaponType.Fists),
            new PowerupDefinition(PowerupType.TailRhythm, "Demoness: Tail Rhythm", "Tail Sweep cooldown is 10% shorter per rank", 3, WeaponType.Tail),
            new PowerupDefinition(PowerupType.CruelTouch, "Demoness: Cruel Touch", "Your hits deal +1 damage to immobilized enemies (paralysed, frozen, stunned or rooted) per rank", 3, WeaponType.Tail),
            new PowerupDefinition(PowerupType.QuickDeal, "Gambler: Quick Deal", "Coin Volley cooldown is 10% shorter per rank", 3, WeaponType.Coins),
            new PowerupDefinition(PowerupType.LuckyStreak, "Gambler: Lucky Streak", "Gamble win chance +3% per rank", 3, WeaponType.Coins),
            new PowerupDefinition(PowerupType.HeatSink, "Augment: Heat Sink", "Plasma Cannon cooldown is 10% shorter per rank", 3, WeaponType.Beam),
            new PowerupDefinition(PowerupType.WideBeam, "Augment: Wide Beam", "Plasma ray width +0.08 per rank", 3, WeaponType.Beam),
            new PowerupDefinition(PowerupType.Immovable, "Bulk: Immovable", "Guarding slows you 25% less", 2, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.RootedStance, "Bulk: Rooted Stance", "Enemies that touch you no longer hurt you", 1, WeaponType.Mutation),
            // Universal talents built on conditions and trade-offs rather than flat stat bumps.
            new PowerupDefinition(PowerupType.GlassCannon, "Glass Cannon", "Deal 1.5x damage, but your maximum HP is halved (later max HP gains are halved too). Stacks up to 3 times", 3),
            new PowerupDefinition(PowerupType.LastStand, "Last Stand", "While at or below 25% HP (or at 1 HP), deal 1.5x damage and move 20% faster", 1),
            new PowerupDefinition(PowerupType.Berserker, "Berserker", "+1% damage for every 1% of your HP that is missing", 1),
            new PowerupDefinition(PowerupType.OpeningStrike, "Opening Strike", "Your first hit on an unhurt enemy is always a critical hit (or sets off its element)", 1),
            new PowerupDefinition(PowerupType.Executioner, "Executioner", "+50% damage to enemies below 25% health", 1),
            new PowerupDefinition(PowerupType.Rhythm, "Rhythm", "Every 4th basic attack or class skill deals double damage", 1),
            new PowerupDefinition(PowerupType.Spellblade, "Spellblade", "Using a Q or E ability makes your next 3 basic attacks deal +2", 1),
            new PowerupDefinition(PowerupType.Overkill, "Overkill", "Damage left over from a killing blow hits the nearest enemy; bigger overkills reach farther", 1),
            new PowerupDefinition(PowerupType.ElementalKills, "Elemental Kills", "Every kill makes your next elemental hit set off its element", 1),
            new PowerupDefinition(PowerupType.Thorns, "Thorns", "When you're hit, deal your damage to every enemy within 1 unit", 1),
            new PowerupDefinition(PowerupType.CloseCall, "Close Call", "Rolling through an enemy bolt gives +1 ward (once every 5 seconds)", 1),
            new PowerupDefinition(PowerupType.CheatDeath, "Cheat Death", "Once per world, a hit that would kill you leaves you at 1 HP", 1),
            new PowerupDefinition(PowerupType.Retaliation, "Knight: Retaliation", "After a parry, your next slash is fully charged instantly", 1, WeaponType.Sword),
            new PowerupDefinition(PowerupType.Counterweight, "Knight: Counterweight", "Fully charged slashes knock enemies back and stagger them for 0.5 seconds", 1, WeaponType.Sword),
            new PowerupDefinition(PowerupType.Cleave, "Knight: Cleave", "Fully charged slashes reach 0.6 farther and sweep 30 degrees wider", 1, WeaponType.Sword),
            new PowerupDefinition(PowerupType.Bastion, "Knight: Bastion", "A parry gives +1 ward (once every 15 seconds)", 1, WeaponType.Sword),
            new PowerupDefinition(PowerupType.GlacialRush, "Glacial Rush", "Shield Rush freezes everything it smashes through", 1, WeaponType.Sword, AbilityType.ShieldRush),
            new PowerupDefinition(PowerupType.AegisBurst, "Aegis Burst", "When Aegis ends, it blasts nearby enemies back and damages them once per hit it blocked", 1, WeaponType.Sword, AbilityType.Aegis),
            new PowerupDefinition(PowerupType.HuntersMark, "Archer: Hunter's Mark", "A fully charged arrow marks its target: it takes +1 damage from everything for 4 seconds", 1, WeaponType.Bow),
            new PowerupDefinition(PowerupType.Sniper, "Archer: Sniper", "Arrows deal +1 damage for every 4 units they fly", 1, WeaponType.Bow),
            new PowerupDefinition(PowerupType.PointBlank, "Archer: Point Blank", "Fully charged arrows fired within 3 units knock the target back and stagger it", 1, WeaponType.Bow),
            new PowerupDefinition(PowerupType.QuickNock, "Archer: Quick Nock", "Every 5th arrow fires fully charged instantly", 1, WeaponType.Bow),
            new PowerupDefinition(PowerupType.StormVolley, "Storm Volley", "Each Arrow Volley arrow that strikes the same enemy again deals +1 more than the last", 1, WeaponType.Bow, AbilityType.Volley),
            new PowerupDefinition(PowerupType.SplittingShot, "Splitting Shot", "After the first enemy, Piercing Shot splits into 3 arrows", 1, WeaponType.Bow, AbilityType.PiercingShot),
            new PowerupDefinition(PowerupType.ElementalClash, "Wizard: Elemental Clash", "Hitting a burning or frozen enemy with another element spreads its first element to enemies within 2 units", 1, WeaponType.Staff),
            new PowerupDefinition(PowerupType.ArcaneEcho, "Wizard: Arcane Echo", "Every 5th fireball fires a second copy", 1, WeaponType.Staff),
            new PowerupDefinition(PowerupType.Shatter, "Wizard: Shatter", "A fully charged fireball on a frozen enemy shatters the ice for double damage", 1, WeaponType.Staff),
            new PowerupDefinition(PowerupType.Pyromancer, "Wizard: Pyromancer", "Burning enemies take +1 from your lightning", 1, WeaponType.Staff),
            new PowerupDefinition(PowerupType.StormChargedOrb, "Storm-Charged Orb", "Hold Inferno Orb's key to charge it; at full charge its blast burns and shocks everything it hits", 1, WeaponType.Staff, AbilityType.Fireball),
            new PowerupDefinition(PowerupType.Frostblink, "Frostblink", "Arcane Blink leaves a Frost Nova where you started", 1, WeaponType.Staff, AbilityType.Blink),
            new PowerupDefinition(PowerupType.Bleed, "Assassin: Bleed", "Backstabs make the target bleed: 10 ticks over 5 seconds, each for 10% of the backstab", 1, WeaponType.Daggers),
            // Vanish is retired: its slot stays so later talents keep their numbers.
            new PowerupDefinition(PowerupType.Vanish, "Assassin: Vanish", "Retired", 1, WeaponType.Daggers, retired: true),
            new PowerupDefinition(PowerupType.Ambush, "Assassin: Ambush", "Your first hit after being hidden deals double damage", 1, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.Poisoner, "Assassin: Poisoner", "Enemies suffering damage over time (burning, bleeding, poisoned) take +1 from your stabs", 1, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.BloodTrail, "Assassin: Blood Trail", "Every 10 critical hits heal 1 HP", 1, WeaponType.Daggers),
            new PowerupDefinition(PowerupType.VenomKnives, "Venom Knives", "Fan of Knives poisons everything it cuts", 1, WeaponType.Daggers, AbilityType.FanOfKnives),
            new PowerupDefinition(PowerupType.Zeal, "Paladin: Zeal", "Each hit adds a stack of zeal; at 10 stacks your next blessing spends them all and gives everyone blessed double the bonus", 1, WeaponType.Hammer),
            new PowerupDefinition(PowerupType.HolyGround, "Paladin: Holy Ground", "Holy Sword strikes leave glowing ground that slows enemies", 1, WeaponType.Hammer),
            new PowerupDefinition(PowerupType.Retribution, "Paladin: Retribution", "After you're hit, your next swipe smites in a wide arc for +3", 1, WeaponType.Hammer),
            new PowerupDefinition(PowerupType.Shepherd, "Paladin: Shepherd", "Your blessings also give +10% movement speed", 1, WeaponType.Hammer),
            new PowerupDefinition(PowerupType.BlessedJudgment, "Blessed Judgment", "Judgment also blesses everyone standing near the mark", 1, WeaponType.Hammer, AbilityType.Judgment),
            new PowerupDefinition(PowerupType.HealingSanctuary, "Healing Sanctuary", "Dropping Sanctuary heals you and allies inside for 1 HP", 1, WeaponType.Hammer, AbilityType.Sanctuary),
            new PowerupDefinition(PowerupType.ComboCounter, "Brawler: Combo Counter", "Every punch that lands in a barrage adds +1 damage to its final punch", 1, WeaponType.Fists),
            new PowerupDefinition(PowerupType.Footwork, "Brawler: Footwork", "Your dodge roll keeps you untouchable a little longer", 1, WeaponType.Fists),
            new PowerupDefinition(PowerupType.Brawl, "Brawler: Brawl", "+1 damage for each enemy within 2 units (a guardian counts as 3)", 1, WeaponType.Fists),
            new PowerupDefinition(PowerupType.Knockout, "Brawler: Knockout", "The last punch of a charged barrage stuns for 1 second", 1, WeaponType.Fists),
            new PowerupDefinition(PowerupType.MeteorLeap, "Meteor Leap", "Wild Leap's slam sets enemies burning", 1, WeaponType.Fists, AbilityType.WildLeap),
            new PowerupDefinition(PowerupType.SandwichSpecial, "Sandwich Special", "Knuckle Sandwich sends a shockwave rolling forward past its box", 1, WeaponType.Fists, AbilityType.KnuckleSandwich),
            new PowerupDefinition(PowerupType.LingeringTerror, "Demoness: Lingering Terror", "When your paralysis wears off, the enemy stays 40% slower for 2 seconds", 1, WeaponType.Tail),
            new PowerupDefinition(PowerupType.Torment, "Demoness: Torment", "Each hit in a row on a paralysed enemy deals +1 more than the last, until its paralysis ends", 1, WeaponType.Tail),
            new PowerupDefinition(PowerupType.DreadAura, "Demoness: Dread Aura", "Enemies within 3 units act and attack 25% slower", 1, WeaponType.Tail),
            new PowerupDefinition(PowerupType.BloodPact, "Demoness: Blood Pact", "-1 max HP, but all your paralysis lasts 1 second longer", 1, WeaponType.Tail),
            new PowerupDefinition(PowerupType.CursedPaw, "Cursed Paw", "HEEEELP's slam leaves a Demon Curse where it lands", 1, WeaponType.Tail, AbilityType.DemonPaw),
            new PowerupDefinition(PowerupType.InfernalTechnique, "Infernal Technique", "During Archdemon's Technique, tail whips set enemies burning", 1, WeaponType.Tail, AbilityType.ArchdemonTechnique),
            new PowerupDefinition(PowerupType.Ricochet, "Gambler: Ricochet", "Thrown coins bounce on to a second enemy", 1, WeaponType.Coins),
            new PowerupDefinition(PowerupType.GoldCoin, "Gambler: Gold Coin", "Every 10th coin thrown deals its damage times the coins you carry (up to 50x)", 1, WeaponType.Coins),
            new PowerupDefinition(PowerupType.CompoundInterest, "Gambler: Compound Interest", "Every 30 seconds, gain 10% of the coins you carry (at least 1)", 1, WeaponType.Coins),
            new PowerupDefinition(PowerupType.HeadsOrTails, "Gambler: Heads or Tails", "Each coin hit has an even chance of dealing double or half damage", 1, WeaponType.Coins),
            new PowerupDefinition(PowerupType.TipJar, "Gambler: Tip Jar", "Every 25 gold coins you pick up heal 1 HP", 1, WeaponType.Coins),
            new PowerupDefinition(PowerupType.SnakeEyes, "Snake Eyes", "Losing All In makes you greedier: double damage for 5 seconds", 1, WeaponType.Coins, AbilityType.AllIn),
            new PowerupDefinition(PowerupType.ThermalVent, "Augment: Thermal Vent", "Firing the plasma cannon gives +20% movement speed for 2 seconds", 1, WeaponType.Beam),
            new PowerupDefinition(PowerupType.Overheat, "Augment: Overheat", "An enemy hit by your ray twice within 2 seconds starts burning", 1, WeaponType.Beam),
            new PowerupDefinition(PowerupType.TargetingArray, "Augment: Targeting Array", "Your ray deals +1 to enemies more than 6 units away", 1, WeaponType.Beam),
            new PowerupDefinition(PowerupType.ReactivePlating, "Augment: Reactive Plating", "When you're hit, a plasma burst goes off around you", 1, WeaponType.Beam),
            new PowerupDefinition(PowerupType.Coolant, "Augment: Coolant", "Rolling vents 30% of the plasma cannon's remaining cooldown", 1, WeaponType.Beam),
            new PowerupDefinition(PowerupType.MissileTurret, "Missile Turret", "Sentry Turret also fires a micro-missile every 2 seconds", 1, WeaponType.Beam, AbilityType.SentryTurret),
            // Universal: any hero can hold enemies in place, and these reward doing it.
            new PowerupDefinition(PowerupType.SittingDuck, "Sitting Duck", "+30% damage to immobilized enemies (paralysed, frozen, stunned or rooted)", 1),
            new PowerupDefinition(PowerupType.IronGrip, "Iron Grip", "Your paralysis, freezes, stuns and roots last 25% longer", 1),
            new PowerupDefinition(PowerupType.SearingHold, "Searing Hold", "Immobilizing an enemy sets it burning", 1),
            new PowerupDefinition(PowerupType.StaticHold, "Static Hold", "Immobilizing an enemy shocks every enemy near it", 1),
            new PowerupDefinition(PowerupType.NumbingHold, "Numbing Hold", "Immobilizing an enemy chills it, so it stays slowed for 2 seconds after it breaks free", 1),
            new PowerupDefinition(PowerupType.Domino, "Domino", "When an immobilized enemy dies, enemies within 1.5 units are stunned for 0.75 seconds", 1),
            new PowerupDefinition(PowerupType.Frenzy, "Frenzy", "+25% attack and charge speed", 1),
            new PowerupDefinition(PowerupType.GraveTithe, "Reaper: Grave Tithe", "Killing an immobilized enemy (afraid, paralysed, frozen, stunned or rooted) gives you a soul", 1, WeaponType.Scythe),
            new PowerupDefinition(PowerupType.ElementalImmobilization, "Elemental Immobilization", "When an enemy is affected by an element (burning, chilled, frozen or shocked), your next hit against it stuns it for 1 second (once every 3 seconds)", 1),
            new PowerupDefinition(PowerupType.SoulFury, "Reaper: Soul Fury", "Deal 1% / 2% / 3% more damage by rank for every soul you hold", 3, WeaponType.Scythe),
            new PowerupDefinition(PowerupType.DeepWounds, "Samurai: Deep Wounds", "Bleeding you inflict gains 1 / 3 / 5 extra ticks by rank over the same 5 seconds", 3, WeaponType.Katana),
            new PowerupDefinition(PowerupType.BloodInTheWater, "Samurai: Blood in the Water", "Bleeding enemies take 10% / 15% / 25% more damage by rank from your attacks", 3, WeaponType.Katana),
            new PowerupDefinition(PowerupType.JaggedBlade, "Samurai: Jagged Blade", "Your attacks have a 10% chance to inflict bleeding", 1, WeaponType.Katana),
            new PowerupDefinition(PowerupType.FineDicing, "Fine Dicing", "Slice Dice Chunk deals 10% / 30% / 50% more damage by rank", 3, WeaponType.Katana, AbilityType.SliceDiceChunk),
            new PowerupDefinition(PowerupType.BledDry, "Samurai: Bled Dry", "When bleeding you inflicted ends, the enemy is stunned for 0.5 / 1 / 1.5 seconds by rank", 3, WeaponType.Katana),
            new PowerupDefinition(PowerupType.RazorTip, "Edge: Razor Tip", "+10% critical hit chance", 3, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.LongChain, "Edge: Long Chain", "+0.5 lash reach", 2, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.WeightedTip, "Edge: Weighted Tip", "The sweet spot at the tip of your lash is 50% longer, and tip crits deal +1 damage", 2, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.ReelIn, "Edge: Reel In", "Hook cooldown is 1 second shorter, and hooked enemies take a hit when they land", 2, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.Featherweight, "Edge: Featherweight", "+0.4 movement speed and 10% shorter dodge cooldown", 3, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.BleedingEdge, "Edge: Bleeding Edge", "Your critical hits make the target bleed: 10 ticks over 5 seconds, each for 10% of the hit", 1, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.LightOnHisFeet, "Edge: Light on His Feet", "After a dodge roll, your next lash is always a critical hit", 1, WeaponType.Mutation),
            new PowerupDefinition(PowerupType.FreightTrain, "Freight Train", "Bulldoze charges 50% farther, and the enemies it slams are stunned 1 second longer", 1, WeaponType.Mutation, AbilityType.Bulldoze),
            new PowerupDefinition(PowerupType.RubbleWall, "Rubble Wall", "Boulder Toss's rock wall lasts twice as long and is twice as wide", 1, WeaponType.Mutation, AbilityType.BoulderToss),
            new PowerupDefinition(PowerupType.Hardened, "Hardened", "Every hit Iron Skin absorbs is also stored as Force", 1, WeaponType.Mutation, AbilityType.IronSkin),
            new PowerupDefinition(PowerupType.Zipline, "Zipline", "Swing Line is ready again at once when its kick kills", 1, WeaponType.Mutation, AbilityType.SwingLine),
            new PowerupDefinition(PowerupType.LowBlow, "Low Blow", "Enemies tripped by Ankle Wrap take a critical hit from every blow until they get up", 1, WeaponType.Mutation, AbilityType.AnkleWrap),
            new PowerupDefinition(PowerupType.Shackles, "Shackles", "When a bound enemy dies, your chain leaps to the nearest enemy and binds it for the time it had left", 1, WeaponType.Mutation, AbilityType.Bind),
            new PowerupDefinition(PowerupType.Lodestone, "Lodestone", "Crystals (and your hero's own pickups: souls, coins, scrap and runes) are drawn in from 25% / 50% farther, faster, and picked up from farther away", 2),
            new PowerupDefinition(PowerupType.SoulBurst, "Reaper: Soul Burst", "Killing an enemy that would leave a soul when it dies also makes it explode, hurting everything around it", 1, WeaponType.Scythe),
            new PowerupDefinition(PowerupType.SoulHoard, "Reaper: Soul Hoard", "You can hold 25 / 50 / 100 more souls by rank", 3, WeaponType.Scythe),
            new PowerupDefinition(PowerupType.GrimHarvest, "Grim Harvest", "Reaper's Technique's swings harvest a soul from every enemy they hit", 1, WeaponType.Scythe, AbilityType.ReapersTechnique),
            new PowerupDefinition(PowerupType.PinnedWounds, "Samurai: Pinned Wounds", "Bleeding you inflict deals 1.5x damage while its victim is immobilized (paralysed, frozen, stunned or rooted)", 1, WeaponType.Katana),
            new PowerupDefinition(PowerupType.HotStreak, "Hot Streak", "After a kill, your next attack charges 50% faster (doesn't stack)", 1),
            new PowerupDefinition(PowerupType.ColdBlooded, "Cold Blooded", "Making an enemy bleed freezes it", 1),
            new PowerupDefinition(PowerupType.FlashFreeze, "Flash Freeze", "Enemies a shock arcs into (not the one it started from) are frozen", 1),
            new PowerupDefinition(PowerupType.Scorchblood, "Scorchblood", "Enemies both burning and bleeding are scorchblooded: both tick 50% harder and the fire won't go out while they bleed. Pyre Burst, Crimson Bloom and Elemental Clash from a scorchblooded enemy spread scorchblood", 1),
            new PowerupDefinition(PowerupType.BloodDebt, "Samurai: Blood Debt", "Every 10 bleeding enemies you kill heal you for 1 HP", 1, WeaponType.Katana)
        };

        private static readonly Dictionary<PowerupType, PowerupDefinition> byType = BuildLookup();

        private static Dictionary<PowerupType, PowerupDefinition> BuildLookup()
        {
            var lookup = new Dictionary<PowerupType, PowerupDefinition>();
            foreach (var definition in All) lookup[definition.Type] = definition;
            return lookup;
        }

        /// <summary>Looks a talent up by type, so its class and ability gates never depend on catalog order.</summary>
        public static PowerupDefinition Get(PowerupType type) => byType[type];
    }
}
