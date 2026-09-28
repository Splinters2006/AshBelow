using UnityEngine;

namespace Slopgame
{
    public enum WeaponType { Sword, Bow, Staff, Daggers, Hammer }
    [CreateAssetMenu(menuName = "Slopgame/Character", fileName = "NewCharacter")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Knight";
        [SerializeField, TextArea] private string description = "A sword-and-shield delver. Charge wide slashes and reflect incoming bolts with a timed directional shield.";
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
