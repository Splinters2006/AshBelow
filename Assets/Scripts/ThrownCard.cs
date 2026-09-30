using UnityEngine;

namespace Slopgame
{
    /// <summary>Card Toss: a thrown playing card. Its suit decides what it does to the first enemy it strikes.</summary>
    public sealed class ThrownCard : MonoBehaviour
    {
        public enum Suit { Hearts, Diamonds, Clubs, Spades }
        public const float Speed = 11f, Range = 7f, Spread = 12f;
        private DungeonPlayer player;
        private Vector2 direction;
        private Suit suit;
        private int damage;
        private float remaining = Range;

        /// <summary>Hearts burn, diamonds freeze, clubs shock and spades paralyse.</summary>
        public static Color SuitColor(Suit suit) => suit == Suit.Hearts ? new Color(1f, 0.5f, 0.2f) : suit == Suit.Diamonds ? AbilityCatalog.Ice
            : suit == Suit.Clubs ? CombatDamage.ShockColor : DemonessAttack.Violet;

        public static void Toss(DungeonPlayer player, Vector2 aim, int count, int damage)
        {
            for (int i = 0; i < count; i++)
            {
                var suit = (Suit)Random.Range(0, 4);
                Vector2 direction = Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * Spread) * aim.normalized;
                var sprite = DungeonVisuals.Create("Playing card", player.Run.ProjectileRoot, player.transform.position, new Vector2(0.24f, 0.34f), Color.white, 6);
                var pip = DungeonVisuals.Create("Card suit", sprite.transform, player.transform.position, new Vector2(0.1f, 0.1f), SuitColor(suit), 7);
                pip.transform.localScale = new Vector3(0.45f, 0.32f, 1f);
                var card = sprite.gameObject.AddComponent<ThrownCard>();
                card.player = player;
                card.direction = direction;
                card.suit = suit;
                card.damage = damage;
                CombatVfx.Trail(sprite.gameObject, FlameMesh.Alpha(SuitColor(suit), 0.6f), 0.1f, 0.14f);
                CoopFx.Arrow(player.Run, player.transform.position, direction, Range);
            }
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            transform.rotation = Quaternion.Euler(0, 0, Time.time * 720f);
            float distance = Mathf.Min(Speed * Time.deltaTime, remaining);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f)) { Destroy(gameObject); return; }
                transform.position = next;
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    Strike(enemy, next);
                    Destroy(gameObject);
                    return;
                }
            }
            remaining -= distance;
            if (remaining <= 0f) Destroy(gameObject);
        }

        private void Strike(DungeonEnemy enemy, Vector2 at)
        {
            var run = player.Run;
            HeroVfx.Sparks(run.ProjectileRoot, at, SuitColor(suit), 8, 3.5f, 0.3f);
            CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, at - direction, 0.6f);
            if (enemy == null || enemy.Health <= 0) return;
            switch (suit)
            {
                case Suit.Hearts: enemy.Burn(CombatDamage.BurnTicks, CombatDamage.BurnTickDamage(damage)); break;
                case Suit.Diamonds: enemy.Freeze(CombatDamage.FreezeDuration); break;
                case Suit.Clubs: CombatDamage.Shock(player, at, enemy, damage); break;
                default: enemy.Paralyze(1f); break;
            }
        }
    }
}
