using UnityEngine;

namespace Slopgame
{
    /// <summary>A small, fragile ember spider that scuttles in much faster than an ashling (a scuttle bot in the Neon Arcology).</summary>
    public sealed class AshSkitter : EnemyVariant
    {
        public const float SpeedMultiplier = 1.6f, MaxSpeed = 5.4f;
        private static Sprite sprite;

        public override string DisplayName => World.HighTech ? "Scuttle bot" : "Ash skitter";
        public override Color Tint => World.HighTech ? new Color(0.45f, 1f, 0.75f) : new Color(1f, 0.78f, 0.3f);

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health = Mathf.Max(1, enemy.Health / 2);
            enemy.Speed = Mathf.Min(MaxSpeed, enemy.Speed * SpeedMultiplier);
            enemy.transform.localScale = Vector2.one * 0.48f;
        }

        public override Sprite Sprite => World.HighTech ? NeonSprites.Scuttler : AshSprite;

        private static Sprite AshSprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite("Ash skitter", new[]
        {
            "................", "................", "W..............W", ".W...WWWWWW...W.", "..W.WWWWWWWW.W..",
            "...WWDWWWWDWW...", "W..WWWWWWWWWW..W", ".WWWWWWWWWWWWWW.", "...WWWWWWWWWW...", "W..WWWWWWWWWW..W",
            ".WW.WWWWWWWW.WW.", "...W.WWWWWW.W...", "..W..........W..", ".W............W.", "................", "................"
        }, key => key switch
        {
            'W' => Color.white,
            'D' => new Color(0.2f, 0.22f, 0.28f),
            _ => Color.clear
        });
    }
}
