using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Augment's passive (Salvage): an enemy he lands the final hit on may drop a piece of scrap (twice as often
    /// with his second passive, Scavenger). Every <see cref="PerHeal"/> pieces he gathers repair <see cref="HealAmount"/>
    /// HP (see <see cref="CyborgAttack.CollectScrap"/>).
    /// </summary>
    public sealed class ScrapPickup : MonoBehaviour
    {
        public const float DropChance = 0.05f, ScavengerDropChance = 0.1f;
        public const int PerHeal = 3, HealAmount = 1;
        public const float MagnetRadius = 1.5f, PickupRadius = 0.45f;
        private const float HopTime = 0.35f, HopHeight = 0.5f, Size = 0.4f;
        public static readonly Color Metal = new Color(0.72f, 0.78f, 0.84f);
        private static Sprite scrapSprite;
        public static Sprite ScrapSprite => scrapSprite != null ? scrapSprite : scrapSprite = DungeonVisuals.PaletteSprite("Scrap", new[]
        {
            "..OOOO...", ".OLMMMO..", "OLMMOMMO.", "OMMOPOMMO", "OMOPPPOMO", "OMMOPOMDO", ".OMMOMDO.", "..OMDDO..", "...OOO..."
        }, key => key switch
        {
            'O' => new Color(0.12f, 0.15f, 0.2f),
            'L' => new Color(0.92f, 0.96f, 1f),
            'M' => Metal,
            'D' => new Color(0.45f, 0.5f, 0.58f),
            'P' => CyborgAttack.Plasma,
            _ => Color.clear
        });
        private DungeonRun run;
        private Vector2 rest, hopFrom;
        private float phase, age;

        /// <summary>Rolls the drop for an enemy the local hero just killed; only an Augment with the passive ever gets one.</summary>
        public static void TryDrop(DungeonRun run, DungeonEnemy enemy)
        {
            var player = run != null ? run.Player : null;
            if (player == null || player.Health <= 0 || !player.Permanent.HasPassive(WeaponType.Beam)) return;
            float chance = player.Permanent.HasSecondPassive(WeaponType.Beam) ? ScavengerDropChance : DropChance;
            if (Random.value >= chance) return;
            Drop(run, enemy.transform.position);
        }

        public static ScrapPickup Drop(DungeonRun run, Vector2 position)
        {
            if (run == null || run.ProjectileRoot == null) return null;
            var sprite = DungeonVisuals.Create("Scrap", run.ProjectileRoot, position, Vector2.one * Size, Color.white, 5);
            sprite.sprite = ScrapSprite;
            var scrap = sprite.gameObject.AddComponent<ScrapPickup>();
            scrap.run = run;
            scrap.hopFrom = position;
            Vector2 landing = position + Random.insideUnitCircle.normalized * Random.Range(0.2f, 0.4f);
            scrap.rest = run.Map != null && (!run.Map.CanStand(landing, 0.1f) || run.Map.IsLava(landing)) ? position : landing;
            scrap.phase = Random.value * Mathf.PI * 2f;
            HeroVfx.Sparks(run.ProjectileRoot, position, Metal, 6, 2.5f, 0.25f, Vector2.up, 120f, 0.7f);
            return scrap;
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
            // Lodestone: drawn in from farther, faster, and picked up from farther away.
            float reach = player.Powerups != null ? player.Powerups.PickupReach : 1f;
            if (alive && distance <= MagnetRadius * reach) rest = Vector2.MoveTowards(rest, hero, (6f - distance / reach * 2f) * reach * Time.deltaTime);
            Place(rest, 0.12f + 0.07f * Mathf.Sin(Time.time * 3f + phase));
            if (!alive || Vector2.Distance(rest, hero) > PickupRadius * reach || !(player.Weapon is CyborgAttack augment)) return;
            augment.CollectScrap();
            HeroVfx.Sparks(run.ProjectileRoot, hero, Metal, 6, 2.5f, 0.25f);
            Destroy(gameObject);
        }

        private void Place(Vector2 ground, float height)
        {
            transform.position = ground + Vector2.up * height;
            transform.localScale = Vector3.one * Size * (1f + 0.08f * Mathf.Sin(Time.time * 6f + phase));
        }
    }
}
