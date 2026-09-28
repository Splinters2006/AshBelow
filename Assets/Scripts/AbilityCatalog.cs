using UnityEngine;

namespace Slopgame
{
    public enum AbilityType
    {
        None, ShieldRush, Earthshatter, Aegis, Volley, PiercingShot, Windstep,
        Fireball, FrostNova, Blink, FanOfKnives, VenomStrike, ShadowVeil, HealingLight, Judgment, Sanctuary,
        Eclipse, SoulRend, ShadowReign
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
            new AbilityDefinition(AbilityType.ShieldRush, WeaponType.Sword, "Shield Rush", "Surge forward, striking enemies in your path. Briefly invulnerable.", ">>", 8f, Ice),
            new AbilityDefinition(AbilityType.Earthshatter, WeaponType.Sword, "Earthshatter", "A heavy shockwave damages and slows nearby enemies.", "X", 10f, Gold),
            new AbilityDefinition(AbilityType.Aegis, WeaponType.Sword, "Aegis", "Become invulnerable for 2 seconds. Ranks extend protection.", "O", 14f, Ice),
            new AbilityDefinition(AbilityType.Volley, WeaponType.Bow, "Arrow Volley", "Unleash a seven-arrow fan. Arrows keep their 5-unit range.", "///", 8f, Green),
            new AbilityDefinition(AbilityType.PiercingShot, WeaponType.Bow, "Piercing Shot", "A powerful arrow pierces a line of enemies. Range: 5 units.", "->", 9f, Gold),
            new AbilityDefinition(AbilityType.Windstep, WeaponType.Bow, "Windstep", "Dash in your aim direction and fire a three-arrow counterattack.", ">>", 10f, Green),
            new AbilityDefinition(AbilityType.Fireball, WeaponType.Staff, "Inferno Orb", "Unlock an explosive fireball. Its elemental effect can ignite enemies.", "*", 8f, new Color(1f, 0.43f, 0.23f)),
            new AbilityDefinition(AbilityType.FrostNova, WeaponType.Staff, "Frost Nova", "Damage nearby enemies with ice, slowing all their actions by 50%.", "+", 10f, Ice),
            new AbilityDefinition(AbilityType.Blink, WeaponType.Staff, "Arcane Blink", "Blink forward and release an icy pulse at your destination.", "<>", 9f, Violet),
            new AbilityDefinition(AbilityType.FanOfKnives, WeaponType.Daggers, "Fan of Knives", "Throw a ring of short-range knives. Each can critically strike.", "X", 8f, Violet),
            new AbilityDefinition(AbilityType.VenomStrike, WeaponType.Daggers, "Venom Strike", "Wound nearby enemies with poison and a heavy physical hit.", "+", 9f, Green),
            new AbilityDefinition(AbilityType.ShadowVeil, WeaponType.Daggers, "Shadow Veil", "Brief invulnerability lets you slip behind enemies safely.", "~", 12f, Violet),
            new AbilityDefinition(AbilityType.HealingLight, WeaponType.Hammer, "Healing Light", "Restore 2 HP to yourself and nearby allies.", "+", 16f, Gold),
            new AbilityDefinition(AbilityType.Judgment, WeaponType.Hammer, "Judgment", "Smite and slow enemies around you with a radiant shockwave.", "!", 10f, Gold),
            new AbilityDefinition(AbilityType.Sanctuary, WeaponType.Hammer, "Sanctuary", "Protect yourself and nearby allies with temporary invulnerability.", "O", 16f, Ice),
            new AbilityDefinition(AbilityType.Eclipse, WeaponType.Shadow, "Eclipse", "A vast black sun executes every visible enemy within 11 units, including guardians. Ranks widen its reach.", "O", 6f, Violet),
            new AbilityDefinition(AbilityType.SoulRend, WeaponType.Shadow, "Soul Rend", "Tear open a 14-unit shadow corridor for triple fully charged damage. Ranks multiply its damage.", "///", 3f, Ice),
            new AbilityDefinition(AbilityType.ShadowReign, WeaponType.Shadow, "Shadow Reign", "Detonate nearby shadows. Become invulnerable and double rift damage for 3 seconds. Ranks extend the reign.", "*", 8f, Violet)
        };

        public static AbilityDefinition Get(AbilityType type)
        {
            foreach (var ability in All) if (ability.Type == type) return ability;
            return null;
        }
    }
}
