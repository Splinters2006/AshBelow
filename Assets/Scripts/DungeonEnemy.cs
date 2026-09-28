using UnityEngine;

namespace Slopgame
{
    [RequireComponent(typeof(EnemyFacing))]
    public sealed class DungeonEnemy : MonoBehaviour
    {
        public DungeonRun Run { get; set; }
        public int Health { get; set; }
        public float Speed { get; set; }
        private float hitUntil;
        private SpriteRenderer body;
        private EnemyShooter shooter;
        public DungeonBoss Boss { get; set; }
        public float HitRadius => Boss != null ? 0.85f : 0.38f;
        public float MoveMultiplier => Time.time < chilledUntil ? 0.45f : 1f;
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
        }

        private void Update()
        {
            if (!Run.IsPlaying || Health <= 0) return;
            if (burnTicks > 0 && Time.time >= nextBurn)
            {
                burnTicks--;
                nextBurn = Time.time + 1f;
                Hit(burnDamage);
                if (Health <= 0) return;
                CombatVfx.Ring(Run.ProjectileRoot, transform.position, 0.4f, burnColor, 0.2f);
            }
            if (Boss != null) return;
            Vector2 position = transform.position;
            Vector2 target = Run.Player.transform.position;
            float distance = Vector2.Distance(position, target);
            bool visible = Run.HasLineOfSight(position, target);
            if (distance < 10f)
            {
                if (!IsRanged || !shooter.IsCharging) Facing.TurnToward(target - position, Time.deltaTime);
                Vector2 direction = visible
                    ? (target - position).normalized : Run.DirectionToPlayer(position);
                if (IsRanged && visible)
                {
                    if (distance < 3f) direction = -direction;
                    else if (distance < 5.5f) direction = Vector2.zero;
                    if (shooter.IsCharging) direction = Vector2.zero;
                }
                transform.position = Run.Map.Move(position, direction * Speed * MoveMultiplier * Time.deltaTime);
            }
            body.color = Time.time < hitUntil || (IsRanged && shooter.IsCharging) ? Color.white
                : Time.time < chilledUntil ? AbilityCatalog.Ice : IsRanged ? new Color(1f, 0.65f, 0.2f) : new Color(1f, 0.35f, 0.4f);
            if (!IsRanged && Vector2.Distance(transform.position, target) < 0.65f) Run.Player.Hit();
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
                Run.Enemies.Remove(this);
                Run.Kills++;
                Run.Player.Powerups.OnKill(Run.Player);
                Boss?.Defeated();
                CombatVfx.Ring(Run.ProjectileRoot, transform.position, Boss != null ? 1.6f : 0.45f, AbilityCatalog.Gold);
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Vector2 away = ((Vector2)transform.position - (Vector2)Run.Player.transform.position).normalized;
            if (Boss == null) transform.position = Run.Map.Move(transform.position, away * 0.65f);
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
        }
    }
}
