using UnityEngine;

namespace Slopgame
{
    /// <summary>A floating Arcane Spire eye that circles the hero at range, stopping only to fire a quick arcane bolt.</summary>
    public sealed class OrbitingEye : EnemyVariant
    {
        public const float OrbitRadius = 3.6f;
        private EnemyShooter shooter;
        private float orbitSign = 1f;
        private static Sprite sprite;
        public override string DisplayName => "Orbiting eye";
        public override Color Tint => new Color(0.8f, 0.75f, 1f);
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            enemy.name = DisplayName;
            enemy.Health += 1;
            enemy.Speed = Mathf.Min(4.5f, enemy.Speed * 1.1f);
            enemy.transform.localScale = Vector2.one * 0.58f;
            shooter = enemy.gameObject.AddComponent<EnemyShooter>();
            shooter.Windup = 0.35f;
            shooter.Recovery = 1.1f;
            shooter.Kind = BoltKind.Arcane;
            shooter.BoltSpeed = 9f;
        }

        public override bool Move(DungeonEnemy enemy, Vector2 target, bool visible)
        {
            Vector2 position = transform.position;
            Vector2 offset = position - target;
            float distance = offset.magnitude;
            if (!visible || distance > 9f || distance < 0.01f) return false;
            enemy.Facing.TurnToward(-offset, Time.deltaTime * enemy.ActionSpeedMultiplier * 2f);
            if (shooter.IsCharging) return true;
            // Sideways around the hero, easing in or out to hold the orbit; reverse when the way is blocked.
            Vector2 radial = offset / distance;
            Vector2 direction = (Vector2.Perpendicular(radial) * orbitSign + radial * Mathf.Clamp(OrbitRadius - distance, -1f, 1f)).normalized;
            Vector2 step = direction * enemy.Speed * enemy.MoveMultiplier * Time.deltaTime;
            Vector2 moved = enemy.Run.Map.Move(position, step, enemy.MoveRadius);
            if ((moved - position).sqrMagnitude < step.sqrMagnitude * 0.25f) orbitSign = -orbitSign;
            transform.position = moved;
            return true;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            "................", "................", ".....DDDDDD.....", "...DDLLWWWMDD...",
            "..DLWWWWWWWWMD..", ".DLWWWIIIIWWWMD.", ".DLWWIIPPIIWWMD.", ".DWWWIPPYPIWWMD.",
            ".DWWWIPPPPIWWMD.", ".DWWWIIPPIIWMMD.", ".DWWWWIIIIWWMMD.", "..DMWWWWWWWMMD..",
            "...DDMMMMMMDD...", "..R..DDDDDD..R..", ".R............R.", "................"
        }, new Color(0.15f, 0.12f, 0.3f), key => key switch
        {
            'I' => new Color(0.45f, 0.75f, 1f), 'P' => new Color(0.05f, 0.05f, 0.15f), 'R' => new Color(0.7f, 0.5f, 1f), 'Y' => Color.white, _ => Color.clear
        });
    }
}
