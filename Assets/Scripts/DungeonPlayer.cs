using UnityEngine;

namespace Slopgame
{
    public sealed class DungeonPlayer : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        /// <summary>
        /// Maximum HP after Glass Cannon and the Infernal Pact scale it (rounded up); every max HP gain adds to the
        /// unscaled total, so Glass Cannon also halves later gains.
        /// </summary>
        public int MaxHealth => Mathf.Max(1, Mathf.CeilToInt(rawMaxHealth * (Powerups != null ? Powerups.MaxHealthMultiplier : 1f)));
        private int rawMaxHealth = 6;
        /// <summary>Fired when a hit lands on this hero (true when a ward took it); Thorns and class talents listen.</summary>
        public event System.Action<bool> Struck;
        /// <summary>Fired when a blow glances off while the hero is invulnerable (Aegis Burst counts these).</summary>
        public event System.Action Deflected;
        public int Health { get; private set; } = 6;
        public int BaseDamage { get; private set; } = 1;
        public int Damage => Mathf.Max(1, Mathf.RoundToInt((BaseDamage + (Blessing != null ? Blessing.BonusDamage : 0)
            + (Mechanic != null ? Mechanic.BonusDamage : 0) + (Buffs != null ? Buffs.JackpotDamage : 0) + (Crystals != null ? Crystals.BonusDamage : 0)
            + (Powerups != null ? Powerups.BasicAttackBonus : 0) + WarBanner.BonusAt(transform.position))
            * (Buffs != null ? Buffs.DamageMultiplier : 1f) * (Powerups != null ? Powerups.DamageMultiplier(this) : 1f)));
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
        /// <summary>Shield Taunt: the Knight can walk with his great shield up, but not attack, dodge or use abilities.</summary>
        public bool IsHoldingShield => Mechanic is ShieldTaunt taunt && taunt.IsTaunting;
        public float Speed { get; private set; } = 5f;
        public bool IsRolling => Time.time < rollUntil;
        /// <summary>Poisoned by the Savage Wilds' venom: slowed and tinted green, then one damage when it wears off.</summary>
        public bool IsPoisoned => poisonedUntil > 0f;
        public const float PoisonDuration = 4f, PoisonSlow = 0.75f;
        public static readonly Color PoisonGreen = new Color(0.45f, 1f, 0.3f);
        private float poisonedUntil, nextPoisonMote;
        /// <summary>Set alight by brimstone and hex fire: one damage every <see cref="IgniteInterval"/> until the ticks run out or a dodge roll smothers it.</summary>
        public bool IsIgnited => igniteTicks > 0;
        public const float IgniteInterval = 1.2f;
        public static readonly Color IgniteOrange = new Color(1f, 0.5f, 0.15f);
        private int igniteTicks;
        private float nextIgniteAt, nextIgniteFlame;
        public bool IsInvulnerable => Time.time < invulnerableUntil || IsRolling;
        /// <summary>Shadow Veil: enemies cannot see this hero, so they neither chase nor turn toward them.</summary>
        /// <summary>Unscaled time the hero last fell, so the HUD can let the death animation play first.</summary>
        public float FellAt { get; private set; } = float.NegativeInfinity;
        public bool IsFalling => Health <= 0 && Time.unscaledTime < FellAt + DeathAnimation.HeroDuration;
        public bool IsVeiled => Time.time < veiledUntil && Health > 0;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        /// <summary>Where the cursor points in the world (Orbital Laser follows it).</summary>
        public Vector2 CursorPoint { get; private set; }
        /// <summary>This frame's movement input (zero while standing still).</summary>
        public Vector2 MoveInput { get; private set; }
        private const float RollDuration = 0.25f;
        public const float FootworkGrace = 0.25f;
        public const float RollCooldown = 1.4f;
        /// <summary>Seconds between burning-ground damage ticks.</summary>
        public const float BurnInterval = 1f;
        public float DodgeCooldownRemaining => DebugMode.Cooldown(Mathf.Max(0f, rollReady - Time.time));
        public WeaponType ClassWeapon => weaponType;
        public Vector2 RollDirection => rollDirection;
        public AttackCharge Charge { get; private set; }
        public KnightShield Shield { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        /// <summary>How long the hero blinks red after losing health.</summary>
        public const float HurtBlink = 0.35f;
        public static readonly Color HurtColor = new Color(1f, 0.25f, 0.25f);
        /// <summary>True just after losing health, while the hero blinks red.</summary>
        public bool IsHurt => Time.time < hurtUntil;
        private float invulnerableUntil, rollUntil, rollReady, busyUntil, veiledUntil, nextBurnAt, hurtUntil;
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
            rawMaxHealth = character.StartingHealth + Permanent.Health;
            Health = MaxHealth;
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
            else if (weaponType == WeaponType.Beam)
            {
                var cannon = gameObject.AddComponent<CyborgAttack>();
                cannon.Player = this;
                Weapon = cannon;
            }
            else if (weaponType == WeaponType.Scythe)
            {
                var scythe = gameObject.AddComponent<ReaperAttack>();
                scythe.Player = this;
                Weapon = scythe;
            }
            else if (weaponType == WeaponType.Katana)
            {
                var katana = gameObject.AddComponent<SamuraiAttack>();
                katana.Player = this;
                Weapon = katana;
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
            body.color = IsHurt ? (Mathf.Repeat(Time.time * 16f, 1f) < 0.5f ? HurtColor : Color.white)
                : IsRolling ? new Color(0.4f, 0.65f, 1f) : IsHoldingShield ? HeroBuffs.AngryTint(characterColor) : IsInvulnerable ? Color.white
                : IsIgnited ? Color.Lerp(Buffs.Tint(characterColor), IgniteOrange, 0.5f + 0.2f * Mathf.Sin(Time.time * 14f))
                : IsPoisoned ? Color.Lerp(Buffs.Tint(characterColor), PoisonGreen, 0.55f) : Buffs.Tint(characterColor);
            UpdatePoison();
            UpdateIgnite();
            SetVeiledLook(IsVeiled || IsIntangible);
            if (IsVeiled && !wasVeiled && Powerups.Count(PowerupType.Ambush) > 0) Powerups.AmbushReady = true;
            wasVeiled = IsVeiled;
            if (!Run.IsPlaying || Health <= 0 || IsBusy || Run.HudCapturesInput) { MoveInput = Vector2.zero; Charge.Tick(PlayerInput.Attack, false); return; }
            Vector2 cursor = Run.View.ScreenToWorldPoint(new Vector3(PlayerInput.CursorPosition.x,
                PlayerInput.CursorPosition.y, -Run.View.transform.position.z));
            CursorPoint = cursor;
            Vector2 aim = cursor - (Vector2)transform.position;
            if (aim.sqrMagnitude > 0.001f) AimDirection = aim.normalized;
            FaceAim();
            Vector2 movement = MoveInput = PlayerInput.Movement;
            bool holdingShield = IsHoldingShield;
            if (PlayerInput.Dodge && !holdingShield) TryRoll(MobilityAim(AimDirection));
            // A roll can be steered: it keeps its speed and length but follows the movement keys.
            if (IsRolling && movement.sqrMagnitude > 0.01f) rollDirection = movement.normalized;
            Vector2 velocity = IsRolling ? rollDirection * Speed * 2.6f * Buffs.DodgeSpeedMultiplier
                : movement * Speed * Buffs.MoveMultiplier * Crystals.SpeedMultiplier * (IsPoisoned ? PoisonSlow : 1f) * Powerups.MoveMultiplier(this) * (Blessing.IsHasted ? DamageBlessing.ShepherdSpeed : 1f) * (Weapon.IsHeavyAttacking ? 0.55f : Charge.IsCharging ? 0.7f : 1f);
            if (DebugMode.Enabled) velocity *= DebugMode.SpeedMultiplier;
            // Ice walls stop heroes too; slide along them rather than sticking.
            Vector2 here = transform.position, moved = Run.Map.Move(here, velocity * Time.deltaTime);
            if (IceWall.BlocksHero(here, moved))
            {
                Vector2 alongX = Run.Map.Move(here, new Vector2(velocity.x, 0f) * Time.deltaTime), alongY = Run.Map.Move(here, new Vector2(0f, velocity.y) * Time.deltaTime);
                moved = !IceWall.BlocksHero(here, alongX) ? alongX : !IceWall.BlocksHero(here, alongY) ? alongY : here;
            }
            transform.position = moved;
            // Rolling into an urn smashes it.
            if (IsRolling) Breakable.SmashAt(Run, transform.position, 0.3f);
            // Abilities and heavy attacks get the full offset to the cursor, so targeted and mobility moves
            // (Venom Vial, Judgment, Shadowstep, Blink...) stop at the cursor when it is within their range.
            Vector2 toCursor = aim.sqrMagnitude > 0.001f ? aim : AimDirection;
            if (holdingShield) { Charge.Cancel(); return; }
            bool usedAbility = PlayerInput.ActiveQ && Abilities.TryUse(0, toCursor);
            if (!usedAbility && PlayerInput.ActiveE) usedAbility = Abilities.TryUse(1, toCursor);
            if (!usedAbility && Mechanic != null && PlayerInput.Mechanic) usedAbility = Mechanic.TryActivate(toCursor);
            if (IsBusy) { Charge.Cancel(); return; }
            if (!usedAbility && !Run.IsPointerOverHud && PlayerInput.HeavyAttack && !IsRolling && Powerups.BasicAttack(() => Weapon.TryHeavyAttack(toCursor)))
                Breakable.SmashInArc(this, toCursor, Breakable.HeavyReach);
            // Brawler mid-roll: the charge is left alone, then keeps building (or fires, if released) once the roll ends.
            if (IsRolling && Weapon is BrawlerAttack) return;
            Charge.Tick(PlayerInput.Attack, !Run.IsPointerOverHud && !usedAbility && !IsRolling && !Weapon.IsHeavyAttacking && !PlayerInput.HeavyAttack);
        }

        /// <summary>
        /// Where the dodge roll goes: the way the hero is walking, or toward <paramref name="toCursor"/> when standing still.
        /// (Movement abilities such as dashes and blinks always aim at the cursor.)
        /// </summary>
        public Vector2 MobilityAim(Vector2 toCursor) => MoveInput.sqrMagnitude > 0.01f ? MoveInput.normalized * 1000f : toCursor;

        public void ResetDodge() => rollReady = Mathf.Min(rollReady, Time.time);
        public void ResetClassSkill() => Weapon?.ReduceHeavyCooldown(float.PositiveInfinity);

        /// <summary>Kill talents: takes time off the dodge, class skill, relic abilities and a cooldown-based class mechanic.</summary>
        public void ReduceCooldowns(float seconds)
        {
            rollReady = Cooldowns.Shorten(rollReady, seconds);
            Weapon?.ReduceHeavyCooldown(seconds);
            Abilities?.ReduceCooldowns(seconds);
            Mechanic?.ReduceCooldown(seconds);
        }

        public bool TryRoll(Vector2 direction)
        {
            if (!Run.IsPlaying || IsRolling || IsBusy || DodgeCooldownRemaining > 0f || direction.sqrMagnitude < 0.001f) return false;
            rollDirection = direction.normalized;
            rollUntil = Time.time + RollDuration;
            // Coolant: the roll vents 30% of the plasma cannon's remaining cooldown.
            if (Powerups.Count(PowerupType.Coolant) > 0 && Weapon is CyborgAttack cannon) cannon.ReduceHeavyCooldown(cannon.HeavyCooldownRemaining * 0.3f);
            // Footwork: the Brawler stays untouchable a beat after the roll.
            if (Powerups.Count(PowerupType.Footwork) > 0) Protect(RollDuration + FootworkGrace);
            // Rolling smothers the flames.
            if (IsIgnited)
            {
                igniteTicks = 0;
                HeroVfx.Motes(Run.ProjectileRoot, transform.position, 0.5f, new Color(0.8f, 0.8f, 0.8f), 10, 0.6f);
            }
            rollReady = Time.time + Mathf.Max(0.2f, RollCooldown * Powerups.DodgeCooldownMultiplier * Buffs.DodgeCooldownMultiplier
                - Buffs.DodgeCooldownReduction);
            // The Brawler keeps a held punch charging through the roll; every other class loses it.
            // Mastered Technique (her passive): she does not even stop a running barrage.
            if (Weapon is BrawlerAttack brawler) { if (!brawler.KeepsBarrageWhileRolling) brawler.StopBarrage(); }
            else
            {
                Weapon?.Hide();
                Charge.Cancel();
            }
            return true;
        }

        /// <summary>Venom poisons a living hero; a fresh dose restarts the timer rather than stacking.</summary>
        public void Poison()
        {
            if (Health <= 0 || DebugMode.Enabled) return;
            if (!IsPoisoned) HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 0.7f, PoisonGreen, 0.3f);
            poisonedUntil = Time.time + PoisonDuration;
        }

        /// <summary>Sets the hero alight for <paramref name="ticks"/> burns; a fresh flame tops the ticks up rather than stacking.</summary>
        public void Ignite(int ticks)
        {
            if (Health <= 0 || DebugMode.Enabled || ticks <= 0) return;
            if (!IsIgnited) nextIgniteAt = Time.time + IgniteInterval;
            igniteTicks = Mathf.Max(igniteTicks, ticks);
        }

        private void UpdateIgnite()
        {
            if (!IsIgnited) return;
            if (Health <= 0) { igniteTicks = 0; return; }
            if (!Run.IsPlaying) { nextIgniteAt += Time.deltaTime; return; }
            if (Time.time >= nextIgniteFlame)
            {
                nextIgniteFlame = Time.time + 0.1f;
                HeroVfx.Sparks(Run.ProjectileRoot, (Vector2)transform.position + Random.insideUnitCircle * 0.2f, IgniteOrange, 2, 1.8f, 0.3f, Vector2.up, 50f, 0.8f);
            }
            if (Time.time < nextIgniteAt) return;
            igniteTicks--;
            nextIgniteAt = Time.time + IgniteInterval;
            if (Hit()) HeroVfx.Sparks(Run.ProjectileRoot, transform.position, IgniteOrange, 10, 3.5f, 0.35f, Vector2.up, 140f);
        }

        /// <summary>Clears poison without its parting damage (a heart pickup).</summary>
        public void CurePoison() => poisonedUntil = 0f;

        private void UpdatePoison()
        {
            if (!IsPoisoned) return;
            if (Health <= 0) { poisonedUntil = 0f; return; }
            if (!Run.IsPlaying) { poisonedUntil += Time.deltaTime; return; }
            if (Time.time >= nextPoisonMote)
            {
                nextPoisonMote = Time.time + 0.35f;
                HeroVfx.Sparks(Run.ProjectileRoot, (Vector2)transform.position + Vector2.up * 0.2f, PoisonGreen, 2, 1.2f, 0.4f, Vector2.up, 60f, 0.7f);
            }
            if (Time.time < poisonedUntil) return;
            poisonedUntil = 0f;
            // The venom's parting sting; a ward still absorbs it.
            if (Hit()) HeroVfx.Sparks(Run.ProjectileRoot, transform.position, PoisonGreen, 10, 3.5f, 0.35f, Vector2.up, 140f);
        }

        /// <summary>Whether a killing blow is survived at 1 HP (and spends that save).</summary>
        private bool TryDefyDeath()
        {
            bool saved = false;
            if (Powerups.Count(PowerupType.CheatDeath) > 0 && !Powerups.CheatDeathSpent) { Powerups.CheatDeathSpent = true; saved = true; }
            else if (Permanent.BackupDrive && !backupDriveSpent) { backupDriveSpent = true; saved = true; }
            if (!saved) return false;
            HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 1.6f, AbilityCatalog.Gold, 0.6f);
            HeroVfx.Motes(Run.ProjectileRoot, transform.position, 0.9f, AbilityCatalog.Gold, 18, 1f);
            ScreenFx.Flash(new Color(1f, 0.85f, 0.4f, 0.4f), 0.5f);
            return true;
        }
        private bool backupDriveSpent, wasVeiled;
        private float interventionUntil, insuredUntil;
        public const float InsuranceTime = 6f;
        public const int InsurancePremium = 5;
        /// <summary>Insurance: hits cost coins instead of HP for a while.</summary>
        public void Insure(float duration)
        {
            insuredUntil = Mathf.Max(insuredUntil, Time.time + duration);
            HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 1.1f, InsuranceVfx.Policy, 0.4f);
            InsuranceVfx.Play(Run.ProjectileRoot, transform, duration);
            CoopFx.Insurance(Run, duration);
        }

        /// <summary>Divine Intervention: for a while, a killing blow is turned aside.</summary>
        public void Intercede(float duration) => interventionUntil = Mathf.Max(interventionUntil, Time.time + duration);

        public const float InterventionRescueHealth = 0.75f;

        /// <summary>Saved by Divine Intervention: back at three quarters of max HP, untouchable and empowered for 2 seconds.</summary>
        private void Rescue()
        {
            interventionUntil = 0f;
            GuardianAngelsVfx.Rescued(Run.ProjectileRoot, transform);
            CoopFx.InterventionSaved(Run);
            Health = Mathf.Max(1, Mathf.CeilToInt(MaxHealth * InterventionRescueHealth));
            Protect(2f);
            Blessing.Apply(2, 2f);
            HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 2f, new Color(1f, 0.95f, 0.7f), 0.6f);
            HeroVfx.Motes(Run.ProjectileRoot, transform.position, 1f, AbilityCatalog.Gold, 24, 1.2f);
            ScreenFx.Flash(new Color(1f, 0.95f, 0.7f, 0.45f), 0.5f);
        }

        /// <summary>Standing in burning ground: one tick of damage per second, however many fires overlap.</summary>
        public void Burn(int damage = 1)
        {
            if (Time.time >= nextBurnAt && Hit(damage)) nextBurnAt = Time.time + BurnInterval;
        }

        /// <summary>
        /// Takes one hit of <paramref name="damage"/> (or spends a ward, which turns the whole blow aside). False when
        /// nothing landed: invulnerable, dead, or debug mode.
        /// </summary>
        public bool Hit(int damage = 1) => Strike(false, damage);

        /// <summary>
        /// A killing blow: every hit point at once. Only what would turn aside any other blow still answers it: a roll's
        /// invulnerability, a ward, and the rescues that defy death.
        /// </summary>
        public bool Slay() => Strike(true, 1);

        private bool Strike(bool lethal, int damage)
        {
            if (Run.IsPlaying && IsInvulnerable && Health > 0) Deflected?.Invoke();
            if (!Run.IsPlaying || IsInvulnerable || Health <= 0) return false;
            if (DebugMode.Enabled) { Health = MaxHealth; return false; }
            // Insurance: while the policy holds, the Gambler pays in coins instead of blood.
            bool claiming = Time.time < insuredUntil && Weapon is GamblerAttack;
            bool insured = claiming && ((GamblerAttack)Weapon).Spend(InsurancePremium);
            if (claiming)
            {
                // Paid out in coins, or denied when the purse can't cover the premium.
                InsuranceVfx.Claim(Run.ProjectileRoot, transform, insured);
                CoopFx.InsuranceClaim(Run, insured);
            }
            bool warded = insured || Powerups.AbsorbHit();
            if (!warded)
            {
                // Cheat Death (once per world), then the Ash shop's Backup Drive (once per descent), turn a killing blow into 1 HP.
                if ((lethal || Health <= damage) && Time.time < interventionUntil) Rescue();
                else if ((lethal || Health <= damage) && TryDefyDeath()) Health = 1;
                else Health -= lethal ? Health : Mathf.Min(Health, Mathf.Max(1, damage));
                Mechanic?.OnDamaged();
            }
            Powerups.OnStruck(this);
            Struck?.Invoke(warded);
            if (Run.ProjectileRoot != null)
            {
                if (warded) HeroVfx.Pulse(transform, transform.position, 0.95f, AbilityCatalog.Ice, 0.3f);
                else
                {
                    HeroVfx.Sparks(Run.ProjectileRoot, transform.position, HurtColor, 14, 4.2f, 0.4f);
                    HeroVfx.Pulse(transform, transform.position, 1.2f, HurtColor, 0.3f);
                }
            }
            // Make every hit unmistakable: a red flash and a jolt (a blocked hit flashes ice blue), then the hero blinks red.
            ScreenFx.Flash(warded ? new Color(0.4f, 0.75f, 1f, 0.22f) : new Color(1f, 0.08f, 0.08f, 0.38f), warded ? 0.25f : 0.4f);
            ScreenFx.Shake(warded ? 0.08f : 0.2f, warded ? 0.12f : 0.25f);
            if (!warded) hurtUntil = Time.time + HurtBlink;
            invulnerableUntil = Time.time + 1f;
            if (Health > 0) return true;
            // The hero topples over; a solo death holds the game-over screen back until the fall has played.
            FellAt = Time.unscaledTime;
            if (Run.ProjectileRoot != null) DeathAnimation.Play(transform, Run.ProjectileRoot, DeathAnimation.HeroDuration, true);
            SetVisible(false);
            if (!Run.IsNetworked) { Run.EndRun(); return true; }
            Run.LocalHeroDied();
            return true;
        }

        /// <summary>
        /// Co-op: a fallen hero rises with half their health, whether at the start of the next floor or raised
        /// mid-fight by a teammate's Heavenly Host.
        /// </summary>
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
            else if (kind == SupportKind.Ward) for (int i = 0; i < Mathf.Max(1, amount); i++) Powerups.AddWard();
            else if (kind == SupportKind.Intervention) Intercede(duration);
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
            Run?.Progress?.Discover(Encyclopedia.TalentId(type));
            if (type == PowerupType.Damage) BaseDamage++;
            if (type == PowerupType.Vitality) { rawMaxHealth += 2; if (Health > 0) Health = MaxHealth; }
            if (type == PowerupType.GlassCannon) Health = Mathf.Min(Health, MaxHealth);
            if (type == PowerupType.BloodPact) { rawMaxHealth = Mathf.Max(1, rawMaxHealth - 1); Health = Mathf.Min(Health, MaxHealth); }
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
            rawMaxHealth += amount;
            Heal(amount);
        }
        public void Protect(float duration) { invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration); }
        public void Veil(float duration) { veiledUntil = Mathf.Max(veiledUntil, Time.time + duration); }

        /// <summary>Shade Walk: the Reaper's body is intangible, so enemies' touch passes through him (bolts and hazards still land).</summary>
        public bool IsIntangible => Time.time < intangibleUntil && Health > 0;
        private float intangibleUntil;
        public void ShadeWalk(float duration) { intangibleUntil = Mathf.Max(intangibleUntil, Time.time + duration); }

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
