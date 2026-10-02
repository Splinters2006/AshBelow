using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Demoness's passive (Demonic Runes): an enemy that dies while immobilized may leave a rune behind, and so may
    /// every hit she lands on an immobilized guardian. Walking over it resets her Tail Sweep cooldown and makes every hit
    /// she lands paralyse for <see cref="BuffDuration"/> seconds. With her second passive (Rune Burst) picking one up also
    /// paralyses everything within <see cref="BurstRadius"/> units, walls or not. Each machine drops runes for its own hero.
    /// </summary>
    public sealed class DemonicRune : MonoBehaviour
    {
        public const float DropChance = 0.25f, GuardianHitChance = 0.25f, BuffDuration = 5f;
        public const float MagnetRadius = 10f, PickupRadius = 0.45f;
        /// <summary>Rune Burst: how far the paralysing blast reaches.</summary>
        public const float BurstRadius = 5f;
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
            // A paralysing blow that killed counts its victim as held too.
            if (player == null || player.Health <= 0 || !player.Permanent.HasPassive(WeaponType.Tail) || !enemy.CountsAsHeld
                || Random.value >= DropChance) return;
            Drop(run, enemy.transform.position);
        }

        /// <summary>The Demoness hit an immobilized guardian: a rune may shake loose beside it.</summary>
        public static void TryDropFromGuardian(DungeonPlayer player, DungeonEnemy guardian)
        {
            if (player == null || player.Health <= 0 || player.Run == null || player.Permanent == null || !player.Permanent.HasPassive(WeaponType.Tail)
                || guardian == null || guardian.Boss == null || Random.value >= GuardianHitChance) return;
            Drop(player.Run, guardian.Boss.Behaviour != null ? guardian.Boss.Behaviour.GroundPosition : (Vector2)guardian.transform.position);
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
            // Lodestone draws it in from farther, faster, and lets it be picked up from farther away.
            float reach = player.Powerups.PickupReach, magnet = MagnetRadius * reach;
            if (alive && distance <= magnet) rest = Vector2.MoveTowards(rest, hero, (12f + (magnet - distance) * 2f) * reach * Time.deltaTime);
            Place(rest, 0.12f + 0.07f * Mathf.Sin(Time.time * 3f + phase));
            if (!alive || Vector2.Distance(rest, hero) > PickupRadius * reach) return;
            player.Buffs.Rune(BuffDuration);
            if (player.Weapon is DemonessAttack tail)
            {
                tail.ResetSweepCooldown();
                if (player.Permanent.HasSecondPassive(WeaponType.Tail)) tail.RuneBurst(BurstRadius);
            }
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
