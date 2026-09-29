using UnityEngine;

namespace Slopgame
{
    public enum AbilityType
    {
        None, ShieldRush, Earthshatter, Aegis, Volley, PiercingShot, Windstep,
        Fireball, FrostNova, Blink, FanOfKnives, VenomVial, ShadowVeil, HealingLight, Judgment, Sanctuary,
        Eclipse, SoulRend, ShadowReign, KnuckleSandwich, WildLeap, PrimalRage, ArchdemonTechnique, DemonPaw, DemonCurse,
        Windfall, AllIn, Jackpot
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
        public AbilityDefinition(AbilityType type, WeaponType weapon, string name, string description, string glyph, float cooldown, Color color)
        {
            Type = type; ClassWeapon = weapon; Name = name; Description = description;
            Glyph = glyph; Cooldown = cooldown; Color = color;
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
            new AbilityDefinition(AbilityType.PiercingShot, WeaponType.Bow, "Piercing Shot", "A thundering arrow tears through every enemy in a long line. Range: 18 units.", "->", 9f, Gold),
            new AbilityDefinition(AbilityType.Windstep, WeaponType.Bow, "Windstep", "Dash in your aim direction and fire a tight, long-range three-arrow counterattack.", ">>", 10f, Green),
            new AbilityDefinition(AbilityType.Fireball, WeaponType.Staff, "Inferno Orb", "Unlock an explosive fireball that always ignites the enemies it hits.", "*", 8f, new Color(1f, 0.43f, 0.23f)),
            new AbilityDefinition(AbilityType.FrostNova, WeaponType.Staff, "Frost Nova", "A ring of ice slowly expands around you, damaging the enemies it reaches and freezing them solid for 2 seconds.", "+", 10f, Ice),
            new AbilityDefinition(AbilityType.Blink, WeaponType.Staff, "Arcane Blink", "Blink up to 6 units toward your aim, straight through walls.", "<>", 9f, Violet),
            new AbilityDefinition(AbilityType.FanOfKnives, WeaponType.Daggers, "Fan of Knives", "Throw a ring of piercing knives that fly back to you after 1 second, wherever you are, cutting through everything in their path. Each can critically strike.", "X", 8f, Violet),
            new AbilityDefinition(AbilityType.VenomVial, WeaponType.Daggers, "Venom Vial", "Throw a vial at the cursor (up to 5 units, stopped by walls). It shatters into a toxic pool that poisons enemies standing in it.", "+", 9f, Green),
            new AbilityDefinition(AbilityType.ShadowVeil, WeaponType.Daggers, "Shadow Veil", "Vanish into shadow: enemies lose track of you and stop turning toward you. Briefly invulnerable as you fade.", "~", 12f, Violet),
            new AbilityDefinition(AbilityType.HealingLight, WeaponType.Hammer, "Healing Light", "Restore 2 HP to yourself and nearby allies.", "+", 30f, Gold),
            new AbilityDefinition(AbilityType.Judgment, WeaponType.Hammer, "Judgment", "Call down a column of holy light at the cursor. After a short windup it smites and slows enemies around the mark.", "!", 10f, Gold),
            new AbilityDefinition(AbilityType.Sanctuary, WeaponType.Hammer, "Sanctuary", "Surround yourself with a bubble of holy light that moves with you, destroys every projectile inside it, shoves enemies out and slows any that return. Recast to drop it. You cannot attack while it holds.", "O", 16f, Ice),
            new AbilityDefinition(AbilityType.Eclipse, WeaponType.Shadow, "Eclipse", "A vast black sun executes every visible enemy within 11 units, including guardians. Ranks widen its reach.", "O", 6f, Violet),
            new AbilityDefinition(AbilityType.SoulRend, WeaponType.Shadow, "Soul Rend", "Tear open a 14-unit shadow corridor for triple fully charged damage. Ranks multiply its damage.", "///", 3f, Ice),
            new AbilityDefinition(AbilityType.ShadowReign, WeaponType.Shadow, "Shadow Reign", "Detonate nearby shadows. Become invulnerable and double rift damage for 3 seconds. Ranks extend the reign.", "*", 8f, Violet),
            new AbilityDefinition(AbilityType.KnuckleSandwich, WeaponType.Fists, "Knuckle Sandwich", "Wind up and throw a HEAVY punch that smashes everything in a big rectangle ahead. Ranks increase its size and damage.", "[]", 7f, BrawlerAttack.Glove),
            new AbilityDefinition(AbilityType.WildLeap, WeaponType.Fists, "Wild Leap", "Pounce onto an enemy, even over walls, and slam down for massive damage. The farther you leap, the wider the slam. Invulnerable while airborne.", "^", 16f, Gold),
            new AbilityDefinition(AbilityType.PrimalRage, WeaponType.Fists, "Primal Rage", "10 seconds of huge damage, charge, movement and dodge buffs, then 5 seconds tired. Cooldown starts once rested.", "!!", 25f, new Color(1f, 0.25f, 0.2f)),
            new AbilityDefinition(AbilityType.ArchdemonTechnique, WeaponType.Tail, "Archdemon's Technique", "Channel your father, the Demon Lord, for 8 seconds: every click is a fully charged vital stab, and charging winds up a devastating paralysing tail whip. Cooldown starts once it ends.", "W", 16f, HeroBuffs.AscendColor),
            new AbilityDefinition(AbilityType.DemonPaw, WeaponType.Tail, "HEEEELP", "Open a portal over a nearby enemy, even beyond walls: your giant demonic pet slams a huge clawed paw down on it, stunning everything underneath.", "!", 12f, DemonessAttack.Violet),
            new AbilityDefinition(AbilityType.DemonCurse, WeaponType.Tail, "Demon Curse", "Brand a pentagram at the cursor, even past walls: enemies on it are paralysed for 3 seconds and take 50% more damage for 6.", "*", 14f, DemonessAttack.Pale),
            new AbilityDefinition(AbilityType.Windfall, WeaponType.Coins, "Windfall", "Your purse coughs up 5 coins at once. Ranks add one more coin.", "$", 12f, GamblerAttack.Gold),
            new AbilityDefinition(AbilityType.AllIn, WeaponType.Coins, "All In", "Double or nothing on every coin you carry: a 50% chance to double them, or lose them all. Ranks tilt the odds 5% your way.", "x2", 10f, new Color(0.35f, 0.9f, 0.5f)),
            new AbilityDefinition(AbilityType.Jackpot, WeaponType.Coins, "Jackpot", "Feed every coin you carry into the machine. Half the time nothing happens. Otherwise you win a random speed buff, damage buff or heal that grows with every coin spent. Ranks lengthen the buffs.", "777", 18f, new Color(1f, 0.4f, 0.55f))
        };

        public static AbilityDefinition Get(AbilityType type)
        {
            foreach (var ability in All) if (ability.Type == type) return ability;
            return null;
        }
    }
}
