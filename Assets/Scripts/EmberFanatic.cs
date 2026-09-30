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

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "......F..F......", ".....FOFFOF.....", "....FOOFFOOF....", "....DDDDDDDD....",
            "...DLWWWWWWMD...", "...DWEEWWEEMD...", "...DWWWKKWWMD...", "....DDWWWWDD....",
            "..FFDLWWWWMDFF..", ".FOOFDWWWWDFOOF.", "..FFDDWWWWDDFF..", "....DLWWWWMD....",
            "...DLWWWWWWMD...", "..DLWWMWWMWWMD..", "..DWWMWWWWMWMD..", "..DDDDDDDDDDDD.."
        }, new Color(0.22f, 0.12f, 0.16f), key => key switch
        {
            'F' => new Color(1f, 0.86f, 0.35f), 'O' => new Color(1f, 0.5f, 0.15f), 'E' => new Color(1f, 0.95f, 0.6f), 'K' => new Color(0.1f, 0.04f, 0.04f), _ => Color.clear
        });
    }
}
