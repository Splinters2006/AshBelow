using UnityEngine;

namespace Slopgame
{
    public enum BossKind { AshWarden, Duelist, Archdemon }

    /// <summary>
    /// The arena guardian's shared state (health, title, invulnerability, co-op state). Its fighting style lives in
    /// a <see cref="BossBehaviour"/> component chosen by floor: the Ash Warden, the Ashen Duelist, then the Archdemon.
    /// </summary>
    [RequireComponent(typeof(DungeonEnemy))]
    public sealed class DungeonBoss : MonoBehaviour
    {
        public DungeonEnemy Enemy { get; private set; }
        public BossBehaviour Behaviour { get; private set; }
        public BossKind Kind { get; private set; }
        public int MaxHealth { get; private set; }
        public string Title => Behaviour.Title;
        public string Tell => Behaviour.Tell;
        public bool IsEnraged => Enemy.Health <= MaxHealth / 2;
        public bool IsCharging => Behaviour.IsCharging;
        public bool IsInvulnerable => Behaviour.IsInvulnerable;
        public float HitRadius => Behaviour.HitRadius;
        public bool DealsContactDamage => Behaviour.DealsContactDamage;
        public float ContactReach => Behaviour.ContactReach;
        /// <summary>Four bits of attack state mirrored to co-op guests.</summary>
        public byte NetState => (byte)(Behaviour.NetState & 0x0F);
        private SpriteRenderer body;
        private bool dropped;
        private float nextDeflect;

        /// <summary>Boss floors cycle Warden, Duelist, Archdemon (floors 5, 10, 15, then again from 20).</summary>
        public static BossKind KindForFloor(int floor) => (BossKind)(Mathf.Max(0, floor / 5 - 1) % 3);

        public void Initialize(DungeonRun run)
        {
            Enemy = GetComponent<DungeonEnemy>();
            Enemy.Run = run;
            Enemy.Boss = this;
            body = GetComponent<SpriteRenderer>();
            Kind = KindForFloor(run.Floor);
            Behaviour = Kind == BossKind.Duelist ? gameObject.AddComponent<DuelistBoss>()
                : Kind == BossKind.Archdemon ? (BossBehaviour)gameObject.AddComponent<ArchdemonBoss>()
                : gameObject.AddComponent<AshWardenBoss>();
            MaxHealth = run.EnemyHealthScaled(Behaviour.BaseHealth(run.Floor));
            Enemy.Health = MaxHealth;
            Behaviour.Setup(this);
            gameObject.name = Title;
        }

        private void Update()
        {
            if (!Enemy.Run.IsPlaying || Enemy.Health <= 0) return;
            body.color = Behaviour.BodyColor();
            Behaviour.VisualTick();
            if (Enemy.Run.IsGuest) return;
            Vector2 offset = Enemy.Run.NearestHero(transform.position) - (Vector2)transform.position;
            Behaviour.HostTick(offset);
            if (DealsContactDamage) Enemy.TryContactHit(ContactReach);
        }

        /// <summary>Co-op guest: mirror the host's attack state so tells, flight and invulnerability match.</summary>
        public void ApplySnapshot(bool isCharging, byte state) => Behaviour.ApplyNetState(isCharging, state);

        /// <summary>A blow glanced off while the boss is untouchable.</summary>
        public void Deflect(Vector2 source)
        {
            if (Time.time < nextDeflect) return;
            nextDeflect = Time.time + 0.12f;
            HeroVfx.Sparks(Enemy.Run.ProjectileRoot, transform.position, AbilityCatalog.Gold, 6, 3f, 0.25f, source - (Vector2)transform.position, 90f);
        }

        public void Defeated()
        {
            if (dropped) return;
            dropped = true;
            foreach (var bolt in Enemy.Run.ProjectileRoot.GetComponentsInChildren<EnemyProjectile>())
            {
                bolt.gameObject.SetActive(false);
                Destroy(bolt.gameObject);
            }
            foreach (var zone in Enemy.Run.ProjectileRoot.GetComponentsInChildren<HellfireZone>())
            {
                zone.gameObject.SetActive(false);
                Destroy(zone.gameObject);
            }
            Behaviour.OnDefeated();
            Enemy.Run.DropArtifact(Behaviour.GroundPosition);
        }
    }
}
