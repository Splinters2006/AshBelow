using UnityEngine;

namespace Slopgame
{
    /// <summary>An Infernal Court cultist who chants, then releases a full ring of hex bolts in every direction.</summary>
    public sealed class BrimstoneCultist : EnemyVariant
    {
        private static Sprite sprite;
        public override string DisplayName => "Brimstone cultist";
        public override Color Tint => new Color(0.75f, 0.35f, 0.95f);
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 2;
            enemy.Speed *= 0.8f;
            enemy.transform.localScale = Vector2.one * 0.7f;
            var shooter = enemy.gameObject.AddComponent<EnemyShooter>();
            shooter.ProjectileCount = 8;
            shooter.SpreadDegrees = 45f;
            shooter.Windup = 0.8f;
            shooter.Recovery = 2.2f;
            shooter.Kind = BoltKind.Hex;
            shooter.BoltSpeed = 6f;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite(DisplayName, new[]
        {
            ".......DD.......", "......DWWD......", ".....DWWWWD.....", "....DWWWWWWD....",
            "....DWKKKKWD....", "....DWKYYKWD....", "...DDWKKKKWDD...", "..DWWDWWWWDWWD..",
            ".DWWDWWSSWWDWWD.", "..DDWWSWWSWWDD..", "...DWWWSSWWWD...", "...DWWWWWWWWD...",
            "..DWWWWWWWWWWD..", "..DWWWWWWWWWWD..", "..DDDDDDDDDDDD..", "................"
        }, key => key switch
        {
            'W' => Color.white, 'D' => new Color(0.15f, 0.05f, 0.2f), 'K' => new Color(0.05f, 0.02f, 0.07f), 'Y' => new Color(1f, 0.4f, 0.3f), 'S' => new Color(1f, 0.6f, 0.2f), _ => Color.clear
        });
    }
}
