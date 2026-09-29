using UnityEngine;

namespace Slopgame
{
    /// <summary>A slow Ash-world caster with a longer windup and a three-ember fan.</summary>
    public sealed class EmberFanatic : EnemyVariant
    {
        private static Sprite sprite;
        public override string DisplayName => "Ember fanatic";
        public override Color Tint => new Color(1f, 0.48f, 0.24f);
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 2;
            enemy.Speed *= 0.75f;
            enemy.transform.localScale = Vector2.one * 0.72f;
            var shooter = enemy.gameObject.AddComponent<EnemyShooter>();
            shooter.ProjectileCount = 3;
            shooter.Windup = 0.85f;
            shooter.Recovery = 1.6f;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite(DisplayName, new[]
        {
            ".......FF.......", "......FFFF......", ".....FWWWWF.....", ".....FWWWWF.....",
            "....DDDDDDDD....", "...DDWWWWWWDD...", "...DWDDWWDDWD...", "....DWWWWWWD....",
            "..FFDDWWWWDDFF..", ".FFFFDWWWWDFFFF.", "..FFDDWWWWDDFF..", "....DWWWWWWD....",
            "...DDWWWWWWDD...", "..DDWWWWWWWWDD..", "..DDDDDDDDDDDD..", "................"
        }, key => key switch
        {
            'W' => Color.white, 'D' => new Color(0.22f, 0.12f, 0.16f),
            'F' => new Color(1f, 0.86f, 0.35f), _ => Color.clear
        });
    }
}
