using UnityEngine;

namespace Slopgame
{
    /// <summary>An Ash Below bomber that keeps its distance and lobs firebombs onto the hero, leaving burning craters.</summary>
    public sealed class SootLobber : EnemyVariant
    {
        public const float Windup = 0.5f, Cooldown = 2.6f, CraterRadius = 1.1f;
        private float readyAt, throwAt;
        private bool winding;
        private Vector2 aimedAt;
        private static Sprite sprite;
        public override string DisplayName => "Soot lobber";
        public override Color Tint => new Color(0.75f, 0.6f, 0.5f);
        public override int CrystalValue => 2;
        protected override bool Winding => winding;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 1;
            enemy.Speed *= 0.85f;
            enemy.transform.localScale = Vector2.one * 0.66f;
            readyAt = enemy.ActionTime + 1.2f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            if (winding)
            {
                if (now < throwAt) return true;
                winding = false;
                readyAt = now + Cooldown;
                // The bomb arcs in as a falling meteor and leaves a burning crater.
                HellfireZone.Spawn(enemy.Run, new HazardSpec { Shape = HazardShape.Pool, Style = HazardStyle.Hellfire, Center = aimedAt,
                    Radius = CraterRadius, Telegraph = 0.9f, Duration = 2.4f });
                return true;
            }
            float distance = Vector2.Distance(transform.position, target);
            if (visible && now >= readyAt && distance < 8f && distance > 2f)
            {
                winding = true;
                throwAt = now + Windup;
                aimedAt = target;
                return true;
            }
            Kite(enemy, target, visible, 4f, 7f);
            return true;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite(DisplayName, new[]
        {
            "......DDDD......", ".....DWWWWD.....", "....DWDWWDWD....", "....DWWWWWWD....",
            ".....DWWWWD..FF.", "...DDDWWWWDDDBBF", "..DWWWDWWDWWWBB.", "..DWWWWWWWWWD...",
            "...DDWWWWWWD....", "....DWWWWWWD....", "....DWWDDWWD....", "...DWWD..DWWD...",
            "...DWD....DWD...", "..DDD......DDD..", "................", "................"
        }, key => key switch
        {
            'W' => Color.white, 'D' => new Color(0.16f, 0.12f, 0.12f), 'B' => new Color(0.25f, 0.22f, 0.22f), 'F' => new Color(1f, 0.6f, 0.15f), _ => Color.clear
        });
    }
}
