using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonPlayer : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        public int MaxHealth { get; private set; } = 6;
        public int Health { get; private set; } = 6;
        public int Damage { get; private set; } = 1;
        public float Speed { get; private set; } = 5f;
        public bool IsRolling => Time.time < rollUntil;
        public bool IsInvulnerable => Time.time < invulnerableUntil || IsRolling;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        private const float RollDuration = 0.25f;
        private const float RollCooldown = 0.8f;
        private float invulnerableUntil, rollUntil, rollReady;
        private Vector2 rollDirection;
        private SpriteRenderer body;
        private SwordAttack sword;
        public SwordAttack Sword => sword;
        private Color characterColor;

        public void Initialize(CharacterDefinition character)
        {
            MaxHealth = Health = character.StartingHealth;
            Damage = character.StartingDamage;
            Speed = character.MoveSpeed;
            characterColor = character.Color;
        }

        private void Start()
        {
            body = GetComponent<SpriteRenderer>();
            sword = gameObject.AddComponent<SwordAttack>();
            sword.Player = this;
        }

        private void Update()
        {
            body.color = IsRolling ? new Color(0.4f, 0.65f, 1f) : IsInvulnerable ? Color.white : characterColor;
            if (!Run.IsPlaying) return;
            Vector2 cursor = Run.View.ScreenToWorldPoint(new Vector3(PlayerInput.CursorPosition.x,
                PlayerInput.CursorPosition.y, -Run.View.transform.position.z));
            Vector2 aim = cursor - (Vector2)transform.position;
            if (aim.sqrMagnitude > 0.001f) AimDirection = aim.normalized;
            Vector2 movement = PlayerInput.Movement;
            if (PlayerInput.Dodge) TryRoll(movement.sqrMagnitude > 0 ? movement : AimDirection);
            Vector2 velocity = IsRolling ? rollDirection * Speed * 2.6f : movement * Speed * (sword.IsHeavyAttacking ? 0.55f : 1f);
            transform.position = Run.Map.Move(transform.position, velocity * Time.deltaTime);
            if (PlayerInput.HeavyAttack && !IsRolling) sword.TryHeavyAttack(AimDirection);
            else if (PlayerInput.Attack && !IsRolling) sword.TryAttack(AimDirection);
        }

        public bool TryRoll(Vector2 direction)
        {
            if (!Run.IsPlaying || IsRolling || Time.time < rollReady || direction.sqrMagnitude < 0.001f) return false;
            rollDirection = direction.normalized;
            rollUntil = Time.time + RollDuration;
            rollReady = Time.time + RollCooldown;
            if (sword != null) sword.Hide();
            return true;
        }

        public void Hit()
        {
            if (!Run.IsPlaying || IsInvulnerable) return;
            Health--;
            invulnerableUntil = Time.time + 1f;
            if (Health <= 0) Run.EndRun();
        }

        public void Upgrade(int choice)
        {
            if (choice == 0) Damage++;
            if (choice == 1) { MaxHealth += 2; Health = MaxHealth; }
            if (choice == 2) Speed += 0.7f;
            Health = Mathf.Min(MaxHealth, Health + 2);
        }
    }
}
