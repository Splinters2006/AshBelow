using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A gold coin dropped by a slain enemy. Only a Gambler sees them (each machine drops coins for its own hero).
    /// It pops out of the body with a little hop, spins and twinkles on the floor, and walking close draws it in and
    /// adds it to his purse.
    /// </summary>
    public sealed class GoldCoin : MonoBehaviour
    {
        public const float MagnetRadius = 1.8f, PickupRadius = 0.45f;
        private const float Size = 0.28f, HopTime = 0.35f, HopHeight = 0.45f;
        private DungeonRun run;
        private Vector2 rest, hopFrom;
        private float phase, age;
        private bool magnetised;
        private SpriteRenderer body, glint;
        private Transform shadow;

        public static GoldCoin Drop(DungeonRun run, Vector2 position)
        {
            if (run == null || run.ProjectileRoot == null) return null;
            var sprite = DungeonVisuals.CreateCoin("Gold coin", run.ProjectileRoot, position, Size, 5);
            var coin = sprite.gameObject.AddComponent<GoldCoin>();
            coin.run = run;
            coin.body = sprite;
            coin.hopFrom = position;
            // The coin lands a short way from the body so it does not hide under the corpse.
            Vector2 landing = position + Random.insideUnitCircle.normalized * Random.Range(0.25f, 0.5f);
            coin.rest = run.Map != null && !run.Map.CanStand(landing, 0.1f) ? position : landing;
            coin.phase = Random.value * Mathf.PI * 2f;

            // Children use world-space sizes, so they live beside the coin rather than under its spinning scale.
            var shadow = DungeonVisuals.Create("Gold coin shadow", run.ProjectileRoot, coin.rest, new Vector2(0.3f, 0.3f), Color.white, 4);
            shadow.sprite = DungeonVisuals.CoinShadow;
            coin.shadow = shadow.transform;
            coin.glint = DungeonVisuals.Create("Gold coin glint", run.ProjectileRoot, position, Vector2.one * 0.2f, new Color(1f, 1f, 0.9f, 0f), 7);
            coin.glint.sprite = DungeonVisuals.CoinGlint;
            HeroVfx.Sparks(run.ProjectileRoot, position, GamblerAttack.Gold, 6, 2.5f, 0.25f, Vector2.up, 120f, 0.7f);
            return coin;
        }

        private void Update()
        {
            if (run == null || !run.IsPlaying || run.Player == null) return;
            age += Time.deltaTime;
            var player = run.Player;
            var purse = player.Weapon as GamblerAttack;
            Vector2 hero = player.transform.position;

            float height;
            if (age < HopTime)
            {
                // Pop out in an arc, then settle.
                float t = age / HopTime;
                height = HopHeight * 4f * t * (1f - t);
                Place(Vector2.Lerp(hopFrom, rest, t), height, t);
                return;
            }

            float distance = Vector2.Distance(rest, hero);
            if (purse != null && player.Health > 0 && distance <= MagnetRadius)
            {
                if (!magnetised) { magnetised = true; CombatVfx.Trail(gameObject, new Color(1f, 0.82f, 0.3f, 0.7f), 0.12f, 0.12f); }
                rest = Vector2.MoveTowards(rest, hero, (7f - distance * 2f) * Time.deltaTime);
            }
            height = 0.08f + 0.06f * Mathf.Sin(Time.time * 4f + phase);
            Place(rest, height, 1f);

            // Every so often a twinkle crosses the coin's face.
            if (glint != null)
            {
                float cycle = Mathf.Repeat(Time.time * 0.7f + phase, 1f);
                float flash = cycle < 0.15f ? Mathf.Sin(cycle / 0.15f * Mathf.PI) : 0f;
                glint.color = new Color(1f, 1f, 0.9f, flash);
                glint.transform.position = (Vector2)transform.position + new Vector2(-0.05f, 0.05f);
                glint.transform.localScale = Vector3.one * (0.1f + 0.18f * flash);
                glint.transform.rotation = Quaternion.Euler(0f, 0f, flash * 45f);
            }

            if (purse == null || player.Health <= 0 || Vector2.Distance(rest, hero) > PickupRadius) return;
            int gained = purse.PickupCoinsForRoll(Random.value);
            purse.AddCoins(gained);
            player.Powerups.OnCoinsPicked(player, gained);
            var root = run.ProjectileRoot;
            HeroVfx.Pulse(root, rest, 0.5f, new Color(1f, 0.85f, 0.35f, 0.7f), 0.2f);
            HeroVfx.Sparks(root, rest, GamblerAttack.Gold, 8, 3f, 0.3f, Vector2.up, 150f, 0.8f);
            HeroVfx.Motes(root, hero, 0.35f, new Color(1f, 0.92f, 0.55f), 5, 0.6f);
            Collect();
        }

        /// <param name="shadowScale">0-1: how grown the shadow is (it swells as the coin lands).</param>
        private void Place(Vector2 ground, float height, float shadowScale)
        {
            transform.position = ground + Vector2.up * height;
            // A coin spins by squashing its width.
            float spin = Mathf.Abs(Mathf.Cos(Time.time * 3f + phase));
            transform.localScale = new Vector3(Size * (0.15f + 0.85f * spin), Size, 1f);
            if (body != null) body.color = Color.Lerp(new Color(0.85f, 0.75f, 0.55f), Color.white, spin);
            if (shadow != null)
            {
                shadow.position = ground + Vector2.down * 0.12f;
                float width = Mathf.Lerp(0.18f, 0.3f, shadowScale) * (1f - height * 0.6f);
                shadow.localScale = new Vector3(width, width * 0.6f, 1f);
            }
        }

        private void Collect()
        {
            if (shadow != null) Destroy(shadow.gameObject);
            if (glint != null) Destroy(glint.gameObject);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Floors clear the projectile root; take the shadow and twinkle along with the coin.
            if (shadow != null) Destroy(shadow.gameObject);
            if (glint != null) Destroy(glint.gameObject);
        }
    }
}
