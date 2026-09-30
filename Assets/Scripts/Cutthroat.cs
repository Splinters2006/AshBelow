using UnityEngine;

namespace Slopgame
{
    /// <summary>A Shadow Market cutthroat that closes in, crouches, then lunges through a quick knife slash.</summary>
    public sealed class Cutthroat : EnemyVariant
    {
        public const float Windup = 0.4f, Cooldown = 1.8f, SlashReach = 2.4f, Lunge = 0.8f;
        private float readyAt, slashAt;
        private bool winding;
        private Vector2 aim;
        private static Sprite sprite;
        public override string DisplayName => "Cutthroat";
        public override Color Tint => new Color(0.6f, 0.75f, 0.7f);
        public override int CrystalValue => 2;
        protected override bool Winding => winding;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 1;
            enemy.Speed = Mathf.Min(4.6f, enemy.Speed * 1.15f);
            enemy.transform.localScale = Vector2.one * 0.62f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            if (winding)
            {
                if (now < slashAt) return true;
                winding = false;
                readyAt = now + Cooldown;
                Vector2 from = transform.position;
                HellfireZone.Spawn(enemy.Run, new HazardSpec { Shape = HazardShape.Beam, Style = HazardStyle.Steel, Center = from, Direction = aim,
                    Radius = Mathf.Min(SlashReach, Reach(enemy.Run.Map, from, aim, SlashReach) + 0.4f), Width = 1f, Telegraph = 0.25f, Duration = 0.15f });
                transform.position = enemy.Run.Map.Move(from, aim * Lunge, enemy.MoveRadius);
                return true;
            }
            if (!visible || now < readyAt || Vector2.Distance(transform.position, target) > 2.3f) return false;
            winding = true;
            slashAt = now + Windup;
            aim = (target - (Vector2)transform.position).normalized;
            enemy.Facing.Face(aim);
            return true;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "................", "......DDDD......", ".....DLWWMD.....", "....DLWWWWMD....",
            "....DKKKKKKD....", "....DKEKKEKD....", "....DWWWWWMD....", ".....DMWWMD.....",
            "...DDLWWWWMDD...", "..DLWDWWWWDWMD..", "..DWDDWWWWDDMDB.", "..DWD.DWWD.DMBB.",
            ".....DWWWWD.QB..", "....DWMDDWMD....", "....DWD..DMD....", "...DDD....DDD..."
        }, new Color(0.08f, 0.1f, 0.1f), key => key switch
        {
            'K' => new Color(0.06f, 0.06f, 0.08f), 'E' => new Color(0.9f, 1f, 0.9f), 'B' => new Color(0.75f, 0.95f, 1f), 'Q' => new Color(0.5f, 0.35f, 0.2f), _ => Color.clear
        });
    }
}
