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
        public bool IsBurning => burnTicks > 0;
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
            Vector2 target = Run.Player.transform.position;
            float distance = Vector2.Distance(position, target);
            bool visible = Run.HasLineOfSight(position, target);
            if (distance < 10f)
            {
                if (!IsRanged || !shooter.IsCharging) Facing.TurnToward(target - position, Time.deltaTime * ActionSpeedMultiplier);
                Vector2 direction = tactics.Direction(target, visible, IsRanged && shooter.IsCharging);
                transform.position = Run.Map.Move(position, direction * Speed * MoveMultiplier * Time.deltaTime, MoveRadius);
            }
            body.color = Time.time < hitUntil || (IsRanged && shooter.IsCharging) ? Color.white
                : Time.time < chilledUntil ? AbilityCatalog.Ice : IsTank ? new Color(0.65f, 0.7f, 0.8f) : IsRanged ? new Color(1f, 0.65f, 0.2f) : new Color(1f, 0.35f, 0.4f);
            if (!IsRanged) TryContactHit(HitRadius + 0.27f);
        }

        public void TryContactHit(float reach)
        {
            if (!Run.IsPlaying || Health <= 0 || ActionTime < contactReadyAt || Run.Player.IsInvulnerable
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
            LastHitRegion = Facing.RegionFrom(source);
            HitReceived?.Invoke(LastHitRegion);
            Health -= damage;
            hitUntil = Time.time + 0.15f;
            if (Health <= 0)
            {
                Run.EnemyDefeated(this);
                Run.Player.Powerups.OnKill(Run.Player);
                Boss?.Defeated();
                CombatVfx.Ring(Run.ProjectileRoot, transform.position, Boss != null ? 1.6f : 0.45f, AbilityCatalog.Gold);
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Vector2 away = ((Vector2)transform.position - (Vector2)Run.Player.transform.position).normalized;
            if (Boss == null) transform.position = Run.Map.Move(transform.position, away * (IsTank ? 0.2f : 0.65f), MoveRadius);
        }

        public void Chill(float duration) { chilledUntil = Mathf.Max(chilledUntil, Time.time + duration * (Boss != null ? 0.5f : 1f)); }
        public void Burn(int ticks, int damage, Color? color = null)
        {
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
