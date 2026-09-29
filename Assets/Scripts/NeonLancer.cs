using UnityEngine;

namespace Slopgame
{
    /// <summary>Locks a charge lane, flashes its warning, then dashes and recovers. The host owns movement.</summary>
    public sealed class NeonLancer : EnemyVariant
    {
        public const float Windup = 0.8f, DashDuration = 0.4f, DashSpeed = 10f, Recovery = 1.8f;
        private static Sprite sprite;
        private Vector2 direction;
        private float phaseUntil, nextWarning;
        private int phase;
        public bool IsWindingUp => phase == 1;
        public bool IsDashing => phase == 2;
        public override string DisplayName => "Neon lancer";
        public override Color Tint => WorldCatalog.Neon;
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 3;
            enemy.Speed *= 0.9f;
            enemy.transform.localScale = Vector2.one * 0.7f;
            phaseUntil = enemy.ActionTime + 1f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            float now = enemy.ActionTime;
            Vector2 position = transform.position;
            if (phase == 0)
            {
                float distance = Vector2.Distance(position, target);
                if (now < phaseUntil || !visible || distance < 2f || distance > 7f) return false;
                direction = (target - position).normalized;
                enemy.Facing.Face(direction);
                phase = 1;
                phaseUntil = now + Windup;
                nextWarning = now;
            }
            if (phase == 1)
            {
                if (now < phaseUntil)
                {
                    if (now >= nextWarning)
                    {
                        nextWarning = now + 0.15f;
                        Vector2 end = position;
                        // Match the charge's collision radius so the warning stops at walls.
                        for (float travel = 0.1f; travel <= DashSpeed * DashDuration; travel += 0.1f)
                        {
                            Vector2 point = position + direction * travel;
                            if (!enemy.Run.Map.CanStand(point, enemy.MoveRadius)) break;
                            end = point;
                        }
                        CombatVfx.Bolt(enemy.Run.ProjectileRoot, position, end, WorldCatalog.NeonPink, 0.08f, 0.2f);
                        CoopFx.Bolt(enemy.Run, position, end, WorldCatalog.NeonPink);
                    }
                    return true;
                }
                phase = 2;
                phaseUntil = now + DashDuration;
            }
            if (phase == 2)
            {
                if (now < phaseUntil)
                {
                    float distance = DashSpeed * Time.deltaTime * enemy.MoveMultiplier;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
                    for (int i = 0; i < steps; i++)
                    {
                        Vector2 next = (Vector2)transform.position + direction * (distance / steps);
                        if (!enemy.Run.Map.CanStand(next, enemy.MoveRadius)) { phaseUntil = now; break; }
                        transform.position = next;
                        enemy.TryContactHit(enemy.HitRadius + 0.27f);
                    }
                    return true;
                }
                phase = 3;
                phaseUntil = now + Recovery;
            }
            if (now < phaseUntil) return true;
            phase = 0;
            return false;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite(DisplayName, new[]
        {
            ".......WW.......", "......WWWW......", ".....DDDDDD.....", "....DWWWWWWD....",
            "....DCCCCCCD....", ".....DWWWWD.....", "..WWDDWWWWDDWW..", ".WWWDDWWWWDDWWW.",
            "..WWDWWWWWWDWW..", "....DDWWWWDD....", ".....DWWWWD.....", "....DDWDDWDD....",
            "...DWWD..DWWD...", "..DWWD....DWWD..", "..DDD......DDD..", "................"
        }, key => key switch
        {
            'W' => Color.white, 'D' => new Color(0.12f, 0.16f, 0.28f),
            'C' => WorldCatalog.NeonPink, _ => Color.clear
        });
    }
}
