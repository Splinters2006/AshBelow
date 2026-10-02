using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A soul torn loose by the Reaper's harvest (or left by a soul-bound enemy as it dies). Only a Reaper sees them.
    /// It hangs in the air where it was cut free and drifts to him from farther off than coins or crystals do.
    /// </summary>
    public sealed class SoulWisp : MonoBehaviour
    {
        /// <summary>Souls are drawn in from farther away than gold coins (1.8 units).</summary>
        public const float MagnetRadius = 3.6f, PickupRadius = 0.55f;
        private const float Size = 0.42f, RiseTime = 0.3f;
        private DungeonRun run;
        private Vector2 rest;
        private float phase, age;
        private bool magnetised, pulled;
        private SpriteRenderer body;
        private static Sprite sprite;

        public static Sprite Sprite => sprite != null ? sprite : sprite = DungeonVisuals.PaletteSprite("Soul", new[]
        {
            "...c...", "..cSc..", ".cSWSc.", ".cSWSc.", ".cSSSc.", "..cSc..", "...c..."
        }, key => key == 'W' ? Color.white : key == 'S' ? ReaperAttack.Soul : key == 'c' ? FlameMesh.Alpha(ReaperAttack.Soul, 0.4f) : Color.clear);

        public static SoulWisp Drop(DungeonRun run, Vector2 position)
        {
            if (run == null || run.ProjectileRoot == null) return null;
            var renderer = DungeonVisuals.Create("Soul", run.ProjectileRoot, position, Vector2.one * Size, Color.white, 6);
            renderer.sprite = Sprite;
            var wisp = renderer.gameObject.AddComponent<SoulWisp>();
            wisp.run = run;
            wisp.body = renderer;
            Vector2 landing = position + Random.insideUnitCircle * 0.35f;
            wisp.rest = run.Map != null && !run.Map.CanStand(landing, 0.1f) ? position : landing;
            wisp.phase = Random.value * Mathf.PI * 2f;
            HeroVfx.Sparks(run.ProjectileRoot, position, ReaperAttack.Soul, 6, 2.2f, 0.3f, Vector2.up, 100f, 0.7f);
            return wisp;
        }

        private void Update()
        {
            if (run == null || !run.IsPlaying || run.Player == null) return;
            age += Time.deltaTime;
            var player = run.Player;
            var reaper = player.Weapon as ReaperAttack;
            Vector2 hero = player.transform.position;
            float distance = Vector2.Distance(rest, hero);
            // Lodestone: drawn in from farther, faster, and picked up from farther away.
            float reach = player.Powerups != null ? player.Powerups.PickupReach : 1f, magnet = MagnetRadius * reach;
            if (reaper != null && player.Health > 0 && age >= RiseTime && (distance <= magnet || pulled))
            {
                if (!magnetised) { magnetised = true; CombatVfx.Trail(gameObject, FlameMesh.Alpha(ReaperAttack.Soul, 0.6f), 0.12f, 0.16f); }
                rest = Vector2.MoveTowards(rest, hero, (pulled ? Crystal.PullSpeed : (3f + (magnet - Mathf.Min(distance, magnet)) / reach * 2.5f) * reach) * Time.deltaTime);
            }
            // It wavers like a candle flame as it hangs there.
            float rise = Mathf.Clamp01(age / RiseTime);
            transform.position = rest + Vector2.up * (0.25f * rise + 0.07f * Mathf.Sin(Time.time * 3f + phase));
            float flicker = 0.9f + 0.1f * Mathf.Sin(Time.time * 9f + phase);
            transform.localScale = new Vector3(Size * flicker, Size * (2f - flicker), 1f) * rise;
            if (body != null) body.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.time * 5f + phase));
            if (reaper == null || player.Health <= 0 || Vector2.Distance(rest, hero) > PickupRadius * reach) return;
            reaper.AddSouls(1);
            HeroVfx.Pulse(run.ProjectileRoot, rest, 0.5f, FlameMesh.Alpha(ReaperAttack.Soul, 0.7f), 0.2f);
            HeroVfx.Motes(run.ProjectileRoot, hero, 0.35f, ReaperAttack.Soul, 5, 0.6f);
            Collect();
        }

        /// <summary>Wave worlds: once the wave is cleared the soul flies to the Reaper from anywhere in the arena.</summary>
        public void PullToHero() => pulled = true;

        /// <summary>Adds the soul straight to the Reaper's tally (a wave floor closing with souls still out).</summary>
        public void CollectNow()
        {
            if (run != null && run.Player != null && run.Player.Weapon is ReaperAttack reaper) reaper.AddSouls(1);
            Collect();
        }

        private void Collect()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
