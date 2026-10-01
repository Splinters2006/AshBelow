using UnityEngine;

namespace Slopgame
{
    [RequireComponent(typeof(EnemyFacing), typeof(EnemyTactics))]
    public sealed class DungeonEnemy : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        public int Health { get; set; }
        public float Speed { get; set; }
        private float hitUntil;
        private SpriteRenderer body;
        private EnemyShooter shooter;
        /// <summary>The special kind of floor enemy this is (including world-specific specialists), or null for the basic three.</summary>
        public EnemyVariant Variant => variant != null ? variant : variant = GetComponent<EnemyVariant>();
        private EnemyVariant variant;
        public DungeonBoss Boss { get; set; }
        public bool IsTank { get; set; }
        /// <summary>Summoned by a guardian mid-fight; dissolves when its guardian falls.</summary>
        public bool IsMinion { get; set; }
        public float HitRadius => Boss != null ? Boss.HitRadius : IsTank ? 0.5f : 0.38f;
        /// <summary>True while a boss is out of reach (such as the Archdemon in flight); blows glance off.</summary>
        public bool IsInvulnerable => Boss != null && Boss.IsInvulnerable;
        public float MoveRadius => IsTank ? 0.42f : 0.28f;
        public bool IsBurning => burnTicks > 0 || netBurning;
        /// <summary>Elemental Immobilization: an element has touched this enemy (burn, chill, freeze or shock) since it was last seized.</summary>
        public bool ElementTouched { get; private set; }
        public void TouchWithElement() { if (!IsInvulnerable && Health > 0) ElementTouched = true; }
        /// <summary>Spends the element's touch; true if there was one to spend.</summary>
        public bool ConsumeElementTouch()
        {
            bool touched = ElementTouched;
            ElementTouched = false;
            return touched;
        }
        /// <summary>Spawn-order id shared by every machine in a co-op run.</summary>
        public ushort NetId { get; set; }
        private Vector2 netPosition;
        private bool netBurning, netFlashing, hasSnapshot;
        private EnemyTactics tactics;
        private SpriteRenderer burnIndicator;
        public bool IsChilled => Time.time < chilledUntil;
        /// <summary>Frozen solid by ice: like paralysis, a frozen enemy cannot move, turn or attack.</summary>
        public bool IsFrozen => Time.time < frozenUntil;
        /// <summary>True while paralysis or ice holds the enemy completely still.</summary>
        public bool IsHeld => IsParalyzed || IsFrozen || IsStunned;
        /// <summary>Stunned (Holy Lance, Thunder Clap, EMP Pulse...): held like paralysis, but not the Demoness's paralysis.</summary>
        public bool IsStunned => Time.time < stunnedUntil;
        /// <summary>Rooted (Net Shot, Bear Trap): cannot move, but still turns, shoots and swings.</summary>
        public bool IsRooted => Time.time < rootedUntil;
        /// <summary>Held or rooted: what talents mean by an immobilized enemy (paralysed, frozen, stunned or rooted).</summary>
        public bool IsImmobilized => IsHeld || IsRooted;
        public bool IsBleeding => bleedTicks > 0 || netBleeding;
        public bool IsPoisoned => poisonTicks > 0 || netPoisoned;
        /// <summary>Suffering any damage over time: burning, bleeding or poisoned.</summary>
        public bool HasDamageOverTime => IsBurning || IsBleeding || IsPoisoned;
        /// <summary>The most health this enemy has had: its starting health, since enemies never heal.</summary>
        public int PeakHealth => Mathf.Max(peakHealth, Health);
        public float HealthFraction => PeakHealth > 0 ? Health / (float)PeakHealth : 1f;
        /// <summary>Not yet hurt (Opening Strike).</summary>
        public bool IsUnhurt => Health >= PeakHealth;
        public static readonly Color StunnedTint = new Color(1f, 0.95f, 0.55f), RootedTint = new Color(0.7f, 0.6f, 0.4f);
        public static readonly Color BleedColor = new Color(0.85f, 0.08f, 0.12f), PoisonColor = new Color(0.45f, 0.95f, 0.3f);
        private float stunnedUntil, stunImmuneUntil, rootedUntil, nextBleed, nextPoison;
        private int bleedTicks, bleedDamage, poisonTicks, poisonDamage, peakHealth;
        private bool netBleeding, netPoisoned;
        /// <summary>
        /// Death Mark: every hit taken is remembered. If the enemy dies while marked, all of it bursts out onto every
        /// enemy around it; if it survives, it takes it all again when the mark comes due.
        /// </summary>
        public bool IsDeathMarked => deathMarkDue > 0f;
        public const float DeathMarkTime = 6f, DeathMarkBurstRadius = 2.5f;
        private float deathMarkDue;
        private int deathMarkStored;

        public void DeathMark(float delay)
        {
            if (IsInvulnerable || delay <= 0f) return;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.DeathMark, 0, transform.position, 0, delay); return; }
            deathMarkDue = Time.time + delay;
            deathMarkStored = 0;
        }

        /// <summary>Host: when the mark comes due, every hit it remembered lands again at once.</summary>
        private void SettleDeathMark()
        {
            if (deathMarkDue <= 0f || Time.time < deathMarkDue) return;
            deathMarkDue = 0f;
            int owed = deathMarkStored;
            deathMarkStored = 0;
            if (owed <= 0) return;
            HeroVfx.Pulse(Run.ProjectileRoot, transform.position, 1.1f, new Color(0.85f, 0.2f, 0.3f), 0.35f);
            HeroVfx.Sparks(Run.ProjectileRoot, transform.position, new Color(0.85f, 0.2f, 0.3f), 16, 5f, 0.35f);
            Hit(owed, transform.position, 0f);
        }

        /// <summary>Hunter's Mark: takes +1 damage from every hit.</summary>
        public bool IsMarked => Time.time < markedUntil;
        private float markedUntil;

        public void Mark(float duration)
        {
            if (IsInvulnerable || duration <= 0f) return;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Mark, 0, transform.position, 0, duration); }
            if (!IsMarked && Run.ProjectileRoot != null) CombatVfx.Ring(Run.ProjectileRoot, transform.position, 0.6f, new Color(1f, 0.3f, 0.3f), 0.4f);
            markedUntil = Mathf.Max(markedUntil, Time.time + duration);
        }
        /// <summary>Paralysed enemies cannot move, turn or attack (the Demoness's vital stabs and curses).</summary>
        public bool IsParalyzed => Time.time < paralyzedUntil;
        /// <summary>Cursed enemies take <see cref="CurseDamageMultiplier"/> times the damage from every hit.</summary>
        public bool IsCursed => Time.time < cursedUntil;
        public const float CurseDamageMultiplier = 1.5f;
        /// <summary>After a paralysis wears off, a guardian shrugs off new ones for this long.</summary>
        public const float BossParalysisImmunity = 5f;
        /// <summary>Guardians shake off crowd control: holds and chills last this fraction as long on them.</summary>
        public const float BossCrowdControlDuration = 0.35f;
        /// <summary>Guardians feel only this fraction of any slow's strength.</summary>
        public const float BossSlowResistance = 0.5f;
        public float ActionSpeedMultiplier => IsHeld ? 0f
            : Slowed((IsChilled ? 0.5f : 1f) * (HolyBubble.SlowsAt(transform.position) ? HolyBubble.SanctuarySlow : 1f) * (Time.time < terrorUntil ? TerrorSlow : 1f));

        /// <summary>A speed factor after the guardian's resistance: a 50% slow only slows a guardian by 25%.</summary>
        private float Slowed(float factor) => Boss != null ? 1f - (1f - factor) * BossSlowResistance : factor;
        public float MoveMultiplier => ActionSpeedMultiplier;
        // Attack timers follow local action time; status durations and damage-over-time use world time.
        public float ActionTime { get; private set; }
        /// <summary>
        /// How fast this enemy's attack clock runs: guardians fight at <see cref="DungeonBoss.AttackPace"/>, and deadlier
        /// worlds (<see cref="WorldDefinition.EnemyTempo"/>) speed every enemy up. Movement and telegraphs are unaffected.
        /// </summary>
        public float Tempo => (Boss != null ? DungeonBoss.AttackPace : 1f) * (Run != null ? Run.World.EnemyTempo : 1f);
        private float contactReadyAt;
        public bool IsFlashing => Time.time < hitUntil;
        private float chilledUntil, nextBurn, paralyzedUntil, paralysisImmuneUntil, cursedUntil, frozenUntil, freezeImmuneUntil;
        private SpriteRenderer curseIndicator;
        private int burnTicks, burnDamage;
        private Color burnColor = new Color(1f, 0.4f, 0.16f);
        public bool IsRanged => shooter != null;
        public EnemyFacing Facing { get; private set; }
        public EnemyHitRegion LastHitRegion { get; private set; }
        public event System.Action<EnemyHitRegion> HitReceived;

        private void Awake() { Facing = GetComponent<EnemyFacing>(); }

        private void Start()
        {
            body = GetComponent<SpriteRenderer>();
            shooter = GetComponent<EnemyShooter>();
            var world = Run.World;
            if (IsRanged && Variant == null) gameObject.name = world.CasterName;
            tactics = GetComponent<EnemyTactics>();
            if (Boss == null) body.sprite = Variant != null ? Variant.Sprite
                : world.HighTech ? NeonSprites.Enemy(IsRanged, IsTank) : DungeonVisuals.EnemySprite(IsRanged, IsTank);
            burnIndicator = DungeonVisuals.Create("Burn indicator", transform, transform.position,
                new Vector2(0.28f, 0.4f), burnColor, 9);
            burnIndicator.sprite = DungeonVisuals.FlameSprite;
            burnIndicator.transform.localPosition = new Vector2(0, 0.95f);
            burnIndicator.gameObject.SetActive(IsBurning);
            curseIndicator = DungeonVisuals.Create("Curse indicator", transform, transform.position,
                new Vector2(0.3f, 0.3f), DemonessAttack.Violet, 9);
            curseIndicator.transform.localPosition = new Vector2(0, -0.85f);
            curseIndicator.gameObject.SetActive(false);
        }

        /// <summary>A small violet sigil under a cursed enemy that spins while the curse holds.</summary>
        private void UpdateCurseIndicator()
        {
            if (curseIndicator == null) return;
            curseIndicator.gameObject.SetActive(IsCursed);
            if (!IsCursed) return;
            curseIndicator.transform.localRotation = Quaternion.Euler(0, 0, 45f + Time.time * 90f);
            curseIndicator.color = FlameMesh.Alpha(DemonessAttack.Violet, 0.6f + 0.3f * Mathf.Sin(Time.time * 8f));
        }

        private void Update()
        {
            if (!Run.IsPlaying || Health <= 0) return;
            if (Run.IsGuest) { GuestUpdate(); return; }
            ActionTime += Time.deltaTime * ActionSpeedMultiplier * DreadFactor() * Tempo;
            if (terrorPending && !IsParalyzed) { terrorPending = false; terrorUntil = Time.time + TerrorTime; }
            UpdateCurseIndicator();
            if (burnTicks > 0 && Time.time >= nextBurn)
            {
                burnTicks--;
                nextBurn = Time.time + 1f;
                Hit(burnDamage);
                if (Health <= 0) return;
                CombatVfx.Ring(Run.ProjectileRoot, transform.position, 0.4f, burnColor, 0.2f);
            }
            SettleDeathMark();
            if (Health <= 0) return;
            if (bleedTicks > 0 && Time.time >= nextBleed)
            {
                bleedTicks--;
                nextBleed = Time.time + 1f;
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, BleedColor, 5, 2f, 0.3f, Vector2.down, 90f, 0.8f);
                Hit(bleedDamage, transform.position, 0f);
                if (Health <= 0) return;
            }
            if (poisonTicks > 0 && Time.time >= nextPoison)
            {
                poisonTicks--;
                nextPoison = Time.time + 1f;
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, PoisonColor, 4, 1.4f, 0.4f, Vector2.up, 60f, 0.8f);
                Hit(poisonDamage, transform.position, 0f);
                if (Health <= 0) return;
            }
            if (burnIndicator != null)
            {
                burnIndicator.gameObject.SetActive(IsBurning);
                burnIndicator.color = burnColor;
                burnIndicator.transform.localScale = new Vector3(0.28f, 0.4f, 1f) * (1f + 0.12f * Mathf.Sin(Time.time * 12f));
            }
            if (Boss != null) return;
            if (IsHeld) { UpdateColor(); return; }
            Vector2 position = transform.position;
            // Heroes in Shadow Veil are invisible: with nobody to see, the enemy holds still and keeps its facing.
            if (!Run.TryNearestVisibleHero(position, out Vector2 target))
            {
                UpdateColor();
                if (!IsRanged) TryContactHit(HitRadius + 0.27f);
                return;
            }
            float distance = Vector2.Distance(position, target);
            bool visible = Run.HasLineOfSight(position, target);
            if (IsRooted)
            {
                if (!IsRanged || !shooter.IsCharging) Facing.TurnToward(target - position, Time.deltaTime * ActionSpeedMultiplier);
                UpdateColor();
                if (!IsRanged) TryContactHit(HitRadius + 0.27f);
                return;
            }
            if (Variant != null && Variant.Move(this, target, visible))
            {
                UpdateColor();
                if (!IsRanged) TryContactHit(HitRadius + 0.27f);
                return;
            }
            if (distance < 14f)
            {
                if (!IsRanged || !shooter.IsCharging) Facing.TurnToward(target - position, Time.deltaTime * ActionSpeedMultiplier);
                Vector2 direction = tactics.Direction(target, visible, IsRanged && shooter.IsCharging);
                Vector2 step = Run.Map.Move(position, direction * Speed * MoveMultiplier * Time.deltaTime, MoveRadius);
                if (!IceWall.BlocksEnemy(this, position, step)) transform.position = step;
            }
            UpdateColor();
            if (!IsRanged) TryContactHit(HitRadius + 0.27f);
        }

        private void UpdateColor()
        {
            body.color = IsFlashing || netFlashing || (((IsRanged && shooter.IsCharging) || (Variant != null && Variant.IsWindingUp)) && !IsHeld) ? Color.white
                : IsParalyzed ? DemonessAttack.ParalyzedTint(Time.time)
                : IsStunned ? Color.Lerp(StunnedTint, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 14f))
                : IsRooted ? RootedTint
                : IsFrozen ? FrozenTint
                : IsChilled ? AbilityCatalog.Ice : Variant != null ? Variant.Tint
                : IsTank ? Run.World.BruteTint : IsRanged ? Run.World.CasterTint : Run.World.BasicTint;
        }

        /// <summary>Co-op guest: follow the host's snapshots; only contact with the local hero is judged here.</summary>
        private void GuestUpdate()
        {
            ActionTime += Time.deltaTime * ActionSpeedMultiplier * Tempo;
            UpdateCurseIndicator();
            if (hasSnapshot)
                transform.position = Vector2.Distance(transform.position, netPosition) > 2.5f ? netPosition
                    : Vector2.Lerp(transform.position, netPosition, 1f - Mathf.Exp(-14f * Time.deltaTime));
            if (burnIndicator != null)
            {
                burnIndicator.gameObject.SetActive(IsBurning);
                burnIndicator.transform.localScale = new Vector3(0.28f, 0.4f, 1f) * (1f + 0.12f * Mathf.Sin(Time.time * 12f));
            }
            if (Boss != null) { if (Boss.DealsContactDamage) TryContactHit(Boss.ContactReach); return; }
            UpdateColor();
            if (!IsRanged) TryContactHit(HitRadius + 0.27f);
        }

        /// <summary>How long a guest trusts its own hits before the host's health overrides them (covers the round trip).</summary>
        private const float GuestPredictionWindow = 0.5f;
        private float lastLocalHitAt = -10f;
        private readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new System.Collections.Generic.List<Renderer>();

        /// <summary>
        /// Co-op guest: the host's view of this enemy. Fresh local hits are kept until the host has had time to apply
        /// them; after that the host's health wins. Without this, hits the host rejected (the boss was untouchable
        /// there, or not cursed) piled up here until the guest saw the boss "die" and vanish while it fought on.
        /// </summary>
        public void ApplySnapshot(EnemySnapshot snapshot)
        {
            netPosition = snapshot.Position;
            if (!hasSnapshot) transform.position = netPosition;
            hasSnapshot = true;
            Facing.Face(snapshot.Facing);
            if (Boss != null && (snapshot.MoreFlags & EnemySnapshot.HasMaxHealth) != 0) Boss.SyncMaxHealth(snapshot.MaxHealth);
            if (snapshot.Health > 0 && Time.time - lastLocalHitAt > GuestPredictionWindow)
            {
                // The host says it is still alive: bring back an enemy this guest wrongly thought it had killed.
                if (Health <= 0) SetVisible(true);
                Health = snapshot.Health;
            }
            else if (Health > 0) Health = Mathf.Max(1, Mathf.Min(Health, snapshot.Health));
            netFlashing = (snapshot.Flags & EnemySnapshot.Flashing) != 0;
            netBurning = (snapshot.Flags & EnemySnapshot.Burning) != 0;
            chilledUntil = (snapshot.Flags & EnemySnapshot.Chilled) != 0 ? Time.time + 0.25f : Mathf.Min(chilledUntil, Time.time);
            paralyzedUntil = (snapshot.MoreFlags & EnemySnapshot.Paralyzed) != 0 ? Time.time + 0.25f : Mathf.Min(paralyzedUntil, Time.time);
            cursedUntil = (snapshot.MoreFlags & EnemySnapshot.Cursed) != 0 ? Time.time + 0.25f : Mathf.Min(cursedUntil, Time.time);
            frozenUntil = (snapshot.MoreFlags & EnemySnapshot.Frozen) != 0 ? Time.time + 0.25f : Mathf.Min(frozenUntil, Time.time);
            stunnedUntil = (snapshot.MoreFlags & EnemySnapshot.Stunned) != 0 ? Time.time + 0.25f : Mathf.Min(stunnedUntil, Time.time);
            rootedUntil = (snapshot.MoreFlags & EnemySnapshot.Rooted) != 0 ? Time.time + 0.25f : Mathf.Min(rootedUntil, Time.time);
            netBleeding = (snapshot.MoreFlags & EnemySnapshot.Bleeding) != 0;
            netPoisoned = (snapshot.MoreFlags & EnemySnapshot.Poisoned) != 0;
            bool charging = (snapshot.Flags & EnemySnapshot.Charging) != 0;
            if (shooter != null) shooter.SetCharging(charging);
            if (Variant != null) Variant.SetNetWindup(charging && (shooter == null || !shooter.IsCharging));
            if (Boss != null) Boss.ApplySnapshot(charging, (byte)(snapshot.Flags >> EnemySnapshot.BossStateShift));
        }

        /// <summary>Contact damage against the local hero (each machine judges its own hero).</summary>
        public void TryContactHit(float reach)
        {
            if (!Run.IsPlaying || Health <= 0 || IsHeld || ActionTime < contactReadyAt || Run.Player.IsInvulnerable || Run.Player.IsIntangible || Run.Player.Health <= 0
                || Vector2.Distance(transform.position, Run.Player.transform.position) >= reach) return;
            Run.Player.Hit(Boss != null ? DungeonBoss.HitDamage : 1);
            contactReadyAt = ActionTime + 1f;
        }

        public void Hit(int damage)
        {
            Hit(damage, Run.Player.transform.position);
        }

        public void Hit(int damage, Vector2 source, float knockback = 1f)
        {
            if (Health <= 0) return;
            if (IsInvulnerable) { Boss.Deflect(source); return; }
            peakHealth = Mathf.Max(peakHealth, Health);
            if (IsMarked && damage > 0 && !Run.IsGuest) damage++;
            if (DebugMode.Enabled) damage = Mathf.Max(damage, Health);
            LastHitRegion = Facing.RegionFrom(source);
            HitReceived?.Invoke(LastHitRegion);
            if (Run.IsGuest)
            {
                // Show the hit now; the host applies it (and any curse) and confirms any kill.
                Run.Coop.ReportDamage(this, CoopDamageKind.Hit, damage, source, knockback: knockback);
                Health = Mathf.Max(0, Health - CursedDamage(damage));
                hitUntil = Time.time + 0.15f;
                lastLocalHitAt = Time.time;
                if (Health <= 0) SetVisible(false);
                return;
            }
            Health -= CursedDamage(damage);
            if (deathMarkDue > 0f) deathMarkStored += CursedDamage(damage);
            hitUntil = Time.time + 0.15f;
            if (Health <= 0)
            {
                if (Run.IsNetworked) Run.Coop.AnnounceKill(this);
                Die(Run.Coop == null || Run.Coop.IsLocalAttacker);
                return;
            }
            Vector2 away = ((Vector2)transform.position - source).normalized;
            if (Boss == null && knockback > 0f)
                transform.position = Run.Map.Move(transform.position, away * (IsTank ? 0.2f : 0.65f) * knockback, MoveRadius);
        }

        // Only renderers that were showing get restored, so indicators and telegraphs keep their own state.
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

        /// <summary>Host: a marked enemy fell, and every hit it took while marked bursts out onto the enemies around it.</summary>
        private void DeathMarkBurst()
        {
            int owed = deathMarkStored;
            deathMarkDue = 0f;
            deathMarkStored = 0;
            if (owed <= 0) return;
            Vector2 at = transform.position;
            foreach (var enemy in Run.Enemies.ToArray())
                if (enemy != null && enemy != this && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= DeathMarkBurstRadius + enemy.HitRadius)
                    enemy.Hit(owed, at, 1.5f);
        }

        /// <summary>Removes the enemy with its death effects and rewards; kill talents apply only to the killer.</summary>
        public void Die(bool localKill)
        {
            Health = Mathf.Min(Health, 0);
            if (!Run.IsGuest && deathMarkDue > 0f) DeathMarkBurst();
            Run.EnemyDefeated(this);
            if (localKill && Run.Player.Health > 0) Run.Player.Powerups.OnKill(Run.Player, this);
            Boss?.Defeated();
            if (Variant != null) Variant.OnDeath(this);
            // The Gambler collects a gold coin from every fallen enemy (each machine drops coins for its own hero).
            if (Run.Player != null && Run.Player.Weapon is GamblerAttack) GoldCoin.Drop(Run, transform.position);
            // The Reaper takes the souls of the soul-bound, and fear he has sown spreads from the fallen.
            if (Run.Player != null && Run.Player.Weapon is ReaperAttack reaper) reaper.OnEnemyDied(this, localKill);
            // Every fallen enemy leaves crystals for the shop before the next boss (each machine drops its own).
            if (Run.Player != null)
            {
                Crystal.Drop(Run, transform.position, Crystal.ValueFor(this));
                if (Run.Player.Powerups.RollExtraCrystals()) Crystal.Drop(Run, transform.position, Crystal.ValueFor(this));
            }
            CombatVfx.Ring(Run.ProjectileRoot, transform.position, Boss != null ? 1.6f : 0.45f, AbilityCatalog.Gold);
            if (Run.Player != null && Run.Player.ClassWeapon != WeaponType.Shadow)
            {
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, new Color(1f, 0.62f, 0.25f), Boss != null ? 28 : 12,
                    Boss != null ? 6f : 4.2f, Boss != null ? 0.6f : 0.4f);
                HeroVfx.Pulse(Run.ProjectileRoot, transform.position, Boss != null ? 2.4f : 0.8f, AbilityCatalog.Gold, Boss != null ? 0.6f : 0.3f);
            }
            if (Run.ProjectileRoot != null)
                DeathAnimation.Play(transform, Run.ProjectileRoot, Boss != null ? DeathAnimation.BossDuration : DeathAnimation.EnemyDuration);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public void Chill(float duration)
        {
            if (IsInvulnerable) return;
            TouchWithElement();
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Chill, 0, transform.position, 0, duration); return; }
            chilledUntil = Mathf.Max(chilledUntil, Time.time + duration * (Boss != null ? BossCrowdControlDuration : 1f));
        }

        /// <summary>Damage after the curse, rounded half up so even a 1-damage hit is worth more on a cursed enemy.</summary>
        public int CursedDamage(int damage) => IsCursed ? Mathf.FloorToInt(damage * CurseDamageMultiplier + 0.5f) : damage;

        public static readonly Color FrozenTint = new Color(0.72f, 0.93f, 1f);

        /// <summary>
        /// Holds the enemy in place. Guardians are held for a fraction of the time and then resist for a few seconds.
        /// False when nothing took hold (an untouchable or resisting guardian); a co-op guest assumes it lands.
        /// </summary>
        /// <param name="harmless">True when the hold must not set off the hero's damaging hold talents (Sow).</param>
        public bool Paralyze(float duration, bool lingering = false, bool harmless = false)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return false;
            bool fresh = !IsImmobilized;
            duration = HoldTime(duration);
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Paralyze, 0, transform.position, lingering ? 1 : 0, duration); Held(fresh, duration, harmless); return true; }
            // Lingering Terror: once this paralysis wears off, the enemy stays slowed for a while.
            if (lingering) terrorPending = true;
            if (Boss != null)
            {
                // Covers the paralysis itself too, so repeated stabs cannot chain-lock a guardian.
                if (Time.time < paralysisImmuneUntil) return false;
                duration *= BossCrowdControlDuration;
            }
            paralyzedUntil = Mathf.Max(paralyzedUntil, Time.time + duration);
            if (Boss != null) paralysisImmuneUntil = paralyzedUntil + BossParalysisImmunity;
            Held(fresh, duration, harmless);
            return true;
        }

        /// <summary>Ice freezes the enemy solid. Like paralysis, guardians thaw far faster and then resist for a while.</summary>
        public void Freeze(float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return;
            TouchWithElement();
            bool fresh = !IsImmobilized;
            duration = HoldTime(duration);
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Freeze, 0, transform.position, 0, duration); Held(fresh, duration); Seized(); return; }
            if (Boss != null)
            {
                if (Time.time < freezeImmuneUntil) return;
                duration *= BossCrowdControlDuration;
            }
            frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
            if (Boss != null) freezeImmuneUntil = frozenUntil + BossParalysisImmunity;
            if (Run.ProjectileRoot != null)
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.5f), 8, 2.6f, 0.3f);
            Held(fresh, duration);
            Seized();
        }

        /// <summary>
        /// Demonic Power: terror turns the enemy's back on <paramref name="from"/> and paralyses it on the spot.
        /// Returns whether the paralysis took hold.
        /// </summary>
        /// <param name="harmless">True when the fear must deal no damage of its own (Sow): the damaging hold talents stay quiet.</param>
        public bool Fear(Vector2 from, float duration, bool harmless = false)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return false;
            if (Run.IsGuest)
            {
                bool fresh = !IsImmobilized;
                duration = HoldTime(duration);
                Run.Coop.ReportDamage(this, CoopDamageKind.Fear, 0, from, 0, duration);
                fearedUntil = Mathf.Max(fearedUntil, Time.time + duration * (Boss != null ? BossCrowdControlDuration : 1f));
                Held(fresh, duration, harmless);
                return true;
            }
            Vector2 away = (Vector2)transform.position - from;
            if (away.sqrMagnitude > 0.0001f) Facing.Face(away.normalized);
            if (!Paralyze(duration, false, harmless)) return false;
            fearedUntil = paralyzedUntil;
            return true;
        }

        /// <summary>Afraid: paralysed by <see cref="Fear"/> rather than by any other hold (the Reaper's Reap and Sow look for this).</summary>
        public bool IsFeared => Time.time < fearedUntil;
        /// <summary>Seconds of fear still to run.</summary>
        public float FearRemaining => Mathf.Max(0f, fearedUntil - Time.time);
        private float fearedUntil;
        /// <summary>Fear Incarnate: this enemy gives up a soul when it dies.</summary>
        public bool SoulBound { get; set; }
        /// <summary>Sow: if above zero and the enemy dies afraid, fear of this many seconds spreads to the enemies around it.</summary>
        public float SownFear { get; set; }

        /// <summary>Reap: ends the fear (and the paralysis it holds the enemy with) now and says how long it still had to run.</summary>
        public float ConsumeFear()
        {
            float remaining = Mathf.Max(0f, fearedUntil - Time.time);
            fearedUntil = 0f;
            ConsumeParalysis();
            return remaining;
        }

        public const float TerrorSlow = 0.6f, TerrorTime = 2f, DreadRadius = 3f, DreadSlow = 0.75f;
        private bool terrorPending;
        private float terrorUntil;

        /// <summary>
        /// Nightmare Snap: ends every hold on the enemy now (paralysis, freeze, stun and root) and says how long the
        /// longest of them still had to run.
        /// </summary>
        public float ConsumeHolds()
        {
            float until = Mathf.Max(Mathf.Max(paralyzedUntil, frozenUntil), Mathf.Max(stunnedUntil, rootedUntil));
            float remaining = Mathf.Max(0f, until - Time.time);
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.ClearHolds, 0, transform.position, 0, 0f); return remaining; }
            paralyzedUntil = Mathf.Min(paralyzedUntil, Time.time);
            frozenUntil = Mathf.Min(frozenUntil, Time.time);
            stunnedUntil = Mathf.Min(stunnedUntil, Time.time);
            rootedUntil = Mathf.Min(rootedUntil, Time.time);
            return remaining;
        }

        /// <summary>Reap: ends the paralysis now and says how long it still had to run.</summary>
        public float ConsumeParalysis()
        {
            float remaining = Mathf.Max(0f, paralyzedUntil - Time.time);
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.ClearParalysis, 0, transform.position, 0, 0f); return remaining; }
            paralyzedUntil = Mathf.Min(paralyzedUntil, Time.time);
            return remaining;
        }

        /// <summary>Dread Aura: enemies near a Demoness who has it act (and so attack) 25% slower.</summary>
        private float DreadFactor()
        {
            var hero = Run.Player;
            return hero != null && hero.Health > 0 && hero.Powerups.Count(PowerupType.DreadAura) > 0
                && Vector2.Distance(transform.position, hero.transform.position) <= DreadRadius ? Slowed(DreadSlow) : 1f;
        }

        /// <summary>Whether the local hero inflicted this status, rather than the host applying a co-op guest's report.</summary>
        private bool FromLocalHero => Run.Player != null && Run.Player.Health > 0 && (Run.Coop == null || Run.Coop.IsLocalAttacker);

        /// <summary>Iron Grip lengthens holds the local hero inflicts; a guest's reports arrive already lengthened.</summary>
        private float HoldTime(float duration) => FromLocalHero ? duration * Run.Player.Powerups.HoldDurationMultiplier : duration;

        /// <summary>A hold took: if it caught the enemy moving freely, the local hero's hold talents go off.</summary>
        private void Held(bool fresh, float duration, bool harmless = false)
        {
            if (fresh && Health > 0 && FromLocalHero) Run.Player.Powerups.OnImmobilized(Run.Player, this, duration, harmless);
        }

        /// <summary>
        /// A freeze, stun or root from the local hero took hold: it feeds their class mechanic (the Demoness's Demonic
        /// Power). Her own paralyses are counted where she inflicts them, and fear never counts.
        /// </summary>
        private void Seized() { if (FromLocalHero) Run.Player.Mechanic?.OnImmobilized(); }

        /// <summary>Breaks the enemy out of its ice at once (Shatter).</summary>
        public void Thaw() { if (!Run.IsGuest) frozenUntil = Mathf.Min(frozenUntil, Time.time); }

        /// <summary>Stuns the enemy: held like paralysis. Guardians shake stuns off far faster and then resist for a while.</summary>
        public bool Stun(float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return false;
            bool fresh = !IsImmobilized;
            duration = HoldTime(duration);
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Stun, 0, transform.position, 0, duration); Held(fresh, duration); Seized(); return true; }
            if (Boss != null)
            {
                if (Time.time < stunImmuneUntil) return false;
                duration *= BossCrowdControlDuration;
            }
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
            if (Boss != null) stunImmuneUntil = stunnedUntil + BossParalysisImmunity;
            if (Run.ProjectileRoot != null) HeroVfx.Sparks(Run.ProjectileRoot, (Vector2)transform.position + Vector2.up * 0.5f, StunnedTint, 6, 1.6f, 0.4f);
            Held(fresh, duration);
            Seized();
            return true;
        }

        /// <summary>Roots the enemy in place: it can still turn and attack. Guardians are too massive to root.</summary>
        public bool Root(float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0 || Boss != null) return false;
            bool fresh = !IsImmobilized;
            duration = HoldTime(duration);
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Root, 0, transform.position, 0, duration); Held(fresh, duration); Seized(); return true; }
            rootedUntil = Mathf.Max(rootedUntil, Time.time + duration);
            Held(fresh, duration);
            Seized();
            return true;
        }

        /// <summary>Bleeding: <paramref name="damage"/> a second for <paramref name="ticks"/> seconds; a fresh wound tops up rather than stacks.</summary>
        public void Bleed(int ticks, int damage)
        {
            if (IsInvulnerable || ticks <= 0) return;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Bleed, damage, transform.position, ticks, 0f); return; }
            if (bleedTicks == 0) nextBleed = Time.time + 1f;
            bleedDamage = Mathf.Max(bleedTicks > 0 ? bleedDamage : 0, damage);
            bleedTicks = Mathf.Max(bleedTicks, ticks);
        }

        /// <summary>Poisoned: <paramref name="damage"/> a second for <paramref name="ticks"/> seconds; a fresh dose tops up rather than stacks.</summary>
        public void Poison(int ticks, int damage)
        {
            if (IsInvulnerable || ticks <= 0) return;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Poison, damage, transform.position, ticks, 0f); return; }
            if (poisonTicks == 0) nextPoison = Time.time + 1f;
            poisonDamage = Mathf.Max(poisonTicks > 0 ? poisonDamage : 0, damage);
            poisonTicks = Mathf.Max(poisonTicks, ticks);
        }

        public void Curse(float duration)
        {
            if (IsInvulnerable || duration <= 0f) return;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Curse, 0, transform.position, 0, duration); return; }
            cursedUntil = Mathf.Max(cursedUntil, Time.time + duration);
        }

        public void Burn(int ticks, int damage, Color? color = null)
        {
            if (IsInvulnerable) return;
            TouchWithElement();
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Burn, damage, transform.position, ticks, 0f, color); return; }
            if (burnTicks == 0)
            {
                nextBurn = Time.time + 1f;
                burnDamage = damage;
            }
            else burnDamage = Mathf.Max(burnDamage, damage);
            burnTicks = Mathf.Max(burnTicks, ticks);
            burnColor = color ?? new Color(1f, 0.4f, 0.16f);
            if (burnIndicator != null)
            {
                burnIndicator.color = burnColor;
                burnIndicator.gameObject.SetActive(IsBurning);
            }
        }
    }
}
