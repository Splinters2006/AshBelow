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
