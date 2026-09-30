using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A heart spilled from a smashed urn. It bobs on the floor until a wounded hero walks over it and restores
    /// <see cref="HealAmount"/> HP; a hero at full health leaves it lying for later.
    /// </summary>
    public sealed class HealthPickup : MonoBehaviour
    {
        public const int HealAmount = 1;
        public const float MagnetRadius = 1.5f, PickupRadius = 0.45f;
        private const float HopTime = 0.35f, HopHeight = 0.5f, Size = 0.4f;
        private static readonly Color HeartColor = new Color(1f, 0.3f, 0.38f);
        private DungeonRun run;
        private Vector2 rest, hopFrom;
        private float phase, age;
        private Transform shadow;

        public static HealthPickup Drop(DungeonRun run, Vector2 position)
        {
            if (run == null || run.ProjectileRoot == null) return null;
            var sprite = DungeonVisuals.Create("Heart", run.ProjectileRoot, position, Vector2.one * Size, Color.white, 5);
            sprite.sprite = DungeonVisuals.HeartSprite;
            var heart = sprite.gameObject.AddComponent<HealthPickup>();
            heart.run = run;
            heart.hopFrom = position;
            Vector2 landing = position + Random.insideUnitCircle.normalized * Random.Range(0.2f, 0.4f);
            heart.rest = run.Map != null && !run.Map.CanStand(landing, 0.1f) ? position : landing;
            heart.phase = Random.value * Mathf.PI * 2f;
            var shadow = DungeonVisuals.Create("Heart shadow", run.ProjectileRoot, heart.rest, new Vector2(0.3f, 0.18f), Color.white, 4);
            shadow.sprite = DungeonVisuals.CoinShadow;
            heart.shadow = shadow.transform;
            HeroVfx.Sparks(run.ProjectileRoot, position, HeartColor, 6, 2.5f, 0.25f, Vector2.up, 120f, 0.7f);
            return heart;
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
            bool wounded = player.Health > 0 && player.Health < player.MaxHealth;
            float distance = Vector2.Distance(rest, hero);
            if (wounded && distance <= MagnetRadius) rest = Vector2.MoveTowards(rest, hero, (6f - distance * 2f) * Time.deltaTime);
            Place(rest, 0.12f + 0.07f * Mathf.Sin(Time.time * 3f + phase));
            if (!wounded || Vector2.Distance(rest, hero) > PickupRadius) return;
            player.Heal(HealAmount);
            HeroVfx.Motes(run.ProjectileRoot, hero, 0.7f, HeartColor, 10, 0.8f);
            HeroVfx.Pulse(run.ProjectileRoot, rest, 0.5f, new Color(1f, 0.4f, 0.45f, 0.7f), 0.2f);
            Destroy(gameObject);
        }

        private void Place(Vector2 ground, float height)
        {
            transform.position = ground + Vector2.up * height;
            transform.localScale = Vector3.one * Size * (1f + 0.08f * Mathf.Sin(Time.time * 6f + phase));
            if (shadow == null) return;
            shadow.position = ground + Vector2.down * 0.14f;
            float width = 0.3f * (1f - height * 0.5f);
            shadow.localScale = new Vector3(width, width * 0.6f, 1f);
        }

        private void OnDestroy()
        {
            if (shadow != null) Destroy(shadow.gameObject);
        }
    }
}
