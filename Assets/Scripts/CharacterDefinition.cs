using UnityEngine;

namespace Slopgame
{
    // The values are saved in the character assets, so new weapons are appended; the Specimen took over the retired Admin's slot.
    public enum WeaponType { Sword, Bow, Staff, Daggers, Hammer, Mutation, Fists, Tail, Coins, Beam, Scythe, Katana }
    [CreateAssetMenu(menuName = "Slopgame/Character", fileName = "NewCharacter")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Knight";
        [SerializeField, TextArea] private string description = "A veteran of wars nobody remembers winning. The oath outlived the kingdom, and the shield arm never learned to rest.";
        [SerializeField] private Color color = new Color(0.35f, 0.95f, 0.8f);
        [SerializeField, Min(1)] private int startingHealth = 6;
        [SerializeField, Min(1)] private int startingDamage = 1;
        [SerializeField, Min(1)] private float moveSpeed = 5f;
        [SerializeField] private WeaponType weapon;
        public WeaponType Weapon => weapon;
        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public int StartingHealth => startingHealth;
        public int StartingDamage => startingDamage;
        public float MoveSpeed => moveSpeed;
    }
}

namespace Slopgame
{
    /// <summary>
    /// The one order every hero list in the game follows: hero select, the co-op lobby, the Ash shop's tabs, the
    /// encyclopedia, the autofire settings and the class passives.
    /// </summary>
    public static class HeroRoster
    {
        public static readonly WeaponType[] Order =
        {
            WeaponType.Sword, WeaponType.Bow, WeaponType.Staff, WeaponType.Daggers, WeaponType.Hammer, WeaponType.Fists,
            WeaponType.Tail, WeaponType.Coins, WeaponType.Beam, WeaponType.Scythe, WeaponType.Katana, WeaponType.Mutation
        };

        /// <summary>Where a hero sits in <see cref="Order"/> (heroes missing from it go last).</summary>
        public static int IndexOf(WeaponType weapon)
        {
            int index = System.Array.IndexOf(Order, weapon);
            return index < 0 ? Order.Length : index;
        }

        /// <summary>Each hero's name, for lists drawn without the character assets to hand (the settings page).</summary>
        public static string Name(WeaponType weapon) => weapon switch
        {
            WeaponType.Sword => "Knight",
            WeaponType.Bow => "Archer",
            WeaponType.Staff => "Wizard",
            WeaponType.Daggers => "Assassin",
            WeaponType.Hammer => "Paladin",
            WeaponType.Fists => "Brawler",
            WeaponType.Tail => "Demoness",
            WeaponType.Coins => "Gambler",
            WeaponType.Beam => "Augment",
            WeaponType.Scythe => "Reaper",
            WeaponType.Katana => "Samurai",
            WeaponType.Mutation => "Specimen",
            _ => weapon.ToString()
        };
    }
}
