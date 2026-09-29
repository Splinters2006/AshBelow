using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Arrow Volley: arrows rain out of the sky onto a marked circle around the cursor, one after another. Each arrow
    /// strikes every enemy close to where it lands and carries the element loaded in the Elemental Quiver. Teammates
    /// see every arrow fall as a ghost arrow; only the caster's machine deals the damage.
    /// </summary>
    public sealed class ArrowRain : MonoBehaviour
    {
        public const float Range = 8f, Radius = 2.4f, Duration = 1.1f, ImpactRadius = 0.6f;
        public const int BaseArrows = 16, ArrowsPerRank = 4;
        private const float FallTime = 0.22f, FallHeight = 6f;

        private sealed class Arrow
        {
            public Vector2 Landing;
            public float LandAt;
            public SpriteRenderer Sprite;
            public bool Landed;
        }

        private readonly List<Arrow> arrows = new List<Arrow>();
        private DungeonPlayer player;
        private Vector2 center;
        private int damage;
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
                Vector2 sky = arrow.Landing + Vector2.up * FallHeight;
                if (arrow.Sprite == null)
                {
                    arrow.Sprite = DungeonVisuals.Create("Falling arrow", run.ProjectileRoot, sky, new Vector2(0.5f, 0.1f), color, 7);
                    arrow.Sprite.transform.rotation = Quaternion.Euler(0, 0, -90f);
                    CombatVfx.Trail(arrow.Sprite.gameObject, FlameMesh.Alpha(color, 0.8f), 0.07f, 0.1f);
                    CoopFx.Arrow(run, sky, Vector2.down, FallHeight);
                }
                float t = Mathf.Clamp01((age - (arrow.LandAt - FallTime)) / FallTime);
                arrow.Sprite.transform.position = Vector2.Lerp(sky, arrow.Landing, t * t);
                if (t >= 1f) Land(run, arrow);
            }
            DrawMarker();
            if (IsFinished) Destroy(gameObject);
        }

        private void Land(DungeonRun run, Arrow arrow)
        {
            arrow.Landed = true;
            // The arrow sticks in the floor for a moment before fading.
            arrow.Sprite.gameObject.AddComponent<FadingSprite>().Duration = 0.45f;
            HeroVfx.Sparks(run.ProjectileRoot, arrow.Landing, color, 4, 2.5f, 0.2f, Vector2.up, 120f, 0.7f);
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(arrow.Landing, enemy.transform.position) <= ImpactRadius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, arrow.Landing + Vector2.up * 0.3f, 0.2f, infusion);
        }

        /// <summary>The target circle on the floor, pulsing until the last arrow lands.</summary>
        private void DrawMarker()
        {
            float fade = Mathf.Clamp01((endsAt + 0.25f - age) / 0.25f), pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
            marker.Begin();
            marker.Disc(center, Radius, FlameMesh.Alpha(color, (0.06f + 0.05f * pulse) * fade), FlameMesh.Alpha(color, 0.02f * fade));
            marker.Ring(center, Radius, 0.07f, FlameMesh.Alpha(color, (0.45f + 0.3f * pulse) * fade), 56);
            marker.Commit();
        }

        private void OnDestroy() => marker?.Release();
    }
}
