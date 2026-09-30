using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A slow, hardy husk with a burning core. When it dies the core flares for a moment and then bursts,
    /// hurting other enemies caught nearby (never the heroes), so it is worth killing in a crowd.
    /// In the Neon Arcology it is a volatile core bot whose reactor blows the same way.
    /// </summary>
    public sealed class CinderHusk : EnemyVariant
    {
        public const float BurstRadius = 1.6f, FuseSeconds = 0.75f;
        public const int BurstEnemyDamage = 3;
        public static readonly Color Ember = new Color(1f, 0.42f, 0.12f);
        private static Sprite sprite;
        private SpriteRenderer body;

        public override string DisplayName => World.HighTech ? "Volatile core" : "Cinder husk";
        public override Color Tint => Color.Lerp(World.HighTech ? new Color(0.45f, 0.45f, 0.6f) : new Color(0.55f, 0.2f, 0.16f),
            World.HighTech ? WorldCatalog.NeonPink : Ember, 0.35f + 0.25f * Mathf.Sin(Time.time * 5f));
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 2;
            enemy.Speed *= 0.8f;
            enemy.transform.localScale = Vector2.one * 0.7f;
        }

        public override void OnDeath(DungeonEnemy enemy)
        {
            if (enemy.Run == null || enemy.Run.ProjectileRoot == null) return;
            // Parented to the floor, so a burst still fusing is cleared away with it on descent.
            var fuse = new GameObject("Cinder burst").AddComponent<CinderBurst>();
            fuse.transform.SetParent(enemy.transform.parent, false);
            fuse.Begin(enemy.Run, enemy.transform.position);
        }

        public override Sprite Sprite => World.HighTech ? NeonSprites.Core : AshSprite;

        private static Sprite AshSprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite("Cinder husk", new[]
        {
            "................", ".....DDDDDD.....", "....DLWWWWMD....", "...DLDDWWDDMD...",
            "...DWDYWWYDMD...", "..DDWWWWWWWMDD..", ".DLWDDEEEEDDWMD.", ".DWDDEECCEEDDMD.",
            ".DWDDEECCEEDDMD.", ".DWMDDEEEEDDMMD.", "..DDWWDDDDWMDD..", "...DLWWWWWWMD...",
            "...DWWD..DWMD...", "...DWMD..DMMD...", "...DDDD..DDDD...", "................"
        }, new Color(0.2f, 0.22f, 0.28f), key => key switch
        {
            'E' => new Color(1f, 0.55f, 0.2f), 'C' => new Color(1f, 0.92f, 0.6f), 'Y' => new Color(1f, 0.7f, 0.3f), _ => Color.clear
        });
    }

    /// <summary>The flare a dead cinder husk leaves behind: a warning ring, then a burst.</summary>
    public sealed class CinderBurst : MonoBehaviour
    {
        private DungeonRun run;
        private Vector2 center;
        private float burstAt, nextWarning;

        public void Begin(DungeonRun owner, Vector2 at)
        {
            run = owner;
            center = at;
            burstAt = Time.time + CinderHusk.FuseSeconds;
            nextWarning = Time.time;
        }

        private void Update()
        {
            if (run == null) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            if (Time.time < burstAt)
            {
                if (Time.time >= nextWarning)
                {
                    // The warning ring flashes faster as the core is about to go.
                    nextWarning = Time.time + 0.18f;
                    CombatVfx.Ring(run.ProjectileRoot, center, CinderHusk.BurstRadius, CinderHusk.Ember, 0.16f);
                    HeroVfx.Motes(run.ProjectileRoot, center, 0.4f, CinderHusk.Ember, 4, 0.4f);
                }
                return;
            }
            Detonate();
        }

        /// <summary>Bursts now (the fuse calls this; tests call it directly).</summary>
        public void Detonate()
        {
            Destroy(gameObject);
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, center, CinderHusk.BurstRadius, new Color(1f, 0.5f, 0.15f, 0.55f), 0.35f);
            HeroVfx.Sparks(root, center, CinderHusk.Ember, 18, 5f, 0.45f);
            // Only the host damages other enemies; guests learn of it from the host's snapshots.
            if (run.IsGuest) return;
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && enemy.Boss == null
                    && Vector2.Distance(enemy.transform.position, center) <= CinderHusk.BurstRadius + enemy.HitRadius)
                    enemy.Hit(CinderHusk.BurstEnemyDamage, center);
        }
    }
}
