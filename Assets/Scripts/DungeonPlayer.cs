using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonPlayer : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        public int MaxHealth { get; private set; } = 6;
        public int Health { get; private set; } = 6;
        public int BaseDamage { get; private set; } = 1;
        public int Damage => Mathf.Max(1, Mathf.RoundToInt((BaseDamage + (Blessing != null ? Blessing.BonusDamage : 0)
            + (Mechanic != null ? Mechanic.BonusDamage : 0) + (Buffs != null ? Buffs.JackpotDamage : 0) + (Crystals != null ? Crystals.BonusDamage : 0))
            * (Buffs != null ? Buffs.DamageMultiplier : 1f)));
        /// <summary>The class mechanic on R, or null if this hero has not bought it.</summary>
        public ClassMechanic Mechanic { get; private set; }
        /// <summary>Shield Taunt: enemies go for this Knight first.</summary>
        public bool DrawsAggro => Mechanic is ShieldTaunt taunt && taunt.DrawsAggro;
        public DamageBlessing Blessing { get; private set; }
        public HeroBuffs Buffs { get; private set; }
        /// <summary>Crystals for the shop before each boss, and the boss boons bought there.</summary>
        public CrystalPouch Crystals { get; private set; }
        /// <summary>True while a scripted move (such as Wild Leap) controls the hero; input is ignored.</summary>
        public bool IsBusy => Time.time < busyUntil;
        public float Speed { get; private set; } = 5f;
        public bool IsRolling => Time.time < rollUntil;
        public bool IsInvulnerable => Time.time < invulnerableUntil || IsRolling;
        /// <summary>Shadow Veil: enemies cannot see this hero, so they neither chase nor turn toward them.</summary>
        public bool IsVeiled => Time.time < veiledUntil && Health > 0;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        /// <summary>This frame's movement input (zero while standing still).</summary>
        public Vector2 MoveInput { get; private set; }
        private const float RollDuration = 0.25f;
        public const float RollCooldown = 1.4f;
        /// <summary>Seconds between burning-ground damage ticks.</summary>
        public const float BurnInterval = 1f;
        public float DodgeCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, rollReady - Time.time));
        public WeaponType ClassWeapon => weaponType;
        public Vector2 RollDirection => rollDirection;
        public AttackCharge Charge { get; private set; }
        public KnightShield Shield { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        private float invulnerableUntil, rollUntil, rollReady, busyUntil, veiledUntil, nextBurnAt;
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
            Buffs = gameObject.AddComponent<HeroBuffs>();
            Buffs.Player = this;
            Crystals = gameObject.AddComponent<CrystalPouch>();
            Crystals.Player = this;
            Speed = character.MoveSpeed + Permanent.Speed;
            characterColor = character.Color;
            weaponType = character.Weapon;
            Powerups.ClassWeapon = weaponType;
            Abilities = gameObject.AddComponent<PlayerAbilities>();
            Abilities.Player = this;
            Powerups.Abilities = Abilities;
            Charge = gameObject.AddComponent<AttackCharge>();
            Charge.Player = this;
            if (weaponType == WeaponType.Sword)
            {
                Shield = gameObject.AddComponent<KnightShield>();
                Shield.Player = this;
            }
            details = DungeonVisuals.DecorateHero(transform, weaponType, characterColor);
            BlessingSparkles.Attach(transform, () => Health > 0 && Blessing.BonusDamage > 0);
            if (weaponType == WeaponType.Sword) gameObject.AddComponent<KnightRelics>().Player = this;
            if (weaponType == WeaponType.Hammer) gameObject.AddComponent<PaladinRelics>().Player = this;
            Mechanic = ClassMechanic.Attach(this);
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
            else if (weaponType == WeaponType.Fists)
            {
                var fists = gameObject.AddComponent<BrawlerAttack>();
                fists.Player = this;
                Weapon = fists;
            }
            else if (weaponType == WeaponType.Tail)
            {
                var tail = gameObject.AddComponent<DemonessAttack>();
                tail.Player = this;
                Weapon = tail;
            }
            else if (weaponType == WeaponType.Coins)
            {
                var coins = gameObject.AddComponent<GamblerAttack>();
                coins.Player = this;
                Weapon = coins;
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
            // Safety net: a hero with health must never stay hidden, however that health came back.
            if (Health > 0 && hiddenRenderers.Count > 0) SetVisible(true);
            body.color = IsRolling ? new Color(0.4f, 0.65f, 1f) : IsInvulnerable ? Color.white : Buffs.Tint(characterColor);
            SetVeiledLook(IsVeiled);
            if (!Run.IsPlaying || Health <= 0 || IsBusy) { MoveInput = Vector2.zero; Charge.Tick(PlayerInput.Attack, false); return; }
            Vector2 cursor = Run.View.ScreenToWorldPoint(new Vector3(PlayerInput.CursorPosition.x,
                PlayerInput.CursorPosition.y, -Run.View.transform.position.z));
            Vector2 aim = cursor - (Vector2)transform.position;
            if (aim.sqrMagnitude > 0.001f) AimDirection = aim.normalized;
            FaceAim();
            Vector2 movement = MoveInput = PlayerInput.Movement;
            if (PlayerInput.Dodge) TryRoll(MobilityAim(AimDirection));
            Vector2 velocity = IsRolling ? rollDirection * Speed * 2.6f * Buffs.DodgeSpeedMultiplier
                : movement * Speed * Buffs.MoveMultiplier * Crystals.SpeedMultiplier * (Weapon.IsHeavyAttacking ? 0.55f : Charge.IsCharging ? 0.7f : 1f);
            if (DebugMode.Enabled) velocity *= DebugMode.SpeedMultiplier;
            transform.position = Run.Map.Move(transform.position, velocity * Time.deltaTime);
            // Abilities and heavy attacks get the full offset to the cursor, so targeted and mobility moves
            // (Venom Vial, Judgment, Shadowstep, Blink...) stop at the cursor when it is within their range.
            Vector2 toCursor = aim.sqrMagnitude > 0.001f ? aim : AimDirection;
            bool usedAbility = PlayerInput.ActiveQ && Abilities.TryUse(0, toCursor);
            if (!usedAbility && PlayerInput.ActiveE) usedAbility = Abilities.TryUse(1, toCursor);
            if (!usedAbility && Mechanic != null && PlayerInput.Mechanic) usedAbility = Mechanic.TryActivate(toCursor);
            if (IsBusy) { Charge.Cancel(); return; }
            if (!usedAbility && !Run.IsPointerOverHud && PlayerInput.HeavyAttack && !IsRolling) Weapon.TryHeavyAttack(toCursor);
            Charge.Tick(PlayerInput.Attack, !Run.IsPointerOverHud && !usedAbility && !IsRolling && !Weapon.IsHeavyAttacking && !PlayerInput.HeavyAttack);
        }

        /// <summary>
        /// Where a movement move (roll, dash, blink, shadowstep) goes: the way the hero is walking at full range,
        /// or, standing still, <paramref name="toCursor"/> (so cursor-limited moves still stop at the cursor).
        /// </summary>
        public Vector2 MobilityAim(Vector2 toCursor) => MoveInput.sqrMagnitude > 0.01f ? MoveInput.normalized * 1000f : toCursor;

        public bool TryRoll(Vector2 direction)
        {
            if (!Run.IsPlaying || IsRolling || IsBusy || DodgeCooldownRemaining > 0f || direction.sqrMagnitude < 0.001f) return false;
            rollDirection = direction.normalized;
            rollUntil = Time.time + RollDuration;
            rollReady = Time.time + Mathf.Max(0.2f, RollCooldown * Powerups.DodgeCooldownMultiplier * Buffs.DodgeCooldownMultiplier
                - Buffs.DodgeCooldownReduction);
            Weapon?.Hide();
            Charge.Cancel();
            return true;
        }

        /// <summary>Standing in burning ground: one damage per second, however many fires overlap.</summary>
        public void Burn()
        {
            if (Time.time >= nextBurnAt && Hit()) nextBurnAt = Time.time + BurnInterval;
        }

        /// <summary>Takes one hit (or spends a ward). False when nothing landed: invulnerable, dead, or debug mode.</summary>
        public bool Hit()
        {
            if (!Run.IsPlaying || IsInvulnerable || Health <= 0) return false;
            if (DebugMode.Enabled) { Health = MaxHealth; return false; }
            bool warded = Powerups.AbsorbHit();
            if (!warded)
            {
                Health--;
                Mechanic?.OnDamaged();
            }
            if (Run.ProjectileRoot != null)
            {
                if (warded) HeroVfx.Pulse(transform, transform.position, 0.95f, AbilityCatalog.Ice, 0.3f);
                else HeroVfx.Sparks(Run.ProjectileRoot, transform.position, new Color(1f, 0.3f, 0.3f), 10, 3.6f, 0.35f);
            }
            invulnerableUntil = Time.time + 1f;
            if (Health > 0) return true;
            if (!Run.IsNetworked) { Run.EndRun(); return true; }
            SetVisible(false);
            Run.LocalHeroDied();
            return true;
        }

        /// <summary>Co-op: a fallen hero rises at the start of the next floor with half health.</summary>
        public void Revive()
        {
            Health = Mathf.Max(1, MaxHealth / 2);
            invulnerableUntil = Time.time + 1.5f;
            SetVisible(true);
        }

        private readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new System.Collections.Generic.List<Renderer>();

        // Only renderers that were showing get restored, so weapon arcs and indicators keep their own state.
        private void SetVisible(bool value)
        {
            if (!value)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) { renderer.enabled = false; hiddenRenderers.Add(renderer); }
                return;
            }
            foreach (var renderer in hiddenRenderers) if (renderer != null) renderer.enabled = true;
            hiddenRenderers.Clear();
        }

        /// <summary>Heals, shields or blesses this hero; used by local allies and by teammates over the network.</summary>
        /// <param name="teammate">The co-op teammate who sent it; blessings remember them so blessed hits charge their angels.</param>
        public void ApplySupport(SupportKind kind, int amount, float duration, ulong? teammate = null)
        {
            if (kind == SupportKind.Heal) Heal(amount);
            else if (kind == SupportKind.Protect) Protect(duration);
            else if (kind == SupportKind.Bless)
            {
                if (teammate.HasValue) Blessing.ApplyFromTeammate(amount, duration, teammate.Value);
                else Blessing.Apply(amount, duration);
            }
        }

        public void Upgrade(int choice)
        {
            if (choice < 0 || choice >= PowerupCatalog.All.Count || !GrantPowerup((PowerupType)choice)) return;
            Heal(2);
        }

        /// <summary>Adds a rank of <paramref name="type"/> and its stat change (boon choices, crystal shop relics).</summary>
        public bool GrantPowerup(PowerupType type)
        {
            if (!Powerups.Add(type)) return false;
            if (type == PowerupType.Damage) BaseDamage++;
            if (type == PowerupType.Vitality) { MaxHealth += 2; if (Health > 0) Health = MaxHealth; }
            if (type == PowerupType.Movement) Speed += 0.7f;
            return true;
        }

        // Regular hero sprites face right; mirror them when aiming left (small dead zone avoids flicker).
        private void FaceAim()
        {
            if (details == null) return;
            if (AimDirection.x < -0.15f) facingLeft = true;
            else if (AimDirection.x > 0.15f) facingLeft = false;
            body.flipX = details.flipX = facingLeft;
        }

        /// <summary>
        /// Restores health to a living hero. A fallen co-op hero is not healed: only <see cref="Revive"/> raises them,
        /// so their sprites come back and the party stops counting them as dead. (Healing them here, for example through
        /// the boon or artifact picked while spectating, left them alive but invisible on their own screen.)
        /// </summary>
        public void Heal(int amount)
        {
            if (Health <= 0) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
        }
        /// <summary>Raises maximum health and heals by the same amount (the crystal shop's Heart Crystal).</summary>
        public void RaiseMaxHealth(int amount)
        {
            if (amount <= 0) return;
            MaxHealth += amount;
            Heal(amount);
        }
        public void Protect(float duration) { invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration); }
        public void Veil(float duration) { veiledUntil = Mathf.Max(veiledUntil, Time.time + duration); }

        private bool veiledLook;

        /// <summary>A veiled hero fades to a faint shadow so the player can still see where they are.</summary>
        private void SetVeiledLook(bool veiled)
        {
            if (veiled == veiledLook && !veiled) return;
            veiledLook = veiled;
            float alpha = veiled ? 0.3f + 0.08f * Mathf.Sin(Time.time * 6f) : 1f;
            body.color = new Color(body.color.r, body.color.g, body.color.b, alpha);
            if (details != null) details.color = new Color(details.color.r, details.color.g, details.color.b, alpha);
        }
        public void Occupy(float duration) { busyUntil = Mathf.Max(busyUntil, Time.time + duration); }
    }
}
