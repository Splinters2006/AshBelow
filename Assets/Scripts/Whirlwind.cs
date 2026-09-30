using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Whirlwind: the Knight spins with his sword for a while, cutting everything around him; he can still walk, but
    /// can't attack or parry until the spin ends.
    /// A steel blade sweeps round him in a full circle, trailing a bright ring of motion behind it, with gusts of dust
    /// thrown off the rim. Teammates see the same spin on him.
    /// </summary>
    public sealed class Whirlwind : MonoBehaviour
    {
        public const float Radius = 1.9f, Tick = 0.25f, Duration = 2f, TurnsPerSecond = 3f;
        public static readonly Color Steel = new Color(0.55f, 1f, 0.9f);
        private DungeonPlayer player;
        private float until, nextTick;
        private int damage;

        public static void Spin(DungeonPlayer player, float duration, int damage)
        {
            var spin = player.GetComponent<Whirlwind>() ?? player.gameObject.AddComponent<Whirlwind>();
            spin.player = player;
            spin.damage = damage;
            spin.until = Time.time + duration;
            spin.nextTick = Time.time;
            // Both hands are on the sword: any raised shield or charging swing drops.
            player.Shield?.Cancel();
            player.Charge.Cancel();
            WhirlwindVfx.Play(player.Run.ProjectileRoot, player.transform, duration, Radius);
            CoopFx.Whirlwind(player.Run, duration, Radius);
        }

        public bool IsSpinning => Time.time < until;

        /// <summary>While the Knight spins, his sword and shield are busy: he can't attack or parry.</summary>
        public static bool IsSpinningOn(DungeonPlayer player)
        {
            var spin = player != null ? player.GetComponent<Whirlwind>() : null;
            return spin != null && spin.IsSpinning;
        }

        private void Update()
        {
            if (player == null || !player.Run.IsPlaying || player.Health <= 0 || Time.time >= until) return;
            if (Time.time < nextTick) return;
            nextTick += Tick;
            var run = player.Run;
            Vector2 center = player.transform.position;
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= Radius + enemy.HitRadius
                    && run.HasLineOfSight(center, enemy.transform.position))
                {
                    CombatDamage.Apply(player, enemy, damage, DamageElement.Physical, center, 0.4f);
                    HeroVfx.Sparks(run.ProjectileRoot, enemy.transform.position, Color.white, 3, 3f, 0.15f, (Vector2)enemy.transform.position - center, 90f, 0.6f);
                }
        }
    }

    /// <summary>The spin itself, following the hero: a circling blade, its motion ring and dust off the rim.</summary>
    public sealed class WhirlwindVfx : MonoBehaviour
    {
        private const float FadeTime = 0.2f;
        private Transform root, hero;
        private FlameMesh mesh;
        private float age, duration, radius;

        public static void Play(Transform root, Transform hero, float duration, float radius)
        {
            if (root == null || hero == null) return;
            var spin = new GameObject("Whirlwind").AddComponent<WhirlwindVfx>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            spin.transform.SetParent(root, false);
            spin.root = root;
            spin.hero = hero;
            spin.duration = duration;
            spin.radius = radius;
            spin.mesh = new FlameMesh(spin.gameObject, 6);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (hero == null || age >= duration + FadeTime) { Destroy(gameObject); return; }
            Vector2 center = hero.position;
            float alpha = Mathf.Clamp01(age / 0.1f) * Mathf.Clamp01((duration + FadeTime - age) / FadeTime);
            float angle = -(age * Whirlwind.TurnsPerSecond * 360f) * Mathf.Deg2Rad;
            Color steel = Whirlwind.Steel;
            mesh.Begin();
            // A faint disc of the whole reach, so the hit area reads.
            mesh.Disc(center, radius, FlameMesh.Alpha(steel, 0.04f * alpha), FlameMesh.Alpha(steel, 0.12f * alpha), 40);
            // The motion ring: a band that trails the blade most of the way round, brightest right behind it.
            const int Segments = 36;
            const float TrailSweep = Mathf.PI * 1.6f;
            for (int i = 0; i < Segments; i++)
            {
                float u0 = i / (float)Segments, u1 = (i + 1) / (float)Segments;
                // The blade turns clockwise, so its trail lies anticlockwise of it.
                float a0 = angle + u0 * TrailSweep, a1 = angle + u1 * TrailSweep;
                float f0 = (1f - u0) * (1f - u0), f1 = (1f - u1) * (1f - u1);
                float inner = radius * 0.35f;
                mesh.Quad(center + FlameMesh.Polar(a0, inner), center + FlameMesh.Polar(a0, radius), center + FlameMesh.Polar(a1, radius), center + FlameMesh.Polar(a1, inner),
                    FlameMesh.Alpha(steel, 0f), FlameMesh.Alpha(Color.Lerp(steel, Color.white, 0.5f), 0.55f * f0 * alpha),
                    FlameMesh.Alpha(Color.Lerp(steel, Color.white, 0.5f), 0.55f * f1 * alpha), FlameMesh.Alpha(steel, 0f));
                mesh.Quad(center + FlameMesh.Polar(a0, radius - 0.05f), center + FlameMesh.Polar(a0, radius + 0.03f),
                    center + FlameMesh.Polar(a1, radius + 0.03f), center + FlameMesh.Polar(a1, radius - 0.05f),
                    FlameMesh.Alpha(Color.white, 0.8f * f0 * alpha), FlameMesh.Alpha(Color.white, 0.8f * f0 * alpha),
                    FlameMesh.Alpha(Color.white, 0.8f * f1 * alpha), FlameMesh.Alpha(Color.white, 0.8f * f1 * alpha));
            }
            // The sword: a grip at the hero, a crossguard, then a tapering blade out to the rim.
            Vector2 dir = FlameMesh.Polar(angle, 1f), side = Vector2.Perpendicular(dir);
            Vector2 hilt = center + dir * 0.3f, guard = center + dir * 0.45f, tip = center + dir * radius * 0.98f;
            mesh.Bar(hilt, dir, 0.15f, 0.07f, FlameMesh.Alpha(new Color(0.35f, 0.25f, 0.18f), alpha), FlameMesh.Alpha(new Color(0.35f, 0.25f, 0.18f), alpha));
            mesh.Bar(guard - side * 0.14f, side, 0.28f, 0.06f, FlameMesh.Alpha(AbilityCatalog.Gold, alpha), FlameMesh.Alpha(AbilityCatalog.Gold, alpha));
            mesh.Quad(guard - side * 0.07f, guard + side * 0.07f, tip + side * 0.015f, tip - side * 0.015f,
                FlameMesh.Alpha(new Color(0.8f, 0.86f, 0.92f), alpha), FlameMesh.Alpha(Color.white, alpha), FlameMesh.Alpha(Color.white, alpha), FlameMesh.Alpha(new Color(0.8f, 0.86f, 0.92f), alpha));
            mesh.Diamond(tip, 0.06f, FlameMesh.Alpha(Color.white, 0.9f * alpha));
            mesh.Commit();
            // Dust kicked off the rim, flung outward.
            if (Time.frameCount % 3 == 0 && alpha > 0.5f)
                HeroVfx.Sparks(root, tip + Vector2.down * 0.2f, new Color(0.75f, 0.72f, 0.65f, 0.5f), 1, 2.5f, 0.3f, dir, 40f, 0.8f);
        }

        private void OnDestroy() => mesh?.Release();
    }
}
