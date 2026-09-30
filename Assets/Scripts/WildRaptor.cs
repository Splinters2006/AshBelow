using UnityEngine;

namespace Slopgame
{
    /// <summary>A Savage Wilds raptor that stalks in, then pounces in short, fast leaps that hit anything in its path.</summary>
    public sealed class WildRaptor : EnemyVariant
    {
        public const float Crouch = 0.3f, LeapTime = 0.28f, LeapSpeed = 12f, Cooldown = 1.3f;
        private float readyAt, phaseUntil;
        private int phase;
        private Vector2 direction;
        private static Sprite sprite;
        public override string DisplayName => "Wild raptor";
        public override Color Tint => new Color(0.85f, 0.65f, 0.4f);
        public override int CrystalValue => 2;
        protected override bool Winding => phase == 1;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Speed = Mathf.Min(4.8f, enemy.Speed * 1.2f);
            enemy.transform.localScale = Vector2.one * 0.6f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            if (phase == 0)
            {
                float distance = Vector2.Distance(transform.position, target);
                if (!visible || now < readyAt || distance > 5f || distance < 1f) return false;
                direction = (target - (Vector2)transform.position).normalized;
                enemy.Facing.Face(direction);
                phase = 1;
                phaseUntil = now + Crouch;
            }
            if (phase == 1)
            {
                if (now < phaseUntil) return true;
                phase = 2;
                phaseUntil = now + LeapTime;
            }
            if (now < phaseUntil)
            {
                float travel = LeapSpeed * Time.deltaTime * enemy.MoveMultiplier;
                int steps = Mathf.Max(1, Mathf.CeilToInt(travel / 0.08f));
                for (int i = 0; i < steps; i++)
                {
                    Vector2 next = (Vector2)transform.position + direction * (travel / steps);
                    if (!enemy.Run.Map.CanStand(next, enemy.MoveRadius)) { phaseUntil = now; break; }
                    transform.position = next;
                    enemy.TryContactHit(enemy.HitRadius + 0.27f);
                }
                return true;
            }
            phase = 0;
            readyAt = now + Cooldown;
            return false;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite(DisplayName, new[]
        {
            "................", "................", "..........DDDD..", ".........DWWWYD.",
            ".........DWWWWDD", "........DWWWDTT.", ".......DWWWD....", "..DDDDDWWWWD....",
            ".DWWWWWWWWWD....", "DWSWSWSWWWD.....", ".DD.DWWWWWD.....", "....DWWDWWD.....",
            "....DWD.DWD.....", "...DWWD.DWWD....", "...DDD..DDDD....", "................"
        }, key => key switch
        {
            'W' => Color.white, 'D' => new Color(0.2f, 0.12f, 0.06f), 'Y' => new Color(1f, 0.85f, 0.2f), 'S' => new Color(0.5f, 0.3f, 0.15f), 'T' => new Color(0.95f, 0.95f, 0.85f), _ => Color.clear
        });
    }
}
