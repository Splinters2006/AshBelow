using UnityEngine;

namespace Slopgame
{
    /// <summary>An Arcane Spire magus who fires tight arcane volleys and blinks away whenever it is struck (at most every few seconds).</summary>
    public sealed class BlinkMagus : EnemyVariant
    {
        public const float BlinkCooldown = 2.5f, BlinkDistance = 4.5f;
        public static readonly Color Arcane = new Color(0.55f, 0.65f, 1f);
        private DungeonEnemy owner;
        private float nextBlink;
        private static Sprite sprite;
        public override string DisplayName => "Blink magus";
        public override Color Tint => new Color(0.6f, 0.7f, 1f);
        public override int CrystalValue => 2;

        public override void Configure(DungeonEnemy enemy)
        {
            owner = enemy;
            enemy.name = DisplayName;
            enemy.Health += 2;
            enemy.transform.localScale = Vector2.one * 0.68f;
            var shooter = enemy.gameObject.AddComponent<EnemyShooter>();
            shooter.ProjectileCount = 3;
            shooter.SpreadDegrees = 14f;
            shooter.Windup = 0.5f;
            shooter.Recovery = 1.5f;
            shooter.Kind = BoltKind.Arcane;
            enemy.HitReceived += OnHit;
        }

        private void OnHit(EnemyHitRegion region)
        {
            // The host moves enemies; guests see the jump in the next snapshot.
            if (owner == null || owner.Run == null || owner.Run.IsGuest || owner.Health <= 0 || Time.time < nextBlink) return;
            if (!owner.Run.TryNearestVisibleHero(transform.position, out Vector2 hero)) hero = owner.Run.Player.transform.position;
            if (Blink(owner, hero, BlinkDistance, Arcane)) nextBlink = Time.time + BlinkCooldown;
        }

        private void OnDestroy()
        {
            if (owner != null) owner.HitReceived -= OnHit;
        }

        public override Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.ShadedSprite(DisplayName, new[]
        {
            ".......SS.......", "......DSSD......", ".....DLWWMD.....", "....DLWWWWMD....",
            "...DLWWWWWWMD...", "..DDDDDDDDDDDD..", "....DWKKKKWD....", "....DWWWWWMD....",
            "...DDWWWWWMDD...", "..DLWWDWWDWWMD..", "..DWWDWWWWDWMD..", "...DDWWSSWWDD...",
            "....DWWSSWMD....", "....DLWWWWMD....", "...DLWWMMWWMD...", "...DDDDDDDDDD..."
        }, new Color(0.1f, 0.1f, 0.25f), key => key switch
        {
            'K' => new Color(0.9f, 0.95f, 1f), 'S' => new Color(0.4f, 0.85f, 1f), _ => Color.clear
        });
    }
}
