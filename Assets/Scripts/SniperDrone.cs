using UnityEngine;

namespace Slopgame
{
    /// <summary>A Neon Arcology drone that hangs back, paints a laser sight across the room, then fires a plasma lance along it.</summary>
    public sealed class SniperDrone : EnemyVariant
    {
        public const float SightTime = 1f, Cooldown = 3.2f, MaxRange = 14f;
        private float readyAt, aimUntil;
        private static Sprite sprite;
        public override string DisplayName => "Sniper drone";
        public override Color Tint => new Color(0.6f, 0.65f, 0.85f);
        public override int CrystalValue => 2;
        protected override bool Winding => aimUntil > 0f;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 1;
            enemy.Speed *= 0.9f;
            enemy.transform.localScale = Vector2.one * 0.6f;
            readyAt = enemy.ActionTime + 1.5f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            // Holds perfectly still while the sight is painted, then the lance fires.
            if (aimUntil > 0f)
            {
                if (now < aimUntil) return true;
                aimUntil = 0f;
                readyAt = now + Cooldown;
                return true;
            }
            Vector2 position = transform.position;
            float distance = Vector2.Distance(position, target);
            if (visible && now >= readyAt && distance < 11f)
            {
                Vector2 direction = (target - position).normalized;
                enemy.Facing.Face(direction);
                Vector2 muzzle = position + direction * 0.3f;
                // The zone's own warning is the laser sight; the strike itself is a brief lance.
                HellfireZone.Spawn(enemy.Run, new HazardSpec { Shape = HazardShape.Beam, Style = HazardStyle.Plasma, Center = muzzle, Direction = direction,
                    Radius = Reach(enemy.Run.Map, muzzle, direction, MaxRange) + 0.3f, Width = 0.45f, Telegraph = SightTime, Duration = 0.2f });
                aimUntil = now + SightTime + 0.2f;
                return true;
            }
            Kite(enemy, target, visible, 5f, 9f);
            return true;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite(DisplayName, new[]
        {
            "................", "................", ".......DD.......", "..DDD..DD..DDD..",
            ".DWWWDDDDDDWWWD.", "..DDDDWWWWDDDD..", ".....DWWWWD.....", "....DWWCCWWD....",
            "....DWCLLCWD....", "....DWWCCWWD....", ".....DWWWWD.....", "......DWWD......",
            ".......DD.......", ".......CC.......", "................", "................"
        }, key => key switch
        {
            'W' => Color.white, 'D' => new Color(0.1f, 0.12f, 0.22f), 'C' => WorldCatalog.NeonPink, 'L' => Color.white, _ => Color.clear
        });
    }
}
