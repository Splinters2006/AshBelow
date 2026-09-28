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
        public DungeonBoss Boss { get; set; }
        public bool IsTank { get; set; }
        public float HitRadius => Boss != null ? 0.85f : IsTank ? 0.5f : 0.38f;
        public float MoveRadius => IsTank ? 0.42f : 0.28f;
        public bool IsBurning => burnTicks > 0 || netBurning;
        /// <summary>Spawn-order id shared by every machine in a co-op run.</summary>
        public ushort NetId { get; set; }
        private Vector2 netPosition;
        private bool netBurning, netFlashing, hasSnapshot;
        private EnemyTactics tactics;
        private SpriteRenderer burnIndicator;
        public bool IsChilled => Time.time < chilledUntil;
        public float ActionSpeedMultiplier => IsChilled ? 0.5f : 1f;
        public float MoveMultiplier => ActionSpeedMultiplier;
        // Attack timers follow local action time; status durations and damage-over-time use world time.
        public float ActionTime { get; private set; }
        private float contactReadyAt;
        public bool IsFlashing => Time.time < hitUntil;
        private float chilledUntil, nextBurn;
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
            if (IsRanged) gameObject.name = "Ember caster";
            tactics = GetComponent<EnemyTactics>();
            if (Boss == null) body.sprite = DungeonVisuals.EnemySprite(IsRanged, IsTank);
            burnIndicator = DungeonVisuals.Create("Burn indicator", transform, transform.position,
                new Vector2(0.28f, 0.4f), burnColor, 9);
            burnIndicator.sprite = DungeonVisuals.FlameSprite;
            burnIndicator.transform.localPosition = new Vector2(0, 0.95f);
            burnIndicator.gameObject.SetActive(IsBurning);
        }

        private void Update()
        {
            if (!Run.IsPlaying || Health <= 0) return;
            if (Run.IsGuest) { GuestUpdate(); return; }
            ActionTime += Time.deltaTime * ActionSpeedMultiplier;
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
            Vector2 position = transform.position;
            Vector2 target = Run.NearestHero(position);
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
            body.color = IsFlashing || netFlashing || (IsRanged && shooter.IsCharging) ? Color.white
                : IsChilled ? AbilityCatalog.Ice : IsTank ? new Color(0.65f, 0.7f, 0.8f) : IsRanged ? new Color(1f, 0.65f, 0.2f) : new Color(1f, 0.35f, 0.4f);
        }

        /// <summary>Co-op guest: follow the host's snapshots; only contact with the local hero is judged here.</summary>
        private void GuestUpdate()
        {
            ActionTime += Time.deltaTime * ActionSpeedMultiplier;
            if (hasSnapshot)
                transform.position = Vector2.Distance(transform.position, netPosition) > 2.5f ? netPosition
                    : Vector2.Lerp(transform.position, netPosition, 1f - Mathf.Exp(-14f * Time.deltaTime));
            if (burnIndicator != null)
            {
                burnIndicator.gameObject.SetActive(IsBurning);
                burnIndicator.transform.localScale = new Vector3(0.28f, 0.4f, 1f) * (1f + 0.12f * Mathf.Sin(Time.time * 12f));
            }
            if (Boss != null) { TryContactHit(1.05f); return; }
            UpdateColor();
            if (!IsRanged) TryContactHit(HitRadius + 0.27f);
        }

        /// <summary>Co-op guest: the host's view of this enemy. Health only ever falls, so local hits are never undone.</summary>
        public void ApplySnapshot(EnemySnapshot snapshot)
        {
            netPosition = snapshot.Position;
            if (!hasSnapshot) transform.position = netPosition;
            hasSnapshot = true;
            Facing.Face(snapshot.Facing);
            if (Health > 0) Health = Mathf.Max(1, Mathf.Min(Health, snapshot.Health));
            netFlashing = (snapshot.Flags & EnemySnapshot.Flashing) != 0;
            netBurning = (snapshot.Flags & EnemySnapshot.Burning) != 0;
            chilledUntil = (snapshot.Flags & EnemySnapshot.Chilled) != 0 ? Time.time + 0.25f : Mathf.Min(chilledUntil, Time.time);
            bool charging = (snapshot.Flags & EnemySnapshot.Charging) != 0;
            if (shooter != null) shooter.SetCharging(charging);
            if (Boss != null) Boss.ApplySnapshot(charging, (snapshot.Flags & EnemySnapshot.PatternOdd) != 0);
        }

        /// <summary>Contact damage against the local hero (each machine judges its own hero).</summary>
        public void TryContactHit(float reach)
        {
            if (!Run.IsPlaying || Health <= 0 || ActionTime < contactReadyAt || Run.Player.IsInvulnerable || Run.Player.Health <= 0
                || Vector2.Distance(transform.position, Run.Player.transform.position) >= reach) return;
            Run.Player.Hit();
            contactReadyAt = ActionTime + 1f;
        }

        public void Hit(int damage)
        {
            Hit(damage, Run.Player.transform.position);
        }

        public void Hit(int damage, Vector2 source)
        {
            if (Health <= 0) return;
            if (DebugMode.Enabled) damage = Mathf.Max(damage, Health);
            LastHitRegion = Facing.RegionFrom(source);
            HitReceived?.Invoke(LastHitRegion);
            if (Run.IsGuest)
            {
                // Show the hit now; the host applies it and confirms any kill.
                Run.Coop.ReportDamage(this, CoopDamageKind.Hit, damage, source);
                Health = Mathf.Max(0, Health - damage);
                hitUntil = Time.time + 0.15f;
                if (Health <= 0) SetVisible(false);
                return;
            }
            Health -= damage;
            hitUntil = Time.time + 0.15f;
            if (Health <= 0)
            {
                if (Run.IsNetworked) Run.Coop.AnnounceKill(this);
                Die(Run.Coop == null || Run.Coop.IsLocalAttacker);
                return;
            }
            Vector2 away = ((Vector2)transform.position - source).normalized;
            if (Boss == null) transform.position = Run.Map.Move(transform.position, away * (IsTank ? 0.2f : 0.65f), MoveRadius);
        }

        private void SetVisible(bool value)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = value;
        }

        /// <summary>Removes the enemy with its death effects and rewards; kill talents apply only to the killer.</summary>
        public void Die(bool localKill)
        {
            Health = Mathf.Min(Health, 0);
            Run.EnemyDefeated(this);
            if (localKill && Run.Player.Health > 0) Run.Player.Powerups.OnKill(Run.Player);
            Boss?.Defeated();
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
            if (Run.IsGuest) { Run.Coop.ReportDamage(this, CoopDamageKind.Chill, 0, transform.position, 0, duration); return; }
            chilledUntil = Mathf.Max(chilledUntil, Time.time + duration * (Boss != null ? 0.5f : 1f));
        }

        public void Burn(int ticks, int damage, Color? color = null)
        {
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
