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
        /// <summary>The special kind of floor enemy this is (Ash skitter, Cinder husk), or null for the basic three.</summary>
        public EnemyVariant Variant => variant != null ? variant : variant = GetComponent<EnemyVariant>();
        private EnemyVariant variant;
        public DungeonBoss Boss { get; set; }
        public bool IsTank { get; set; }
        public float HitRadius => Boss != null ? Boss.HitRadius : IsTank ? 0.5f : 0.38f;
        /// <summary>True while a boss is out of reach (such as the Archdemon in flight); blows glance off.</summary>
        public bool IsInvulnerable => Boss != null && Boss.IsInvulnerable;
        public float MoveRadius => IsTank ? 0.42f : 0.28f;
        public bool IsBurning => burnTicks > 0 || netBurning;
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
        public bool IsHeld => IsParalyzed || IsFrozen;
        /// <summary>Paralysed enemies cannot move, turn or attack (the Demoness's vital stabs and curses).</summary>
        public bool IsParalyzed => Time.time < paralyzedUntil;
        /// <summary>Cursed enemies take <see cref="CurseDamageMultiplier"/> times the damage from every hit.</summary>
        public bool IsCursed => Time.time < cursedUntil;
        public const float CurseDamageMultiplier = 1.5f;
        /// <summary>After a paralysis wears off, a guardian shrugs off new ones for this long.</summary>
        public const float BossParalysisImmunity = 3f;
        public float ActionSpeedMultiplier => IsHeld ? 0f
            : (IsChilled ? 0.5f : 1f) * (HolyBubble.SlowsAt(transform.position) ? HolyBubble.SanctuarySlow : 1f);
        public float MoveMultiplier => ActionSpeedMultiplier;
        // Attack timers follow local action time; status durations and damage-over-time use world time.
        public float ActionTime { get; private set; }
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
            if (IsRanged) gameObject.name = world.CasterName;
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
            ActionTime += Time.deltaTime * ActionSpeedMultiplier;
            UpdateCurseIndicator();
            if (burnTicks > 0 && Time.time >= nextBurn)
            {
                burnTicks--;
                nextBurn = Time.time + 1f;
                Hit(burnDamage);
                if (Health <= 0) return;
                CombatVfx.Ring(Run.ProjectileRoot, transform.position, 0.4f, burnColor, 0.2f);
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
            if (distance < 10f)
            {
                if (!IsRanged || !shooter.IsCharging) Facing.TurnToward(target - position, Time.deltaTime * ActionSpeedMultiplier);
                Vector2 direction = tactics.Direction(target, visible, IsRanged && shooter.IsCharging);
                transform.position = Run.Map.Move(position, direction * Speed * MoveMultiplier * Time.deltaTime, MoveRadius);
            }
            UpdateColor();
            if (!IsRanged) TryContactHit(HitRadius + 0.27f);
        }

        private void UpdateColor()
        {
            body.color = IsFlashing || netFlashing || (IsRanged && shooter.IsCharging && !IsHeld) ? Color.white
                : IsParalyzed ? DemonessAttack.ParalyzedTint(Time.time)
                : IsFrozen ? FrozenTint
                : IsChilled ? AbilityCatalog.Ice : Variant != null ? Variant.Tint
                : IsTank ? Run.World.BruteTint : IsRanged ? Run.World.CasterTint : Run.World.BasicTint;
        }

        /// <summary>Co-op guest: follow the host's snapshots; only contact with the local hero is judged here.</summary>
        private void GuestUpdate()
        {
            ActionTime += Time.deltaTime * ActionSpeedMultiplier;
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
            bool charging = (snapshot.Flags & EnemySnapshot.Charging) != 0;
            if (shooter != null) shooter.SetCharging(charging);
            if (Boss != null) Boss.ApplySnapshot(charging, (byte)(snapshot.Flags >> EnemySnapshot.BossStateShift));
        }

        /// <summary>Contact damage against the local hero (each machine judges its own hero).</summary>
        public void TryContactHit(float reach)
        {
            if (!Run.IsPlaying || Health <= 0 || IsHeld || ActionTime < contactReadyAt || Run.Player.IsInvulnerable || Run.Player.Health <= 0
                || Vector2.Distance(transform.position, Run.Player.transform.position) >= reach) return;
            Run.Player.Hit();
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

        /// <summary>Removes the enemy with its death effects and rewards; kill talents apply only to the killer.</summary>
        public void Die(bool localKill)
        {
            Health = Mathf.Min(Health, 0);
            Run.EnemyDefeated(this);
            if (localKill && Run.Player.Health > 0) Run.Player.Powerups.OnKill(Run.Player, this);
            Boss?.Defeated();
            if (Variant != null) Variant.OnDeath(this);
            // The Gambler collects a gold coin from every fallen enemy (each machine drops coins for its own hero).
            if (Run.Player != null && Run.Player.Weapon is GamblerAttack) GoldCoin.Drop(Run, transform.position);
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
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public void Chill(float duration)
        {
            if (IsInvulnerable) return;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Chill, 0, transform.position, 0, duration); return; }
            chilledUntil = Mathf.Max(chilledUntil, Time.time + duration * (Boss != null ? 0.5f : 1f));
        }

        /// <summary>Damage after the curse, rounded half up so even a 1-damage hit is worth more on a cursed enemy.</summary>
        public int CursedDamage(int damage) => IsCursed ? Mathf.FloorToInt(damage * CurseDamageMultiplier + 0.5f) : damage;

        public static readonly Color FrozenTint = new Color(0.72f, 0.93f, 1f);

        /// <summary>
        /// Holds the enemy in place. Guardians are held half as long and then resist for a few seconds.
        /// False when nothing took hold (an untouchable or resisting guardian); a co-op guest assumes it lands.
        /// </summary>
        public bool Paralyze(float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return false;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Paralyze, 0, transform.position, 0, duration); return true; }
            if (Boss != null)
            {
                // Covers the paralysis itself too, so repeated stabs cannot chain-lock a guardian.
                if (Time.time < paralysisImmuneUntil) return false;
                duration *= 0.5f;
            }
            paralyzedUntil = Mathf.Max(paralyzedUntil, Time.time + duration);
            if (Boss != null) paralysisImmuneUntil = paralyzedUntil + BossParalysisImmunity;
            return true;
        }

        /// <summary>Ice freezes the enemy solid. Like paralysis, guardians thaw twice as fast and then resist for a while.</summary>
        public void Freeze(float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Freeze, 0, transform.position, 0, duration); return; }
            if (Boss != null)
            {
                if (Time.time < freezeImmuneUntil) return;
                duration *= 0.5f;
            }
            frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
            if (Boss != null) freezeImmuneUntil = frozenUntil + BossParalysisImmunity;
            if (Run.ProjectileRoot != null)
                HeroVfx.Sparks(Run.ProjectileRoot, transform.position, Color.Lerp(AbilityCatalog.Ice, Color.white, 0.5f), 8, 2.6f, 0.3f);
        }

        /// <summary>
        /// Demonic Power: terror turns the enemy's back on <paramref name="from"/> and paralyses it on the spot.
        /// Returns whether the paralysis took hold.
        /// </summary>
        public bool Fear(Vector2 from, float duration)
        {
            if (IsInvulnerable || duration <= 0f || Health <= 0) return false;
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Fear, 0, from, 0, duration); return true; }
            Vector2 away = (Vector2)transform.position - from;
            if (away.sqrMagnitude > 0.0001f) Facing.Face(away.normalized);
            return Paralyze(duration);
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
