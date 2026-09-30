using UnityEngine;

namespace Slopgame
{
    /// <summary>A Shadow Market juggler that throws a wide fan of knives, then a second fan into the gaps of the first.</summary>
    public sealed class BladeJuggler : EnemyVariant
    {
        private static Sprite sprite;
        public override string DisplayName => "Blade juggler";
        public override Color Tint => new Color(0.55f, 1f, 0.75f);
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 1;
            enemy.transform.localScale = Vector2.one * 0.66f;
            var shooter = enemy.gameObject.AddComponent<EnemyShooter>();
            shooter.ProjectileCount = 5;
            shooter.SpreadDegrees = 16f;
            shooter.Windup = 0.6f;
            shooter.Recovery = 1.9f;
            shooter.Kind = BoltKind.Blade;
            shooter.BoltSpeed = 8f;
            shooter.ExtraVolleys = 1;
            shooter.VolleyGap = 0.3f;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "..B....B....B...", "...B...B...B....", "....DDDDDDDD....", "...DLWWWWWWMD...",
            "..DLWKKWWKKWMD..", "..DWWWWWWWWWMD..", "...DWWRRRRWMD...", "....DDWWWWDD....",
            ".B.DLWDWWDWMD.B.", "..DLWWDWWWDWWD..", "..DWD.DWWMD.DWD.", "......DWWMD.....",
            ".....DLWWWMD....", "....DLWDDDWMD...", "....DWD...DMD...", "...DDD....DDD..."
        }, new Color(0.06f, 0.12f, 0.1f), key => key switch
        {
            'K' => new Color(0.06f, 0.12f, 0.1f), 'B' => new Color(0.75f, 0.95f, 1f), 'R' => new Color(0.55f, 0.12f, 0.18f), _ => Color.clear
        });
    }
}
