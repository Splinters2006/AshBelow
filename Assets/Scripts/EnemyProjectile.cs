using UnityEngine;

namespace Slopgame
{
    /// <summary>What an enemy bolt looks like; every kind behaves identically.</summary>
    public enum BoltKind : byte { Ember, Blade, Frost, Plasma, Hex, Arcane, Venom }

    public sealed class EnemyProjectile : MonoBehaviour
    {
        public const float DefaultSpeed = 7.5f;
        private DungeonRun run;
        private float speed = DefaultSpeed;
        public float Speed => speed;
        private Vector2 direction;
        public Vector2 Direction => direction;
        /// <summary>Co-op id assigned by the host; 0 outside co-op.</summary>
        public int Id { get; set; }
        private bool ghost;
        private float lifetime = 4f;
        private bool spent;
        public bool IsReflected { get; private set; }
        private int reflectedDamage;
        public bool IsSpent => spent;
        /// <summary>An ember bolt, a thrown steel blade, an icicle or a plasma shot.</summary>
        public BoltKind Kind { get; private set; }

        public static EnemyProjectile Spawn(DungeonRun run, Transform parent, Vector2 position, Vector2 direction) => Spawn(run, parent, position, direction, true);

        /// <summary>Every machine flies its own copy; a copy only ever strikes that machine's hero.</summary>
        public static EnemyProjectile Spawn(DungeonRun run, Transform parent, Vector2 position, Vector2 direction, bool announce,
            float speed = DefaultSpeed, BoltKind kind = BoltKind.Ember)
        {
            var sprite = kind == BoltKind.Blade ? DungeonVisuals.CreateThrownBlade(parent, position)
                : kind == BoltKind.Frost ? DungeonVisuals.CreateFrostBolt(parent, position)
                : kind == BoltKind.Plasma ? DungeonVisuals.CreatePlasmaBolt(parent, position)
                : kind == BoltKind.Hex || kind == BoltKind.Arcane || kind == BoltKind.Venom ? DungeonVisuals.CreateThemedBolt(parent, position, kind)
                : DungeonVisuals.CreateEmberBolt(parent, position);
            var projectile = sprite.gameObject.AddComponent<EnemyProjectile>();
            projectile.run = run;
            projectile.direction = direction.normalized;
            projectile.speed = speed;
            projectile.Kind = kind;
            projectile.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            if (kind == BoltKind.Blade) CombatVfx.Trail(projectile.gameObject, new Color(0.45f, 0.95f, 1f, 0.6f), 0.07f, 0.12f);
            else if (kind == BoltKind.Frost) CombatVfx.Trail(projectile.gameObject, new Color(0.7f, 0.92f, 1f, 0.55f), 0.08f, 0.14f);
            else if (kind == BoltKind.Plasma) CombatVfx.Trail(projectile.gameObject, new Color(1f, 0.35f, 0.9f, 0.55f), 0.08f, 0.14f);
            else if (kind == BoltKind.Hex) CombatVfx.Trail(projectile.gameObject, new Color(0.75f, 0.3f, 1f, 0.55f), 0.08f, 0.14f);
            else if (kind == BoltKind.Arcane) CombatVfx.Trail(projectile.gameObject, new Color(0.5f, 0.7f, 1f, 0.55f), 0.08f, 0.14f);
            else if (kind == BoltKind.Venom) CombatVfx.Trail(projectile.gameObject, new Color(0.5f, 1f, 0.3f, 0.5f), 0.1f, 0.16f);
            if (announce && run.IsNetworked && run.Coop.IsHost) run.Coop.AnnounceBolt(projectile, position, projectile.direction);
            return projectile;
        }

        private void Update() { Advance(Time.deltaTime); }

        public void Advance(float deltaTime)
        {
            if (spent || !run.IsPlaying) return;
            lifetime -= deltaTime;
            if (lifetime <= 0) { Consume(); return; }
            // Small steps prevent fast projectiles from skipping walls or the player.
            Vector2 movement = direction * speed * deltaTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(movement.magnitude / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + movement / steps;
                if (!run.Map.CanStand(next, 0.11f) || HolyBubble.Blocks(transform.position, next)) { Consume(); return; }
                transform.position = next;
                if (IsReflected)
                {
                    for (int j = run.Enemies.Count - 1; j >= 0; j--)
                    {
                        var enemy = run.Enemies[j];
                        if (Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                        // A teammate's reflection is only a picture here; their machine deals the damage.
                        if (!ghost) CombatDamage.Apply(run.Player, enemy, reflectedDamage, DamageElement.Physical, next - direction);
                        Consume();
                        return;
                    }
                    continue;
                }
                if (run.Player.Health <= 0) continue;
                // Shield Taunt stops bolts outright (no reflection); teammates see the bolt vanish too.
                if (run.Player.Mechanic is ShieldTaunt taunt && taunt.TryBlock(next, direction))
                {
                    if (run.IsNetworked) run.Coop.ReportBolt(this, CoopBoltEventKind.Consumed);
                    Consume();
                    return;
                }
                var shield = run.Player.Shield;
                if (shield != null && shield.CanReflect(next, direction))
                {
                    Reflect(next, -direction, false);
                    reflectedDamage = run.Player.Powerups.ReflectionDamage;
                    if (run.IsNetworked) run.Coop.ReportBolt(this, CoopBoltEventKind.Reflected);
                    return;
                }
                if (Vector2.Distance(next, run.Player.transform.position) <= 0.42f)
                {
                    run.Player.Hit();
                    if (run.IsNetworked) run.Coop.ReportBolt(this, CoopBoltEventKind.Consumed);
                    Consume();
                    return;
                }
            }
        }

        private void Reflect(Vector2 position, Vector2 newDirection, bool mirrored)
        {
            IsReflected = true;
            ghost = mirrored;
            transform.position = position;
            direction = newDirection.normalized;
            HeroVfx.Sparks(run.ProjectileRoot, position, new Color(0.55f, 0.85f, 1f), 9, 4.5f, 0.3f, direction, 100f);
            lifetime = 4f;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            GetComponent<SpriteRenderer>().color = new Color(0.55f, 0.85f, 1f);
        }

        /// <summary>A teammate's Knight turned this bolt; show it flying back without dealing damage here.</summary>
        public void MirrorReflection(Vector2 position, Vector2 newDirection) => Reflect(position, newDirection, true);

        public void Consume()
        {
            spent = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
