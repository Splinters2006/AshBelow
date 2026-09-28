using UnityEngine;

namespace Slopgame
{
    [CreateAssetMenu(menuName = "Slopgame/Character", fileName = "NewCharacter")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Knight";
        [SerializeField, TextArea] private string description = "A sword-wielding delver. Precise slashes, powerful sweeping strikes, and a quick dodge roll.";
        [SerializeField] private Color color = new Color(0.35f, 0.95f, 0.8f);
        [SerializeField, Min(1)] private int startingHealth = 6;
        [SerializeField, Min(1)] private int startingDamage = 1;
        [SerializeField, Min(1)] private float moveSpeed = 5f;
        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public int StartingHealth => startingHealth;
        public int StartingDamage => startingDamage;
        public float MoveSpeed => moveSpeed;
    }
}
