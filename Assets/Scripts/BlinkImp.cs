using UnityEngine;

namespace Slopgame
{
    /// <summary>An Infernal Court imp that flickers out and reappears right beside the hero, then claws before it can blink again.</summary>
    public sealed class BlinkImp : EnemyVariant
    {
        public const float Windup = 0.45f, Cooldown = 3.5f, Dazed = 0.35f;
        public static readonly Color Brimstone = new Color(1f, 0.35f, 0.3f);
        private float readyAt, blinkAt, dazedUntil;
        private bool winding;
        private static Sprite sprite;
        public override string DisplayName => "Blink imp";
        public override Color Tint => new Color(1f, 0.45f, 0.4f);
        public override int CrystalValue => 2;
        protected override bool Winding => winding;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Speed *= 1.1f;
            enemy.transform.localScale = Vector2.one * 0.55f;
            readyAt = enemy.ActionTime + 1f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            if (winding)
            {
                if (now < blinkAt) return true;
                winding = false;
                readyAt = now + Cooldown;
                // A brief daze after arriving gives the hero a beat to react.
                if (Blink(enemy, target, 1.4f, Brimstone)) dazedUntil = now + Dazed;
                return true;
            }
            if (now < dazedUntil) return true;
            float distance = Vector2.Distance(transform.position, target);
            if (!visible || now < readyAt || distance < 3f || distance > 9f) return false;
            winding = true;
            blinkAt = now + Windup;
            return true;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "................", "..H..........H..", "..HH........HH..", "...HHDDDDDDHH...",
            "...DLWWWWWWMD...", "..DLWYYWWYYWMD..", "..DWWWWWWWWWMD..", "...DWWKCCKWMD...",
            "....DDWWWWDD....", "..C.DLWWWWMD.C..", "..CDLWWWWWWMDC..", "....DWWWWWWMD...",
            "....DMWWWWMD.T..", ".....DWDDWD.T...", "....DWD..DWDT...", "....DD....DD...."
        }, new Color(0.2f, 0.05f, 0.08f), key => key switch
        {
            'H' => new Color(0.3f, 0.1f, 0.12f), 'Y' => new Color(1f, 0.9f, 0.3f), 'C' => new Color(0.95f, 0.9f, 0.85f), 'T' => new Color(0.35f, 0.08f, 0.1f), 'K' => new Color(0.08f, 0.02f, 0.02f), _ => Color.clear
        });
    }
}
