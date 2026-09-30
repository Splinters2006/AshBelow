using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A crystal dropped by a slain enemy, spent at the crystal shop before each boss. Every machine drops crystals
    /// for its own hero. It pops out of the body, bobs and glimmers on the floor, and walking close draws it in.
    /// </summary>
    public sealed class Crystal : MonoBehaviour
    {
        public const float MagnetRadius = 2f, PickupRadius = 0.45f;
        private const float HopTime = 0.35f, HopHeight = 0.5f;
        private DungeonRun run;
        private Vector2 rest, hopFrom;
        private float phase, age, size;
        private bool magnetised;
        private SpriteRenderer body;
        private Transform shadow;
        public int Value { get; private set; }

        /// <summary>Crystals an enemy is worth: guardians shower the hero, iron brutes carry a few.</summary>
        public static int ValueFor(DungeonEnemy enemy) => enemy.Boss != null ? 15 : enemy.IsTank ? 3
            : enemy.Variant != null ? enemy.Variant.CrystalValue : 1;

        public static Crystal Drop(DungeonRun run, Vector2 position, int value)
        {
            if (run == null || run.ProjectileRoot == null || value <= 0) return null;
            float size = 0.26f + 0.05f * Mathf.Min(value, 6);
            var sprite = DungeonVisuals.Create("Crystal", run.ProjectileRoot, position, Vector2.one * size, Color.white, 5);
            sprite.sprite = DungeonVisuals.CrystalSprite;
            var crystal = sprite.gameObject.AddComponent<Crystal>();
            crystal.run = run;
            crystal.body = sprite;
            crystal.size = size;
            crystal.Value = value;
            crystal.hopFrom = position;
            Vector2 landing = position + Random.insideUnitCircle.normalized * Random.Range(0.25f, 0.5f);
            crystal.rest = run.Map != null && (!run.Map.CanStand(landing, 0.1f) || run.Map.IsLava(landing)) ? position : landing;
            crystal.phase = Random.value * Mathf.PI * 2f;
            var shadow = DungeonVisuals.Create("Crystal shadow", run.ProjectileRoot, crystal.rest, new Vector2(0.3f, 0.18f), Color.white, 4);
            shadow.sprite = DungeonVisuals.CoinShadow;
            crystal.shadow = shadow.transform;
            HeroVfx.Sparks(run.ProjectileRoot, position, CrystalPouch.CrystalColor, 5, 2.5f, 0.25f, Vector2.up, 120f, 0.7f);
            return crystal;
        }

        private void Update()
        {
            if (run == null || !run.IsPlaying || run.Player == null) return;
            age += Time.deltaTime;
            var player = run.Player;
            var pouch = player.Crystals;
            Vector2 hero = player.transform.position;
            if (age < HopTime)
            {
                float t = age / HopTime;
                Place(Vector2.Lerp(hopFrom, rest, t), HopHeight * 4f * t * (1f - t));
                return;
            }
            float distance = Vector2.Distance(rest, hero);
            bool alive = pouch != null && player.Health > 0;
            if (alive && distance <= MagnetRadius)
            {
                if (!magnetised) { magnetised = true; CombatVfx.Trail(gameObject, new Color(0.8f, 0.55f, 1f, 0.7f), 0.1f, 0.12f); }
                rest = Vector2.MoveTowards(rest, hero, (7f - distance * 2f) * Time.deltaTime);
            }
            Place(rest, 0.12f + 0.07f * Mathf.Sin(Time.time * 3f + phase));
            // A soft shimmer pulses through the facets.
            body.color = Color.Lerp(new Color(0.82f, 0.78f, 0.9f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 5f + phase));
            if (!alive || Vector2.Distance(rest, hero) > PickupRadius) return;
            pouch.Add(Value);
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, rest, 0.5f, new Color(0.78f, 0.5f, 1f, 0.7f), 0.2f);
            HeroVfx.Sparks(root, rest, CrystalPouch.CrystalColor, 8, 3f, 0.3f, Vector2.up, 150f, 0.8f);
            Destroy(gameObject);
        }

        private void Place(Vector2 ground, float height)
        {
            transform.position = ground + Vector2.up * height;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.7f + phase) * 10f);
            transform.localScale = Vector3.one * size;
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
