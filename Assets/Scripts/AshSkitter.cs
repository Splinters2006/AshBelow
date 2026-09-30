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

        private static Sprite AshSprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite("Ash skitter", new[]
        {
            "................", "................", "D..D........D..D", ".D..D.DDDD.D..D.",
            "..DD.DLWWMD.DD..", "....DLWWWWMD....", "D..DLWWWWWWMD..D", ".DDWEEWWWWEEWDD.",
            "...DWWKWWKWMD...", "D.DDWWWWWWWMDD.D", ".D..DMWWWWMD..D.", "..DD.DMMMMD.DD..",
            ".D....DDDD....D.", "D..............D", "................", "................"
        }, new Color(0.2f, 0.22f, 0.28f), key => key switch
        {
            'E' => new Color(1f, 0.9f, 0.45f), 'K' => new Color(0.1f, 0.04f, 0.04f), _ => Color.clear
        });
    }
}
