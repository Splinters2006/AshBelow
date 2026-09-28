using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonPlayer : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        public int MaxHealth { get; private set; } = 6;
        public int Health { get; private set; } = 6;
        public int BaseDamage { get; private set; } = 1;
        public int Damage => BaseDamage + (Blessing != null ? Blessing.BonusDamage : 0);
        public DamageBlessing Blessing { get; private set; }
        public float Speed { get; private set; } = 5f;
        public bool IsRolling => Time.time < rollUntil;
        public bool IsInvulnerable => Time.time < invulnerableUntil || IsRolling;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        private const float RollDuration = 0.25f;
        public const float RollCooldown = 1.4f;
        public float DodgeCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, rollReady - Time.time));
        public WeaponType ClassWeapon => weaponType;
        public Vector2 RollDirection => rollDirection;
        public AttackCharge Charge { get; private set; }
        public KnightShield Shield { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        private float invulnerableUntil, rollUntil, rollReady;
        private Vector2 rollDirection;
        private SpriteRenderer body, details;
        private bool facingLeft;
        private SwordAttack sword;
        public SwordAttack Sword => sword;
        public IPlayerWeapon Weapon { get; private set; }
        public PlayerPowerups Powerups { get; private set; }
        public PermanentBonuses Permanent { get; private set; }
        private WeaponType weaponType;
        private Color characterColor;

        public void Initialize(CharacterDefinition character)
        {
            Powerups = gameObject.AddComponent<PlayerPowerups>();
            Permanent = new PermanentBonuses(Run?.Progress, character.Weapon);
            Powerups.Permanent = Permanent;
            MaxHealth = Health = character.StartingHealth + Permanent.Health;
            BaseDamage = character.StartingDamage + Permanent.Damage;
            Blessing = gameObject.AddComponent<DamageBlessing>();
            Speed = character.MoveSpeed + Permanent.Speed;
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
            details = DungeonVisuals.DecorateHero(transform, weaponType, characterColor);
            var afterimage = gameObject.AddComponent<DodgeAfterimage>();
            afterimage.Player = this;
            afterimage.Tint = Color.Lerp(characterColor, new Color(0.4f, 0.65f, 1f), 0.55f);
        }

        private void Start()
        {
            body = GetComponent<SpriteRenderer>();
            if (weaponType == WeaponType.Shadow)
            {
                var admin = gameObject.AddComponent<AdminAttack>();
                admin.Player = this;
                Weapon = admin;
            }
            else if (weaponType == WeaponType.Bow)
            {
                var bow = gameObject.AddComponent<BowAttack>();
                bow.Player = this;
                Weapon = bow;
                gameObject.AddComponent<ArrowRangeIndicator>().Player = this;
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
                if (weaponType == WeaponType.Hammer)
                {
                    var paladin = gameObject.AddComponent<PaladinAttack>();
                    paladin.Initialize(this, sword);
                    Weapon = paladin;
                }
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
            FaceAim();
            Vector2 movement = PlayerInput.Movement;
            if (PlayerInput.Dodge) TryRoll(movement.sqrMagnitude > 0 ? movement : AimDirection);
            Vector2 velocity = IsRolling ? rollDirection * Speed * 2.6f : movement * Speed * (Weapon.IsHeavyAttacking ? 0.55f : Charge.IsCharging ? 0.7f : 1f);
            if (DebugMode.Enabled) velocity *= DebugMode.SpeedMultiplier;
            transform.position = Run.Map.Move(transform.position, velocity * Time.deltaTime);
            bool usedAbility = PlayerInput.ActiveQ && Abilities.TryUse(0, AimDirection);
            if (!usedAbility && PlayerInput.ActiveE) usedAbility = Abilities.TryUse(1, AimDirection);
            if (!usedAbility && !Run.IsPointerOverHud && PlayerInput.HeavyAttack && !IsRolling) Weapon.TryHeavyAttack(AimDirection);
            Charge.Tick(PlayerInput.Attack, !Run.IsPointerOverHud && !usedAbility && !IsRolling && !Weapon.IsHeavyAttacking && !PlayerInput.HeavyAttack);
        }

        public bool TryRoll(Vector2 direction)
        {
            if (!Run.IsPlaying || IsRolling || DodgeCooldownRemaining > 0f || direction.sqrMagnitude < 0.001f) return false;
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
            if (DebugMode.Enabled) { Health = MaxHealth; return; }
            bool warded = Powerups.AbsorbHit();
            if (!warded) Health--;
            if (Run.ProjectileRoot != null)
            {
                if (warded) HeroVfx.Pulse(transform, transform.position, 0.95f, AbilityCatalog.Ice, 0.3f);
                else HeroVfx.Sparks(Run.ProjectileRoot, transform.position, new Color(1f, 0.3f, 0.3f), 10, 3.6f, 0.35f);
            }
            invulnerableUntil = Time.time + 1f;
            if (Health <= 0) Run.EndRun();
        }

        public void Upgrade(int choice)
        {
            if (choice < 0 || choice >= PowerupCatalog.All.Count || !Powerups.Add((PowerupType)choice)) return;
            if (choice == 0) BaseDamage++;
            if (choice == 1) { MaxHealth += 2; Health = MaxHealth; }
            if (choice == 2) Speed += 0.7f;
            Heal(2);
        }

        // Regular hero sprites face right; mirror them when aiming left (small dead zone avoids flicker).
        private void FaceAim()
        {
            if (details == null) return;
            if (AimDirection.x < -0.15f) facingLeft = true;
            else if (AimDirection.x > 0.15f) facingLeft = false;
            body.flipX = details.flipX = facingLeft;
        }

        public void Heal(int amount) { Health = Mathf.Min(MaxHealth, Health + amount); }
        public void Protect(float duration) { invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration); }
    }
}
