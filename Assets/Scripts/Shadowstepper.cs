using UnityEngine;

namespace Slopgame
{
    /// <summary>A Shadow Market assassin that melts into smoke, steps out behind its target on the far side, and stabs before it can vanish again.</summary>
    public sealed class Shadowstepper : EnemyVariant
    {
        public const float Vanish = 0.5f, Cooldown = 3.2f, StabDelay = 0.3f, StabReach = 1.9f;
        public static readonly Color Smoke = new Color(0.3f, 0.95f, 0.65f);
        private float readyAt, phaseUntil;
        private int phase;
        private Vector2 aim;
        private static Sprite sprite;
        public override string DisplayName => "Shadowstepper";
        public override Color Tint => new Color(0.35f, 0.5f, 0.45f);
        public override int CrystalValue => 2;
        protected override bool Winding => phase == 1;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 1;
            enemy.transform.localScale = Vector2.one * 0.62f;
            readyAt = enemy.ActionTime + 1.5f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            Vector2 position = transform.position;
            if (phase == 0)
            {
                float distance = Vector2.Distance(position, target);
                if (!visible || now < readyAt || distance < 2.5f || distance > 9f) return false;
                phase = 1;
                phaseUntil = now + Vanish;
                HeroVfx.Motes(enemy.Run.ProjectileRoot, position, 0.6f, Smoke, 10, 0.6f);
                return true;
            }
            if (phase == 1)
            {
                if (now < phaseUntil) return true;
                // Out of the smoke on the far side of the target, so it strikes from behind.
                Vector2 through = (target - position).normalized;
                var run = enemy.Run;
                Vector2 behind = target + through * 1.4f;
                if (!run.Map.CanStand(behind, enemy.MoveRadius) || !run.HasLineOfSight(target, behind))
                {
                    if (!Blink(enemy, target, 1.4f, Smoke)) { phase = 0; readyAt = now + 1f; return false; }
                }
                else
                {
                    HeroVfx.Pulse(run.ProjectileRoot, position, 0.7f, Smoke, 0.3f);
                    CoopFx.Pulse(run, position, 0.7f, Smoke, 0.3f);
                    HeroVfx.Motes(run.ProjectileRoot, behind, 0.5f, Smoke, 8, 0.5f);
                    CoopFx.Pulse(run, behind, 0.8f, Smoke, 0.3f);
                    transform.position = behind;
                }
                aim = (target - (Vector2)transform.position).normalized;
                enemy.Facing.Face(aim);
                HellfireZone.Spawn(run, new HazardSpec { Shape = HazardShape.Beam, Style = HazardStyle.Steel, Center = transform.position, Direction = aim,
                    Radius = StabReach, Width = 0.9f, Telegraph = StabDelay, Duration = 0.15f });
                phase = 2;
                phaseUntil = now + StabDelay + 0.3f;
                return true;
            }
            if (now < phaseUntil) return true;
            phase = 0;
            readyAt = now + Cooldown;
            return false;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "................", "......DDDD......", ".....DLWWMD.....", "....DLWWWWMD....",
            "....DWDKKDMD....", "....DWGKKGMD....", "....DWWKKWMD....", "...DDWWWWWMDD...",
            "..DLWWDWWDWWMD..", ".DLWWDWWWWDWWMD.", ".DWWDDWWWWDDWBD.", "..DD.DWWWMD.DBD.",
            ".....DWWWMD..B..", "....DWMDDWMD....", "...DDDD..DDDD...", "................"
        }, new Color(0.04f, 0.07f, 0.06f), key => key switch
        {
            'K' => new Color(0.02f, 0.03f, 0.03f), 'G' => new Color(0.3f, 1f, 0.6f), 'B' => new Color(0.75f, 0.95f, 1f), _ => Color.clear
        });
    }
}
