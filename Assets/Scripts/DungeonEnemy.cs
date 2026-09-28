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
                transform.position = Run.Map.Move(position, direction * Speed * Time.deltaTime);
            }
            body.color = Time.time < hitUntil || (IsRanged && shooter.IsCharging) ? Color.white
                : IsRanged ? new Color(1f, 0.65f, 0.2f) : new Color(1f, 0.35f, 0.4f);
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
                Destroy(gameObject);
                return;
            }
            Vector2 away = ((Vector2)transform.position - (Vector2)Run.Player.transform.position).normalized;
            transform.position = Run.Map.Move(transform.position, away * 0.65f);
        }
    }
}
