using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Arrow Volley: real arrows rain out of the sky on a slant, each throwing a shadow ahead of it, onto a marked circle around the cursor, one after another. Each arrow
    /// strikes every enemy close to where it lands and carries the element loaded in the Elemental Quiver. Teammates
    /// see every arrow fall as a ghost arrow; only the caster's machine deals the damage.
    /// </summary>
    public sealed class ArrowRain : MonoBehaviour
    {
        public const float Range = 8f, Radius = 2.4f, Duration = 1.1f, ImpactRadius = 0.6f;
        public const int BaseArrows = 16, ArrowsPerRank = 4;
        private const float FallTime = 0.22f, FallHeight = 6f, ArrowHalf = 0.3f, StuckTime = 0.9f;
        private static readonly Vector2 Slant = new Vector2(0.28f, 1f).normalized;

        private sealed class Arrow
        {
            public Vector2 Landing;
            public float LandAt;
            public SpriteRenderer Sprite;
            public bool Landed;
        }

        private readonly List<Arrow> arrows = new List<Arrow>();
        private readonly Dictionary<DungeonEnemy, int> hitsOn = new Dictionary<DungeonEnemy, int>();
        private DungeonPlayer player;
        private Vector2 center;
        private int damage;
        // The volley's attack: every arrow of it counts together for Massacre.
        private int attack;
        private DamageElement infusion;
        private Color color;
        private float age, endsAt;
        private FlameMesh marker;

        public static ArrowRain Cast(DungeonPlayer player, Vector2 center, int count, int damage)
        {
            var run = player.Run;
            var rain = new GameObject("Arrow rain").AddComponent<ArrowRain>();
            rain.transform.SetParent(run.ProjectileRoot, false);
            rain.player = player;
            rain.center = center;
            rain.damage = damage;
            rain.attack = player.Powerups.ActiveAttack;
            rain.infusion = player.Mechanic is ElementalQuiver quiver ? quiver.Element : DamageElement.Physical;
            rain.color = rain.infusion != DamageElement.Physical
                ? Color.Lerp(CombatDamage.ElementColor(rain.infusion), Color.white, 0.25f) : new Color(0.95f, 1f, 0.65f);
            for (int i = 0; i < count; i++)
            {
                // Spread evenly through the rain, with a little jitter so it patters rather than ticks.
                // The first arrow always lands dead on the mark.
                float landAt = FallTime + Duration * (i + (i == 0 ? 0f : Random.Range(0f, 0.8f))) / count;
                Vector2 landing = i == 0 ? center : center + Random.insideUnitCircle * Radius;
                rain.arrows.Add(new Arrow { Landing = landing, LandAt = landAt });
                rain.endsAt = Mathf.Max(rain.endsAt, landAt);
            }
            rain.marker = new FlameMesh(rain.gameObject, 2);
            CoopFx.Ring(run, center, Radius, rain.color, FallTime + Duration);
            return rain;
        }

        public bool IsFinished => age >= endsAt + 0.25f;

        private void Update() => Advance(Time.deltaTime);

        public void Advance(float deltaTime)
        {
            var run = player != null ? player.Run : null;
            if (run == null || run.ProjectileRoot == null) { Destroy(gameObject); return; }
            if (!run.IsPlaying || IsFinished) return;
            age += deltaTime;
            foreach (var arrow in arrows)
            {
                if (arrow.Landed || age < arrow.LandAt - FallTime) continue;
                // Every arrow drops in on the same slant, as if loosed in one high arc from behind the Archer.
                Vector2 sky = arrow.Landing + Slant * FallHeight;
                if (arrow.Sprite == null)
                {
                    arrow.Sprite = DungeonVisuals.CreateArrow("Falling arrow", run.ProjectileRoot, sky, 0.7f, color, 7);
                    arrow.Sprite.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-Slant.y, -Slant.x) * Mathf.Rad2Deg);
                    CombatVfx.Trail(arrow.Sprite.gameObject, FlameMesh.Alpha(color, 0.6f), 0.06f, 0.08f);
                    CoopFx.Arrow(run, sky, -Slant, FallHeight, infusion);
                }
                float t = Mathf.Clamp01((age - (arrow.LandAt - FallTime)) / FallTime);
                arrow.Sprite.transform.position = Vector2.Lerp(sky, arrow.Landing, t * t) + Slant * ArrowHalf;
                if (t >= 1f) Land(run, arrow);
            }
            DrawMarker();
            if (IsFinished) Destroy(gameObject);
        }

        private void Land(DungeonRun run, Arrow arrow)
        {
            arrow.Landed = true;
            // The arrow sticks in the floor, head buried and fletching up, for a moment before fading.
            var stuck = arrow.Sprite.transform;
            stuck.localScale *= 0.8f;
            stuck.position = arrow.Landing + Slant * ArrowHalf * 0.8f;
            var trail = arrow.Sprite.GetComponent<TrailRenderer>();
            if (trail != null) trail.emitting = false;
            arrow.Sprite.gameObject.AddComponent<FadingSprite>().Duration = StuckTime;
            HeroVfx.Sparks(run.ProjectileRoot, arrow.Landing, new Color(0.75f, 0.7f, 0.6f, 0.7f), 5, 2f, 0.22f, Vector2.up, 160f, 0.6f);
            HeroVfx.Pulse(run.ProjectileRoot, arrow.Landing, 0.3f, FlameMesh.Alpha(color, 0.5f), 0.15f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(arrow.Landing, enemy.transform.position) <= ImpactRadius + enemy.HitRadius)
                {
                    // Storm Volley: every further arrow into the same enemy hits 1 harder than the one before.
                    int streak = hitsOn.TryGetValue(enemy, out int previous) ? previous : 0;
                    hitsOn[enemy] = streak + 1;
                    int bonus = player.Powerups.Count(PowerupType.StormVolley) > 0 ? streak : 0;
                    using (player.Powerups.ResumeAttack(attack))
                        CombatDamage.Apply(player, enemy, damage + bonus, DamageElement.Physical, arrow.Landing + Vector2.up * 0.3f, 0.2f, infusion);
                }
        }

        /// <summary>The target circle on the floor, pulsing until the last arrow lands.</summary>
        private void DrawMarker()
        {
            float fade = Mathf.Clamp01((endsAt + 0.25f - age) / 0.25f), pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
            marker.Begin();
            marker.Disc(center, Radius, FlameMesh.Alpha(color, (0.06f + 0.05f * pulse) * fade), FlameMesh.Alpha(color, 0.02f * fade));
            marker.Ring(center, Radius, 0.07f, FlameMesh.Alpha(color, (0.45f + 0.3f * pulse) * fade), 56);
            // Each falling arrow's shadow darkens and tightens on its landing spot as it drops.
            foreach (var arrow in arrows)
            {
                if (arrow.Landed || age < arrow.LandAt - FallTime) continue;
                float t = Mathf.Clamp01((age - (arrow.LandAt - FallTime)) / FallTime);
                Vector2 shadow = arrow.Landing + Slant * FallHeight * (1f - t * t) * 0.08f;
                marker.Ellipse(shadow, Mathf.Lerp(0.3f, 0.12f, t), Mathf.Lerp(0.12f, 0.05f, t), new Color(0f, 0f, 0f, 0.35f * t), new Color(0f, 0f, 0f, 0f), 12);
            }
            marker.Commit();
        }

        private void OnDestroy() => marker?.Release();
    }
}
