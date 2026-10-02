using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    public enum AbilityType
    {
        None, ShieldRush, Earthshatter, Aegis, Volley, PiercingShot, Windstep,
        Fireball, FrostNova, Blink, FanOfKnives, VenomVial, ShadowVeil, HealingLight, Judgment, Sanctuary,
        Heartbeat, FightOrFlight, Bulldoze, KnuckleSandwich, WildLeap, PrimalRage, ArchdemonTechnique, DemonPaw, DemonCurse,
        Windfall, AllIn, Jackpot, MicroMissiles, RocketBoost, SentryTurret,
        // The talents & abilities expansion.
        ShieldThrow, Whirlwind, WarBanner, NetShot, RicochetArrow, BearTrap, IceWall, BallLightning, LightningStorm, SmokeBomb, DeathMark, ShadowClone, HolyLance, Consecration, DivineIntervention, ThunderClap, HaymakerDash, Suplex, WingDash, SoulSiphon, NightmareSnap, CardToss, DiceBomb, Insurance, EmpPulse, GrappleArm, OrbitalLaser,
        // The Reaper.
        ShadeWalk, FearIncarnate, Feast, Sow, Reap, ReapersTechnique,
        // The Samurai.
        SliceDiceChunk, Bloodscent, BloodShallFlow, MaestrosTechnique, SwiftAsTheWind, Bloodpop,
        // The Specimen (Heartbeat, Fight or Flight and Bulldoze took over the retired Admin's three slots above).
        BoulderToss, IronSkin, GiantSwing, SwingLine, AnkleWrap, Bind, RoundUp
    }

    public sealed class AbilityDefinition
    {
        public AbilityType Type { get; }
        public WeaponType ClassWeapon { get; }
        public string Name { get; }
        public string Description { get; }
        public string Glyph { get; }
        public float Cooldown { get; }
        public Color Color { get; }
        /// <summary>True when the ability must be bought in the Ash shop before guardians can offer it.</summary>
        public bool ShopUnlock { get; }
        /// <summary>The Ash shop upgrade that unlocks a <see cref="ShopUnlock"/> ability.</summary>
        public string UnlockId => "ability_" + Type.ToString().ToLowerInvariant();
        public AbilityDefinition(AbilityType type, WeaponType weapon, string name, string description, string glyph, float cooldown, Color color,
            bool shopUnlock = false)
        {
            Type = type; ClassWeapon = weapon; Name = name; Description = description;
            Glyph = glyph; Cooldown = cooldown; Color = color; ShopUnlock = shopUnlock;
        }
    }

    public static class AbilityCatalog
    {
        public static readonly Color Gold = new Color(1f, 0.77f, 0.36f);
        public static readonly Color Ice = new Color(0.4f, 0.85f, 1f);
        public static readonly Color Violet = new Color(0.74f, 0.53f, 1f);
        public static readonly Color Green = new Color(0.57f, 0.93f, 0.55f);
        public static readonly AbilityDefinition[] All =
        {
            new AbilityDefinition(AbilityType.ShieldRush, WeaponType.Sword, "Shield Rush", "Brace behind your shield and charge forward, smashing through enemies in your path. Unstoppable and invulnerable while charging.", ">>", 8f, Ice),
            new AbilityDefinition(AbilityType.Earthshatter, WeaponType.Sword, "Earthshatter", "Stomp the ground: five quake rings crack the earth and ripple outward, damaging and slowing enemies.", "X", 10f, Gold),
            new AbilityDefinition(AbilityType.Aegis, WeaponType.Sword, "Aegis", "A shimmering bubble makes you invulnerable for 2 seconds. Ranks extend protection.", "O", 14f, Ice),
            new AbilityDefinition(AbilityType.Volley, WeaponType.Bow, "Arrow Volley", "Call down a rain of 16 arrows from the sky onto the area around your cursor.", "///", 8f, Green),
            new AbilityDefinition(AbilityType.PiercingShot, WeaponType.Bow, "Piercing Shot", "A thundering arrow tears through every enemy in a long line, carrying the element in your Elemental Quiver. Range: 18 units.", "->", 9f, Gold),
            new AbilityDefinition(AbilityType.Windstep, WeaponType.Bow, "Windstep", "Dash in your aim direction and fire a tight, long-range three-arrow counterattack.", ">>", 10f, Green),
            new AbilityDefinition(AbilityType.Fireball, WeaponType.Staff, "Inferno Orb", "Unlock an explosive fireball that can ignite the enemies it hits.", "*", 8f, new Color(1f, 0.43f, 0.23f)),
            new AbilityDefinition(AbilityType.FrostNova, WeaponType.Staff, "Frost Nova", "A wide ring of ice bursts out around you, heavily damaging the enemies it reaches and freezing them solid for 3 seconds.", "+", 8f, Ice),
            new AbilityDefinition(AbilityType.Blink, WeaponType.Staff, "Arcane Blink", "Blink up to 6 units toward your aim, straight through walls.", "<>", 9f, Violet),
            new AbilityDefinition(AbilityType.FanOfKnives, WeaponType.Daggers, "Fan of Knives", "Throw a ring of piercing knives that fly back to you after 1 second, wherever you are, cutting through everything in their path. Each can critically strike.", "X", 8f, Violet),
            new AbilityDefinition(AbilityType.VenomVial, WeaponType.Daggers, "Venom Vial", "Throw a vial at the cursor (up to 5 units, stopped by walls). It shatters into a toxic pool that poisons enemies standing in it.", "+", 9f, Green),
            new AbilityDefinition(AbilityType.ShadowVeil, WeaponType.Daggers, "Shadow Veil", "Vanish into shadow: enemies lose track of you and stop turning toward you. Briefly invulnerable as you fade.", "~", 12f, Violet),
            new AbilityDefinition(AbilityType.HealingLight, WeaponType.Hammer, "Healing Light", "Restore 2 HP to yourself and nearby allies.", "+", 30f, HealVfx.Mint),
            new AbilityDefinition(AbilityType.Judgment, WeaponType.Hammer, "Judgment", "Call down a column of holy light at the cursor. After a short windup it smites and slows enemies around the mark.", "!", 10f, Gold),
            new AbilityDefinition(AbilityType.Sanctuary, WeaponType.Hammer, "Sanctuary", "Surround yourself with a bubble of holy light that moves with you, destroys every projectile inside it, shoves enemies out and slows any that return. Recast to drop it. You cannot attack while it holds.", "O", 16f, Ice),
            new AbilityDefinition(AbilityType.Heartbeat, WeaponType.Mutation, "Heartbeat", "A thump of force from your chest knocks back every enemy within 2.5 units. As the Behemoth it reaches farther and stuns for 1 second; as the Edge every enemy it hits takes critical hits from you for 2 seconds. Ranks add damage and lengthen the stun or the exposure.", "<3", 10f, SpecimenCatalog.Vital),
            new AbilityDefinition(AbilityType.FightOrFlight, WeaponType.Mutation, "Fight or Flight", "For 4 seconds you move 40% faster and your dodge is ready at once. The Behemoth also stores 2 Force; the Edge also attacks 50% faster. Ranks add a second.", ">>", 15f, SpecimenCatalog.Vital),
            new AbilityDefinition(AbilityType.Bulldoze, WeaponType.Mutation, "Bulldoze", "Behemoth only. Charge 5 units with your arms crossed, carrying every enemy in your path and slamming them at the end. Bolts that hit you on the way become Force. Ranks add damage.", "=>", 9f, SpecimenCatalog.Amber),
            new AbilityDefinition(AbilityType.KnuckleSandwich, WeaponType.Fists, "Knuckle Sandwich", "Wind up and throw a HEAVY punch that smashes everything in a big rectangle ahead. Ranks increase its size and damage.", "[]", 7f, BrawlerAttack.Glove),
            new AbilityDefinition(AbilityType.WildLeap, WeaponType.Fists, "Wild Leap", "Pounce onto an enemy, even over walls, and slam down for massive damage. The farther you leap, the wider the slam. Invulnerable while airborne.", "^", 16f, Gold),
            new AbilityDefinition(AbilityType.PrimalRage, WeaponType.Fists, "Primal Rage", "10 seconds of huge damage, charge, movement and dodge buffs, then 5 seconds tired. Cooldown starts once rested.", "!!", 25f, new Color(1f, 0.25f, 0.2f)),
            new AbilityDefinition(AbilityType.ArchdemonTechnique, WeaponType.Tail, "Archdemon's Technique", "Channel your father, the Demon Lord, for 8 seconds: every click is a fully charged vital stab, and charging winds up a devastating paralysing tail whip 50% faster. Tail Sweep strikes twice, each sweep paralysing enemies for 1.5 seconds and cursing them to take 25% more damage for 4 seconds. Cooldown starts once it ends.", "W", 16f, HeroBuffs.AscendColor),
            new AbilityDefinition(AbilityType.DemonPaw, WeaponType.Tail, "HEEEELP", "Open a portal over a nearby enemy, even beyond walls: your giant demonic pet slams a huge clawed paw down on it, stunning everything underneath.", "!", 12f, DemonessAttack.Violet),
            new AbilityDefinition(AbilityType.DemonCurse, WeaponType.Tail, "Demon Curse", "Brand a pentagram at the cursor, even past walls: enemies on it are paralysed for 3 seconds and take 50% more damage for 6.", "*", 14f, DemonessAttack.Pale),
            new AbilityDefinition(AbilityType.Windfall, WeaponType.Coins, "Windfall", "Your purse coughs up 5 coins at once. Ranks add one more coin.", "$", 12f, GamblerAttack.Gold),
            new AbilityDefinition(AbilityType.AllIn, WeaponType.Coins, "All In", "Double or nothing on every coin you carry: a 50% chance to double them, or lose them all. Ranks tilt the odds 5% your way.", "x2", 10f, new Color(0.35f, 0.9f, 0.5f)),
            new AbilityDefinition(AbilityType.Jackpot, WeaponType.Coins, "Jackpot", "Feed every coin you carry into the machine and always win a random speed buff, damage buff or heal that grows with every coin spent. Ranks lengthen the buffs.", "777", 18f, new Color(1f, 0.4f, 0.55f)),
            new AbilityDefinition(AbilityType.MicroMissiles, WeaponType.Beam, "Micro-Missiles", "Your shoulder pod fires a fan of 6 homing missiles that seek out the nearest enemies and burst on impact. Ranks add a missile and damage.", "^^", 9f, CyborgAttack.MissileColor),
            new AbilityDefinition(AbilityType.RocketBoost, WeaponType.Beam, "Rocket Boost", "Blast forward on your leg thrusters, ramming through every enemy in your path, and land in a burst of flame that can set them burning. Briefly invulnerable.", ">>", 8f, CyborgAttack.MissileColor),
            new AbilityDefinition(AbilityType.SentryTurret, WeaponType.Beam, "Sentry Turret", "Deploy a turret at the cursor (up to 4 units away). For 6 seconds it fires a piercing plasma ray at the nearest enemy. Ranks add a second and damage.", "T", 16f, CyborgAttack.Plasma),
            new AbilityDefinition(AbilityType.ShieldThrow, WeaponType.Sword, "Shield Throw", "Hurl your shield: it bounces between up to 3 enemies and returns. You can't parry until it's back. Ranks add damage.", "()", 7f, Ice),
            new AbilityDefinition(AbilityType.Whirlwind, WeaponType.Sword, "Whirlwind", "Spin with your sword for 2 seconds, hitting everything around you. You can still walk, but can't attack or parry while spinning. Ranks add damage and spin time.", "@", 10f, new Color(0.55f, 1f, 0.9f)),
            new AbilityDefinition(AbilityType.NetShot, WeaponType.Bow, "Net Shot", "Fire a weighted net that drops on the first enemy it reaches, rooting every enemy under it for 2 seconds. Ranks hold them longer.", "#", 9f, new Color(0.8f, 0.75f, 0.55f)),
            new AbilityDefinition(AbilityType.RicochetArrow, WeaponType.Bow, "Ricochet Arrow", "An arrow that ricochets from each enemy it hits to the nearest one it hasn't, up to 3 times, dealing more damage with each ricochet. It also glances off walls. Ranks add damage.", "/\\", 8f, new Color(1f, 0.85f, 0.45f)),
            new AbilityDefinition(AbilityType.BearTrap, WeaponType.Bow, "Bear Trap", "Set a trap at the cursor (up to 2 at once). The first enemy to step in is held for 2 seconds and takes damage. Ranks hold longer and hurt more.", "W", 6f, new Color(0.62f, 0.62f, 0.68f), true),
            new AbilityDefinition(AbilityType.IceWall, WeaponType.Staff, "Ice Wall", "Raise a wall of ice blocks across your aim for 7 seconds that stops enemies, heroes and bolts. Each block breaks on its own under enemy or hero blows, and its shards freeze enemies right beside it. Ranks make it last longer and tougher.", "||", 11f, new Color(0.7f, 0.92f, 1f)),
            new AbilityDefinition(AbilityType.BallLightning, WeaponType.Staff, "Ball Lightning", "A slow orb of lightning drifts forward for 4 seconds, zapping every enemy it passes, with your usual chance to shock. Ranks add damage and time.", "o", 9f, CombatDamage.ShockColor),
            new AbilityDefinition(AbilityType.LightningStorm, WeaponType.Staff, "Lightning Storm", "For 3 seconds, lightning strikes down from above onto enemies around you, each with your usual chance to shock. Ranks lengthen the storm.", "!", 16f, new Color(0.6f, 0.8f, 1f), true),
            new AbilityDefinition(AbilityType.SmokeBomb, WeaponType.Daggers, "Smoke Bomb", "A smoke cloud for 4 seconds: inside it enemies lose track of you, and your hits on enemies inside always count as backstabs. Ranks make it last longer.", "~", 12f, new Color(0.62f, 0.62f, 0.7f)),
            new AbilityDefinition(AbilityType.DeathMark, WeaponType.Daggers, "Death Mark", "Mark the enemy nearest your cursor for 6 seconds. If it dies while marked, all the damage it took during the mark bursts out onto every enemy around it; if it survives, it takes all that damage again when the mark ends.", "+", 14f, new Color(0.85f, 0.2f, 0.3f), true),
            new AbilityDefinition(AbilityType.ShadowClone, WeaponType.Daggers, "Shadow Clone", "For 7.5 seconds, every backstab you land summons a shadow clone behind the victim that backstabs it again, sharpening your dagger another stack. Ranks lengthen it.", "&", 16f, ShadowstepVfx.Violet, true),
            new AbilityDefinition(AbilityType.HolyLance, WeaponType.Hammer, "Holy Lance", "Hurl a lance of light that pierces a wide line with holy damage and stuns the first enemy for 1 second. Ranks add damage.", "|", 8f, Gold),
            new AbilityDefinition(AbilityType.Consecration, WeaponType.Hammer, "Consecration", "Sanctify the ground around you for 5 seconds: enemies inside take holy damage every second, and allies inside are blessed. Ranks make it last longer.", "#", 14f, Gold),
            new AbilityDefinition(AbilityType.DivineIntervention, WeaponType.Hammer, "Divine Intervention", "Mark the ally nearest your cursor (hover yourself to mark yourself). If they fall in the next 5 seconds a pillar of light saves them at 75% HP, untouchable and +2 damage for 2 seconds.", "+", 20f, new Color(1f, 0.95f, 0.7f), true),
            new AbilityDefinition(AbilityType.ThunderClap, WeaponType.Fists, "Thunder Clap", "Clap to send a shockwave rolling far out from you in a narrow cone, knocking back every enemy it reaches and stunning them for 0.75 seconds. Ranks add damage.", "))", 9f, new Color(0.8f, 0.9f, 1f)),
            new AbilityDefinition(AbilityType.HaymakerDash, WeaponType.Fists, "Haymaker Dash", "Dash forward and uppercut the first enemy, launching it through the air into the enemies behind it. Ranks add damage.", ">!", 8f, BrawlerAttack.Glove),
            new AbilityDefinition(AbilityType.Suplex, WeaponType.Fists, "Suplex", "Grab the nearest enemy and slam it down a short way toward your cursor, hurting everything where it lands. Guardians are too heavy. Ranks add damage.", "U", 10f, new Color(0.85f, 0.7f, 0.5f), true),
            new AbilityDefinition(AbilityType.WingDash, WeaponType.Tail, "Wing Dash", "Beat your demon wings to dash, paralysing every enemy you pass through for 2 seconds. Ranks dash farther.", ">>", 8f, DemonessAttack.Violet),
            new AbilityDefinition(AbilityType.SoulSiphon, WeaponType.Tail, "Soul Siphon", "For 5 seconds, drain immobilized enemies nearby (paralysed, frozen, stunned or rooted): each takes demonic damage and heals you 1 HP every second. Ranks lengthen it.", "~", 14f, DemonessAttack.Violet),
            new AbilityDefinition(AbilityType.NightmareSnap, WeaponType.Tail, "Nightmare Snap", "Snap every hold (paralysis, freeze, stun or root) on every enemy nearby: each takes demonic damage that grows with how long it still had to be held. Ranks add damage.", "*", 10f, DemonessAttack.Pale, true),
            new AbilityDefinition(AbilityType.CardToss, WeaponType.Coins, "Card Toss", "Throw 3 cards in a spread. Each card's suit decides its trick: hearts burn, diamonds freeze, clubs shock and spades paralyse. Ranks add damage.", "<>", 7f, new Color(0.95f, 0.95f, 0.95f)),
            new AbilityDefinition(AbilityType.DiceBomb, WeaponType.Coins, "Dice Bomb", "Toss a pair of dice at the cursor. Each explodes for your damage times the face it rolls. Ranks add damage.", ":", 10f, new Color(0.97f, 0.95f, 0.9f)),
            new AbilityDefinition(AbilityType.Insurance, WeaponType.Coins, "Insurance", "For 6 seconds, every hit costs you 5 coins instead of HP (while you can pay). Ranks lengthen the policy.", "$!", 18f, new Color(0.35f, 0.9f, 0.5f), true),
            new AbilityDefinition(AbilityType.EmpPulse, WeaponType.Beam, "EMP Pulse", "Stun every enemy within 4 units for 1.5 seconds and destroy the enemy bolts around you. Ranks stun longer.", "((", 12f, WorldCatalog.Neon),
            new AbilityDefinition(AbilityType.GrappleArm, WeaponType.Beam, "Grapple Arm", "Fire a hook that snags the first enemy in line and hauls it to you. Guardians are too heavy to pull. Ranks add damage.", "J", 7f, new Color(0.7f, 0.75f, 0.85f)),
            new AbilityDefinition(AbilityType.OrbitalLaser, WeaponType.Beam, "Orbital Laser", "A laser from the sky follows your cursor for 3 seconds, burning everything it touches. Ranks lengthen it.", "|v|", 18f, CyborgAttack.Plasma, true),
            new AbilityDefinition(AbilityType.WarBanner, WeaponType.Sword, "War Banner", "Plant a banner for 6 seconds. You and allies inside its wide circle deal +1 damage, and everyone inside gains 2 wards when it's planted. Ranks extend it.", "F", 18f, new Color(0.9f, 0.2f, 0.25f), true),
            new AbilityDefinition(AbilityType.ShadeWalk, WeaponType.Scythe, "Shade Walk", "Your body turns to shade for 3 seconds: you walk straight through enemies and their touch cannot hurt you. Ranks add a second.", "~", 12f, ReaperAttack.Shade),
            new AbilityDefinition(AbilityType.FearIncarnate, WeaponType.Scythe, "Fear Incarnate", "Every enemy is struck with fear for 1.5 seconds, frozen with its back to you, and gives up a soul when it dies. Ranks lengthen the fear.", "!!", 18f, ReaperAttack.Soul),
            new AbilityDefinition(AbilityType.Feast, WeaponType.Scythe, "Feast!", "Eat 1, 3 or 5 souls to heal 1, 2 or 3 HP: always the most you can afford, but never more than the wound needs. Ranks leave you untouchable for a second each.", "+", 10f, HealVfx.Mint),
            new AbilityDefinition(AbilityType.Sow, WeaponType.Scythe, "Sow", "Sow fear into the enemy nearest your cursor for 2 seconds. Deals no damage. If it dies while afraid, the fear spreads to every enemy within 3 units. Ranks lengthen the fear.", "v", 8f, ReaperAttack.Soul),
            new AbilityDefinition(AbilityType.Reap, WeaponType.Scythe, "Reap", "Reap the fear out of every frightened enemy: each full second of fear it had left gives you a soul and deals damage. Only fear is reaped, no other hold. Ranks add damage.", "^", 9f, ReaperAttack.Bone),
            new AbilityDefinition(AbilityType.ReapersTechnique, WeaponType.Scythe, "Reaper's Technique", "For 10 seconds your quick cuts become a string of three scythe arts that hit harder: a wide cut, a doubled back-cut and a full spin. Charging to harvest is unchanged. Ranks add 2 seconds.", "S", 16f, ReaperAttack.Soul, true),
            new AbilityDefinition(AbilityType.SliceDiceChunk, WeaponType.Katana, "Slice Dice Chunk", "Slice: a quick slash. If it hits, press again to Dice: a bigger slash. If that hits too, press again to Chunk: a huge slash that inflicts bleeding. Ranks add damage.", "///", 9f, SamuraiAttack.Blood),
            new AbilityDefinition(AbilityType.Bloodscent, WeaponType.Katana, "Bloodscent", "Every bleeding enemy's wounds start over: their ticks and duration are reset. Rank 2 resets them to 1.25x, rank 3 to 1.5x.", "%", 12f, SamuraiAttack.Blood),
            new AbilityDefinition(AbilityType.BloodShallFlow, WeaponType.Katana, "Blood Shall Flow", "For 5 seconds every hit with your katana inflicts bleeding. Lasts 7.5 seconds at rank 2 and 10 at rank 3.", "''", 16f, SamuraiAttack.Blood),
            new AbilityDefinition(AbilityType.MaestrosTechnique, WeaponType.Katana, "Maestro's Technique", "For 10 seconds your katana swipes become a three-hit combo: sweep, sweep, thrust. The sweeps deal 1.5x damage; the thrust deals 2.25x damage and inflicts bleeding. Ranks add 2 seconds.", "S", 16f, SamuraiAttack.Steel),
            new AbilityDefinition(AbilityType.SwiftAsTheWind, WeaponType.Katana, "Swift as the Wind", "Attack 50% faster for 5 seconds. Lasts 10 seconds at rank 2 and 15 at rank 3.", ">>", 20f, new Color(0.8f, 0.95f, 0.9f), true),
            new AbilityDefinition(AbilityType.Bloodpop, WeaponType.Katana, "Bloodpop", "Every bleeding enemy's blood pops: all the bleed damage it had left is dealt at once. Rank 2 deals 1.15x of it, rank 3 1.25x.", "*", 14f, SamuraiAttack.Blood, true),
            new AbilityDefinition(AbilityType.BoulderToss, WeaponType.Mutation, "Boulder Toss", "Behemoth only. Rip a chunk out of the floor and hurl it. It shatters on the first enemy it hits, showering the enemies around it, and leaves a rock wall that blocks bolts for 4 seconds. Ranks add damage.", "@", 10f, SpecimenCatalog.Amber),
            new AbilityDefinition(AbilityType.IronSkin, WeaponType.Mutation, "Iron Skin", "Behemoth only. For 4 seconds the next 3 hits on you do no damage, and each one sends a shockwave through the enemies around you. Ranks add half a second.", "[]", 16f, SpecimenCatalog.Amber),
            new AbilityDefinition(AbilityType.GiantSwing, WeaponType.Mutation, "Giant Swing", "Behemoth only. Grab the nearest enemy, swing it around you like a club through everything nearby, then fling it. Guardians are too big to grab: they take a huge punch instead. Ranks add damage.", "O", 14f, SpecimenCatalog.Amber, true),
            new AbilityDefinition(AbilityType.SwingLine, WeaponType.Mutation, "Swing Line", "Edge only. Hook a wall or an enemy up to 6 units toward the cursor and zip to it, kicking whatever waits at the end. Ranks add damage.", "~>", 8f, SpecimenCatalog.Keen),
            new AbilityDefinition(AbilityType.AnkleWrap, WeaponType.Mutation, "Ankle Wrap", "Edge only. A low lash across a half circle wraps every enemy's legs: they trip and are stunned for 1 second. Ranks lengthen the stun.", "_", 9f, SpecimenCatalog.Keen),
            new AbilityDefinition(AbilityType.Bind, WeaponType.Mutation, "Bind", "Edge only. Wrap the enemy nearest the cursor in your chain: it is rooted for 2 seconds, and every hit you land on it while it is bound is a critical hit. Ranks lengthen the bind.", "&", 7f, SpecimenCatalog.Keen),
            new AbilityDefinition(AbilityType.RoundUp, WeaponType.Mutation, "Round-Up", "Edge only. Hook up to 4 enemies in a wide cone and smash them together in front of you: each takes collision damage and is stunned for 1 second. Guardians only take the hit. Ranks add damage.", "><", 14f, SpecimenCatalog.Keen, true)
        };

        /// <summary>What guardians can offer this hero: their class's abilities, less any not yet bought in the Ash shop.</summary>
        public static List<AbilityDefinition> PoolFor(WeaponType weapon, PermanentProgress progress)
        {
            var pool = new List<AbilityDefinition>();
            foreach (var ability in All)
                if (ability.ClassWeapon == weapon && (!ability.ShopUnlock || (progress != null && progress.Rank(ability.UnlockId) > 0))) pool.Add(ability);
            return pool;
        }

        public static AbilityDefinition Get(AbilityType type)
        {
            foreach (var ability in All) if (ability.Type == type) return ability;
            return null;
        }
    }
}
