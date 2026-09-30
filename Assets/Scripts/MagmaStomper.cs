using UnityEngine;

namespace Slopgame
{
    /// <summary>A slow, heavy Ash Below brute that stomps when a hero comes close, sending a ring of fire rolling outward. Roll through it.</summary>
    public sealed class MagmaStomper : EnemyVariant
    {
        public const float Windup = 0.55f, Cooldown = 3f, ShockRadius = 3.2f;
        private float readyAt, stompAt;
        private bool winding;
        private static Sprite sprite;
        public override string DisplayName => "Magma stomper";
        public override Color Tint => new Color(0.55f, 0.3f, 0.25f);
        public override int CrystalValue => 3;
        protected override bool Winding => winding;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 4;
            enemy.Speed *= 0.7f;
            enemy.transform.localScale = Vector2.one * 0.85f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            if (winding)
            {
                if (now < stompAt) return true;
                winding = false;
                readyAt = now + Cooldown;
                ScreenFx.Shake(0.2f, 0.25f);
                HellfireZone.Spawn(enemy.Run, new HazardSpec { Shape = HazardShape.Ring, Style = HazardStyle.Hellfire, Center = transform.position,
                    Radius = ShockRadius, Width = 0.7f, Telegraph = 0.2f, Duration = 0.6f });
                return true;
            }
            if (!visible || now < readyAt || Vector2.Distance(transform.position, target) > 2.2f) return false;
            winding = true;
            stompAt = now + Windup;
            return true;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "................", "....DDDDDDDD....", "...DLLWWWWWMD...", "...DWYYWWYYMD...",
            "...DWWWWWWWMD...", ".DDDDWWOOWMDDDD.", "DLWWWDOOOODLWWMD", "DWWWDOOYYOODWWMD",
            "DWWMDOOYYOODWMMD", ".DDDWDOOOODWDDD.", "...DWWDOODWMD...", "...DLWWWWWWMD...",
            "..DLWWMD.DWWMD..", "..DWWMMD.DWMMD..", ".DDDDDDD.DDDDDD.", "................"
        }, new Color(0.14f, 0.1f, 0.1f), key => key switch
        {
            'O' => new Color(1f, 0.45f, 0.1f), 'Y' => new Color(1f, 0.9f, 0.5f), _ => Color.clear
        });
    }
}
