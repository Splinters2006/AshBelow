using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Card Toss: a thrown playing card. Its suit decides what it does to the first enemy it strikes. Each card is a
    /// real little card (white face, dark border, its suit's pip in the middle and in two corners) haloed in its
    /// element's colour. It spins flat and flips edge-on as it flies, and on a hit the suit's pip pops out of the enemy.
    /// Teammates see the same cards fly; only the thrower's machine deals the damage.
    /// </summary>
    public sealed class ThrownCard : MonoBehaviour
    {
        public enum Suit { Hearts, Diamonds, Clubs, Spades }
        public const float Speed = 11f, Range = 7f, Spread = 12f, CardSize = 0.34f;
        private static readonly Sprite[] cardSprites = new Sprite[4], pipSprites = new Sprite[4];
        private DungeonRun run;
        private DungeonPlayer player;
        private Vector2 direction;
        private Suit suit;
        private int damage;
        private float remaining = Range, age, spinSpeed;
        private bool ghost;
        private Transform face;
        private SpriteRenderer glow;

        /// <summary>Hearts burn, diamonds freeze, clubs shock and spades paralyse.</summary>
        public static Color SuitColor(Suit suit) => suit == Suit.Hearts ? new Color(1f, 0.5f, 0.2f) : suit == Suit.Diamonds ? AbilityCatalog.Ice
            : suit == Suit.Clubs ? CombatDamage.ShockColor : DemonessAttack.Violet;

        private static readonly string[][] Pips =
        {
            new[] { ".SS.SS.", "SSSSSSS", "SSSSSSS", ".SSSSS.", "..SSS..", "...S...", "......." },
            new[] { "...S...", "..SSS..", ".SSSSS.", "SSSSSSS", ".SSSSS.", "..SSS..", "...S..." },
            new[] { "..SSS..", "..SSS..", "S.SSS.S", "SSSSSSS", "S..S..S", "...S...", "..SSS.." },
            new[] { "...S...", "..SSS..", ".SSSSS.", "SSSSSSS", "SSSSSSS", "...S...", "..SSS.." },
        };

        private static Color Ink(Suit suit) => suit == Suit.Hearts || suit == Suit.Diamonds ? new Color(0.82f, 0.1f, 0.14f) : new Color(0.1f, 0.1f, 0.14f);

        /// <summary>An 11x15 card: dark border, rounded corners, off-white face, a big pip and two small corner pips.</summary>
        internal static Sprite CardSprite(Suit suit)
        {
            int index = (int)suit;
            if (cardSprites[index] != null) return cardSprites[index];
            const int Width = 11, Height = 15;
            var rows = new char[Height][];
            for (int y = 0; y < Height; y++)
            {
                rows[y] = new char[Width];
                for (int x = 0; x < Width; x++)
                {
                    bool border = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                    bool corner = (x == 0 || x == Width - 1) && (y == 0 || y == Height - 1);
                    rows[y][x] = corner ? '.' : border ? 'K' : 'W';
                }
            }
            var pip = Pips[index];
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    if (pip[y][x] == 'S') rows[4 + y][2 + x] = 'S';
            // Corner marks: a tiny pip top-left and bottom-right.
            rows[1][1] = rows[2][1] = rows[1][2] = 'S';
            rows[Height - 2][Width - 2] = rows[Height - 3][Width - 2] = rows[Height - 2][Width - 3] = 'S';
            var lines = new string[Height];
            for (int y = 0; y < Height; y++) lines[y] = new string(rows[y]);
            Color ink = Ink(suit);
            return cardSprites[index] = DungeonVisuals.PaletteSprite("Card " + suit, lines, key => key switch
            {
                'K' => new Color(0.18f, 0.16f, 0.2f),
                'W' => new Color(0.98f, 0.96f, 0.9f),
                'S' => ink,
                _ => Color.clear
            });
        }

        /// <summary>The suit's pip on its own, in white so it can be tinted.</summary>
        internal static Sprite PipSprite(Suit suit)
        {
            int index = (int)suit;
            return pipSprites[index] != null ? pipSprites[index]
                : pipSprites[index] = DungeonVisuals.PaletteSprite("Pip " + suit, Pips[index], key => key == 'S' ? Color.white : Color.clear);
        }

        public static void Toss(DungeonPlayer player, Vector2 aim, int count, int damage)
        {
            var run = player.Run;
            Vector2 origin = player.transform.position;
            for (int i = 0; i < count; i++)
            {
                var suit = (Suit)Random.Range(0, 4);
                Vector2 direction = Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * Spread) * aim.normalized;
                var card = Create(run, origin, direction, suit);
                card.player = player;
                card.damage = damage;
                CoopFx.Card(run, origin, direction, (int)suit);
            }
            // A flick of the wrist: the fan of cards snaps out in a spray of glitter.
            HeroVfx.Sparks(run.ProjectileRoot, origin + aim.normalized * 0.3f, Color.white, 6, 3f, 0.18f, aim, 60f, 0.6f);
        }

        public static void SpawnGhost(DungeonRun run, Vector2 origin, Vector2 direction, Suit suit) => Create(run, origin, direction, suit).ghost = true;

        private static ThrownCard Create(DungeonRun run, Vector2 origin, Vector2 direction, Suit suit)
        {
            var holder = new GameObject("Playing card");
            holder.transform.SetParent(run.ProjectileRoot, false);
            holder.transform.position = origin;
            var card = holder.AddComponent<ThrownCard>();
            card.run = run;
            card.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            card.suit = suit;
            card.spinSpeed = Random.Range(900f, 1200f) * (Random.value < 0.5f ? -1f : 1f);
            Color color = SuitColor(suit);
            card.glow = DungeonVisuals.Create("Card glow", holder.transform, origin, Vector2.one * CardSize * 2.4f, FlameMesh.Alpha(color, 0.45f), 5);
            card.glow.sprite = DungeonVisuals.GlowSprite;
            var face = DungeonVisuals.Create("Card face", holder.transform, origin, Vector2.one * CardSize, Color.white, 6);
            face.sprite = CardSprite(suit);
            card.face = face.transform;
            CombatVfx.Trail(holder, FlameMesh.Alpha(color, 0.7f), 0.14f, 0.18f);
            return card;
        }

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null || (!ghost && player == null)) { Destroy(gameObject); return; }
            if (!run.IsPlaying) return;
            age += Time.deltaTime;
            // Spins flat like a thrown card and wobbles edge-on, flashing its face.
            face.rotation = Quaternion.Euler(0, 0, age * spinSpeed);
            float flip = Mathf.Cos(age * 14f);
            face.localScale = new Vector3(CardSize * (0.35f + 0.65f * Mathf.Abs(flip)), CardSize, 1f);
            glow.color = FlameMesh.Alpha(SuitColor(suit), 0.35f + 0.15f * Mathf.Sin(age * 30f));
            float distance = Mathf.Min(Speed * Time.deltaTime, remaining);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position + direction * (distance / steps);
                if (!run.Map.CanStand(next, 0.08f) || IceWall.StopsShot(transform.position, next, !ghost))
                {
                    HeroVfx.Sparks(run.ProjectileRoot, transform.position, Color.white, 5, 2.5f, 0.2f, -direction, 120f, 0.6f);
                    Destroy(gameObject);
                    return;
                }
                transform.position = next;
                foreach (var enemy in run.Enemies.ToArray())
                {
                    if (enemy == null || enemy.Health <= 0 || Vector2.Distance(next, enemy.transform.position) > enemy.HitRadius) continue;
                    Impact(next);
                    if (!ghost) Strike(enemy, next);
                    Destroy(gameObject);
                    return;
                }
            }
            remaining -= distance;
            if (remaining <= 0f)
            {
                HeroVfx.Sparks(run.ProjectileRoot, transform.position, SuitColor(suit), 4, 1.5f, 0.2f, null, 360f, 0.5f);
                Destroy(gameObject);
            }
        }

        /// <summary>The card slices in: a burst in its element's colour and its suit's pip popping up out of the hit.</summary>
        private void Impact(Vector2 at)
        {
            Color color = SuitColor(suit);
            HeroVfx.Sparks(run.ProjectileRoot, at, color, 9, 3.5f, 0.3f, direction, 120f, 0.9f);
            HeroVfx.Pulse(run.ProjectileRoot, at, 0.45f, FlameMesh.Alpha(color, 0.7f), 0.15f);
            var pip = DungeonVisuals.Create("Suit pop", run.ProjectileRoot, at + Vector2.up * 0.2f, Vector2.one * 0.35f, Color.Lerp(color, Color.white, 0.35f), 9);
            pip.sprite = PipSprite(suit);
            pip.gameObject.AddComponent<SuitPop>();
        }

        private void Strike(DungeonEnemy enemy, Vector2 at)
        {
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

        /// <summary>A pip that springs up out of a hit, swells and fades.</summary>
        internal sealed class SuitPop : MonoBehaviour
        {
            private const float Life = 0.45f;
            private float age;
            private Vector3 start, scale;
            private SpriteRenderer sprite;

            private void Start()
            {
                start = transform.position;
                scale = transform.localScale;
                sprite = GetComponent<SpriteRenderer>();
            }

            private void Update()
            {
                age += Time.deltaTime;
                float t = age / Life;
                transform.position = start + Vector3.up * 0.45f * (1f - (1f - t) * (1f - t));
                transform.localScale = scale * (0.6f + 0.6f * Mathf.Min(1f, t * 3f));
                var color = sprite.color;
                color.a = Mathf.Clamp01((1f - t) * 2f);
                sprite.color = color;
                if (age >= Life) Destroy(gameObject);
            }
        }
    }
}
