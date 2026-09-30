using UnityEngine;

namespace Slopgame
{
    /// <summary>A swollen Savage Wilds toad that spits venom globs and bursts into a lingering toxic cloud when it dies.</summary>
    public sealed class BloatToad : EnemyVariant
    {
        public const float CloudRadius = 1.6f;
        private static Sprite sprite;
        public override string DisplayName => "Bloat toad";
        public override Color Tint => new Color(0.55f, 0.8f, 0.35f);
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 3;
            enemy.Speed *= 0.7f;
            enemy.transform.localScale = Vector2.one * 0.75f;
            var shooter = enemy.gameObject.AddComponent<EnemyShooter>();
            shooter.Windup = 0.6f;
            shooter.Recovery = 1.8f;
            shooter.Kind = BoltKind.Venom;
            shooter.BoltSpeed = 5.5f;
        }

        /// <summary>Every machine raises its own cloud as the toad dies; each judges only its own hero.</summary>
        public override void OnDeath(DungeonEnemy enemy)
        {
            if (enemy.Run == null || enemy.Run.ProjectileRoot == null) return;
            HellfireZone.Spawn(enemy.Run, new HazardSpec { Shape = HazardShape.Pool, Style = HazardStyle.Venom, Center = enemy.transform.position,
                Radius = CloudRadius, Telegraph = 0.5f, Duration = 2.5f }, false);
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite(DisplayName, new[]
        {
            "................", "................", "................", "...DD......DD...",
            "..DYYD....DYYD..", "..DYPD....DPYD..", ".DDWWDDDDDDWWDD.", "DWWWWWWWWWWWWWWD",
            "DWWSWWWWWWWWSWWD", "DWWWWWWSSWWWWWWD", ".DWWSWWWWWWSWWD.", "..DWWWBBBBWWWD..",
            ".DWWDDBBBBDDWWD.", "DWWD..DDDD..DWWD", "DDD..........DDD", "................"
        }, key => key switch
        {
            'W' => Color.white, 'D' => new Color(0.1f, 0.18f, 0.06f), 'Y' => new Color(1f, 0.85f, 0.2f), 'P' => new Color(0.05f, 0.05f, 0.02f), 'S' => new Color(0.8f, 0.95f, 0.3f), 'B' => new Color(0.9f, 0.9f, 0.7f), _ => Color.clear
        });
    }
}
