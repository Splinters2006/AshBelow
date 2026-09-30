using UnityEngine;

namespace Slopgame
{
    /// <summary>A Neon Arcology crawler that chases the hero and drops arc mines behind it. Each mine arms, then bursts.</summary>
    public sealed class MineCrawler : EnemyVariant
    {
        public const float MineInterval = 2.2f, MineRadius = 0.9f;
        private float nextMine;
        private static Sprite sprite;
        public override string DisplayName => "Mine crawler";
        public override Color Tint => new Color(0.45f, 0.9f, 0.8f);
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 2;
            enemy.Speed *= 1.05f;
            enemy.transform.localScale = Vector2.one * 0.62f;
            nextMine = enemy.ActionTime + 1.5f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            // Mines only start dropping once it has a hero in sight, then it carries on chasing as normal.
            if (visible && enemy.ActionTime >= nextMine)
            {
                nextMine = enemy.ActionTime + MineInterval;
                HellfireZone.Spawn(enemy.Run, new HazardSpec { Shape = HazardShape.Pool, Style = HazardStyle.Circuit, Center = transform.position,
                    Radius = MineRadius, Telegraph = 1.4f, Duration = 0.35f });
            }
            return false;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "................", "................", "................", ".....DDDDDD.....",
            "...DDLWWWWMDD...", "..DLWWCCCCWWMD..", ".DLWWCCYYCCWWMD.", ".DWWWCCYYCCWWMD.",
            "..DWWMCCCCWMMD..", "...DDMMMMMMDD...", "..D.DD.DD.DD.D..", ".D..D..DD..D..D.",
            "D...D..DD..D...D", "................", "................", "................"
        }, new Color(0.08f, 0.14f, 0.18f), key => key switch
        {
            'C' => WorldCatalog.Neon, 'Y' => Color.white, _ => Color.clear
        });
    }
}
