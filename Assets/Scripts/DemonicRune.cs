using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Demoness's passive (Demonic Runes): an enemy that dies while immobilized may leave a rune behind. Walking over
    /// it makes every hit she lands paralyse for <see cref="BuffDuration"/> seconds. Each machine drops runes for its own hero.
    /// </summary>
    public sealed class DemonicRune : MonoBehaviour
    {
        public const float DropChance = 0.1f, BuffDuration = 5f;
        public const float MagnetRadius = 1.5f, PickupRadius = 0.45f;
        private const float HopTime = 0.35f, HopHeight = 0.5f, Size = 0.45f;
        private static Sprite runeSprite;
        private static Sprite RuneSprite => runeSprite != null ? runeSprite : runeSprite = DungeonVisuals.PaletteSprite("Demonic rune", new[]
        {
            "...OOO...", "..OVVVO..", ".OVWVWVO.", "OVVVWVVVO", "OVWWWWWVO", "OVVVWVVVO", ".OVWVWVO.", "..OVVVO..", "...OOO..."
        }, key => key switch
        {
            'O' => new Color(0.16f, 0.04f, 0.28f),
            'V' => DemonessAttack.Violet,
            'W' => new Color(0.95f, 0.8f, 1f),
            _ => Color.clear
        });
        private DungeonRun run;
        private Vector2 rest, hopFrom;
        private float phase, age;

        /// <summary>Rolls the drop for an enemy that just died; only a Demoness with the passive ever gets one.</summary>
        public static void TryDrop(DungeonRun run, DungeonEnemy enemy)
        {
            var player = run != null ? run.Player : null;
            if (player == null || player.Health <= 0 || !player.Permanent.HasPassive(WeaponType.Tail) || !enemy.IsImmobilized
                || Random.value >= DropChance) return;
            Drop(run, enemy.transform.position);
        }

        public static DemonicRune Drop(DungeonRun run, Vector2 position)
        {
            if (run == null || run.ProjectileRoot == null) return null;
            var sprite = DungeonVisuals.Create("Demonic rune", run.ProjectileRoot, position, Vector2.one * Size, Color.white, 5);
            sprite.sprite = RuneSprite;
            var rune = sprite.gameObject.AddComponent<DemonicRune>();
            rune.run = run;
            rune.hopFrom = position;
            Vector2 landing = position + Random.insideUnitCircle.normalized * Random.Range(0.2f, 0.4f);
            rune.rest = run.Map != null && (!run.Map.CanStand(landing, 0.1f) || run.Map.IsLava(landing)) ? position : landing;
            rune.phase = Random.value * Mathf.PI * 2f;
            HeroVfx.Sparks(run.ProjectileRoot, position, DemonessAttack.Violet, 8, 2.5f, 0.3f, Vector2.up, 120f, 0.7f);
            return rune;
        }

        private void Update()
        {
            if (run == null || !run.IsPlaying || run.Player == null) return;
            age += Time.deltaTime;
            var player = run.Player;
            Vector2 hero = player.transform.position;
            if (age < HopTime)
            {
                float t = age / HopTime;
                Place(Vector2.Lerp(hopFrom, rest, t), HopHeight * 4f * t * (1f - t));
                return;
            }
            bool alive = player.Health > 0;
            float distance = Vector2.Distance(rest, hero);
            if (alive && distance <= MagnetRadius) rest = Vector2.MoveTowards(rest, hero, (6f - distance * 2f) * Time.deltaTime);
            Place(rest, 0.12f + 0.07f * Mathf.Sin(Time.time * 3f + phase));
            if (!alive || Vector2.Distance(rest, hero) > PickupRadius) return;
            player.Buffs.Rune(BuffDuration);
            HeroVfx.Motes(run.ProjectileRoot, hero, 0.8f, DemonessAttack.Violet, 14, 0.9f);
            HeroVfx.Pulse(run.ProjectileRoot, hero, 1.2f, DemonessAttack.Violet, 0.35f);
            CoopFx.Pulse(run, hero, 1.2f, DemonessAttack.Violet, 0.35f);
            Destroy(gameObject);
        }

        private void Place(Vector2 ground, float height)
        {
            transform.position = ground + Vector2.up * height;
            transform.localScale = Vector3.one * Size * (1f + 0.1f * Mathf.Sin(Time.time * 6f + phase));
        }
    }
}
