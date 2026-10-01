using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// An Ash Below runner that squares up to the hero, then dashes straight through them in a streak of cinders,
    /// hitting anything in its path. It skids to a halt winded for a moment afterwards, open to a counter-attack.
    /// </summary>
    public sealed class CinderDasher : EnemyVariant
    {
        public const float Windup = 0.45f, DashTime = 0.24f, DashSpeed = 19f, Recover = 0.6f, Cooldown = 2.2f, Range = 6.5f;
        private float readyAt, phaseUntil, nextTrail;
        // 0: stalking, 1: winding up, 2: dashing, 3: winded.
        private int phase;
        private Vector2 direction;
        private static Sprite sprite;
        private static readonly Color Cinder = new Color(1f, 0.5f, 0.15f);
        public override string DisplayName => "Cinder dasher";
        public override Color Tint => new Color(0.75f, 0.6f, 0.55f);
        public override int CrystalValue => 2;
        protected override bool Winding => phase == 1;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Speed = Mathf.Min(4.4f, enemy.Speed * 1.1f);
            enemy.transform.localScale = Vector2.one * 0.62f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            var run = enemy.Run;
            if (phase == 0)
            {
                float distance = Vector2.Distance(transform.position, target);
                if (!visible || now < readyAt || distance > Range || distance < 1.2f) return false;
                direction = (target - (Vector2)transform.position).normalized;
                enemy.Facing.Face(direction);
                phase = 1;
                phaseUntil = now + Windup;
                HeroVfx.Pulse(run.ProjectileRoot, transform.position, 0.8f, Cinder, 0.3f);
                CoopFx.Pulse(run, transform.position, 0.8f, Cinder, 0.3f);
            }
            if (phase == 1)
            {
                if (now < phaseUntil) return true;
                phase = 2;
                phaseUntil = now + DashTime;
            }
            if (phase == 2)
            {
                if (now < phaseUntil)
                {
                    float travel = DashSpeed * Time.deltaTime * enemy.MoveMultiplier;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(travel / 0.08f));
                    for (int i = 0; i < steps; i++)
                    {
                        Vector2 next = (Vector2)transform.position + direction * (travel / steps);
                        if (!run.Map.CanStand(next, enemy.MoveRadius)) { phaseUntil = now; break; }
                        transform.position = next;
                        enemy.TryContactHit(enemy.HitRadius + 0.27f);
                    }
                    // Cinders streak out behind it.
                    if (Time.time >= nextTrail)
                    {
                        nextTrail = Time.time + 0.03f;
                        HeroVfx.Sparks(run.ProjectileRoot, transform.position, Cinder, 4, 2.5f, 0.3f, -direction, 50f);
                    }
                    return true;
                }
                phase = 3;
                phaseUntil = now + Recover;
                HeroVfx.Pulse(run.ProjectileRoot, transform.position, 0.7f, Cinder, 0.25f);
                CoopFx.Pulse(run, transform.position, 0.7f, Cinder, 0.25f);
            }
            if (now < phaseUntil) return true;
            phase = 0;
            readyAt = now + Cooldown;
            return false;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "................", "......DDDD......", ".....DLWWMD.....", "....DLWWWWMD....",
            "....DWYWWYMD....", "....DWWWWWMD....", "..RRRDMWWMD.....", ".RR.DDLWWMDD....",
            "...DLWWWWWWMD...", "..DWDDWWWWDDMD..", "..DD.DWWWWD.DD..", ".....DWWWMD.....",
            "....DWMDDWMD....", "...DWMD..DWMD...", "..DDDD....DDDD..", "................"
        }, new Color(0.14f, 0.08f, 0.06f), key => key switch
        {
            'Y' => new Color(1f, 0.85f, 0.3f), 'R' => new Color(1f, 0.4f, 0.1f), _ => Color.clear
        });
    }
}
