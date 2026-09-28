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
        public const float RollCooldown = 1.4f;
        public float DodgeCooldownRemaining => Mathf.Max(0f, rollReady - Time.time);
        public WeaponType ClassWeapon => weaponType;
        public AttackCharge Charge { get; private set; }
        public KnightShield Shield { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        private float invulnerableUntil, rollUntil, rollReady;
        private Vector2 rollDirection;
        private SpriteRenderer body;
        private SwordAttack sword;
        public SwordAttack Sword => sword;
        public IPlayerWeapon Weapon { get; private set; }
        public PlayerPowerups Powerups { get; private set; }
        private WeaponType weaponType;
        private Color characterColor;

        public void Initialize(CharacterDefinition character)
        {
            Powerups = gameObject.AddComponent<PlayerPowerups>();
            MaxHealth = Health = character.StartingHealth;
            Damage = character.StartingDamage;
            Speed = character.MoveSpeed;
            characterColor = character.Color;
            weaponType = character.Weapon;
            Powerups.ClassWeapon = weaponType;
            Abilities = gameObject.AddComponent<PlayerAbilities>();
            Abilities.Player = this;
            Powerups.Abilities = Abilities;
            Charge = gameObject.AddComponent<AttackCharge>();
            Charge.Player = this;
            if (weaponType == WeaponType.Sword || weaponType == WeaponType.Hammer)
            {
                Shield = gameObject.AddComponent<KnightShield>();
                Shield.Player = this;
            }
            DungeonVisuals.DecorateHero(transform, weaponType, characterColor);
        }

        private void Start()
        {
            body = GetComponent<SpriteRenderer>();
            if (weaponType == WeaponType.Bow)
            {
                var bow = gameObject.AddComponent<BowAttack>();
                bow.Player = this;
                Weapon = bow;
            }
            else if (weaponType == WeaponType.Staff)
            {
                var staff = gameObject.AddComponent<WizardAttack>();
                staff.Player = this;
                Weapon = staff;
            }
            else
            {
                sword = gameObject.AddComponent<SwordAttack>();
                sword.Player = this;
                Weapon = sword;
            }
        }

        private void Update()
        {
            body.color = IsRolling ? new Color(0.4f, 0.65f, 1f) : IsInvulnerable ? Color.white : characterColor;
            if (!Run.IsPlaying) { Charge.Tick(PlayerInput.Attack, false); return; }
            Vector2 cursor = Run.View.ScreenToWorldPoint(new Vector3(PlayerInput.CursorPosition.x,
                PlayerInput.CursorPosition.y, -Run.View.transform.position.z));
            Vector2 aim = cursor - (Vector2)transform.position;
            if (aim.sqrMagnitude > 0.001f) AimDirection = aim.normalized;
            Vector2 movement = PlayerInput.Movement;
            if (PlayerInput.Dodge) TryRoll(movement.sqrMagnitude > 0 ? movement : AimDirection);
            Vector2 velocity = IsRolling ? rollDirection * Speed * 2.6f : movement * Speed * (Weapon.IsHeavyAttacking ? 0.55f : Charge.IsCharging ? 0.7f : 1f);
            transform.position = Run.Map.Move(transform.position, velocity * Time.deltaTime);
            bool usedAbility = PlayerInput.ActiveQ && Abilities.TryUse(0, AimDirection);
            if (!usedAbility && PlayerInput.ActiveF) usedAbility = Abilities.TryUse(1, AimDirection);
            if (!usedAbility && !Run.IsPointerOverHud && PlayerInput.HeavyAttack && !IsRolling) Weapon.TryHeavyAttack(AimDirection);
            Charge.Tick(PlayerInput.Attack, !Run.IsPointerOverHud && !usedAbility && !IsRolling && !Weapon.IsHeavyAttacking && !PlayerInput.HeavyAttack);
        }

        public bool TryRoll(Vector2 direction)
        {
            if (!Run.IsPlaying || IsRolling || Time.time < rollReady || direction.sqrMagnitude < 0.001f) return false;
            rollDirection = direction.normalized;
            rollUntil = Time.time + RollDuration;
            rollReady = Time.time + RollCooldown * Powerups.DodgeCooldownMultiplier;
            Weapon?.Hide();
            Charge.Cancel();
            return true;
        }

        public void Hit()
        {
            if (!Run.IsPlaying || IsInvulnerable) return;
            if (!Powerups.AbsorbHit()) Health--;
            invulnerableUntil = Time.time + 1f;
            if (Health <= 0) Run.EndRun();
        }

        public void Upgrade(int choice)
        {
            if (choice < 0 || choice >= PowerupCatalog.All.Count || !Powerups.Add((PowerupType)choice)) return;
            if (choice == 0) Damage++;
            if (choice == 1) { MaxHealth += 2; Health = MaxHealth; }
            if (choice == 2) Speed += 0.7f;
            Heal(2);
        }

        public void Heal(int amount) { Health = Mathf.Min(MaxHealth, Health + amount); }
        public void Protect(float duration) { invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration); }
    }
}
