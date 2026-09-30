using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Consecration: sanctified ground around the Paladin for a few seconds. Enemies inside take holy damage every second;
    /// heroes inside are blessed.
    /// </summary>
    public sealed class Consecration : MonoBehaviour
    {
        public const float Radius = 2.6f, Duration = 5f, Interval = 1f;
        private DungeonPlayer player;
        private Vector2 center;
        private float until, nextTick;
        private int damage;

        public static void Sanctify(DungeonPlayer player, float duration, int damage)
        {
            var ground = new GameObject("Consecration").AddComponent<Consecration>();
            ground.transform.SetParent(player.Run.ProjectileRoot, false);
            ground.center = player.transform.position;
            ground.player = player;
            ground.until = Time.time + duration;
            ground.damage = damage;
            ConsecrationVfx.Play(player.Run, ground.center, duration, Radius);
            CoopFx.Consecration(player.Run, ground.center, duration, Radius);
        }

        private void Update()
        {
            var run = player != null ? player.Run : null;
            if (run == null || Time.time >= until) { Destroy(gameObject); return; }
            if (!run.IsPlaying || Time.time < nextTick) return;
            nextTick = Time.time + Interval;
            foreach (var enemy in run.Enemies.ToArray())
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= Radius + enemy.HitRadius)
                    CombatDamage.Apply(player, enemy, damage, DamageElement.Holy, center, 0f);
            foreach (var ally in FindObjectsByType<DungeonPlayer>())
                if (ally.Run == run && ally.Health > 0 && Vector2.Distance(center, ally.transform.position) <= Radius)
                    ally.Blessing.Apply(PaladinAttack.BlessingDamage, Interval + 0.3f, player);
            run.Coop?.SupportAllies(center, Radius, SupportKind.Bless, PaladinAttack.BlessingDamage, Interval + 0.3f);
        }
    }

    /// <summary>
    /// The consecrated ground: a holy sigil burned into the floor (double rim ring with rune ticks, a slowly turning
    /// eight-pointed star and a cross at its heart) ringed by white-gold holy fire, with motes of light rising off it.
    /// Every second a wave of light rolls out from the centre and a pillar of light smites each enemy standing inside.
    /// Purely visual; every machine draws its own.
    /// </summary>
    public sealed class ConsecrationVfx : MonoBehaviour
    {
        private const float FadeTime = 0.35f, Interval = 1f;
        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f), Pale = new Color(1f, 0.96f, 0.78f), Amber = new Color(1f, 0.6f, 0.15f);
        private DungeonRun run;
        private Vector2 center;
        private float age, duration, radius, nextWave;
        private FlameMesh mesh;

        public static void Play(DungeonRun run, Vector2 center, float duration, float radius)
        {
            if (run == null || run.ProjectileRoot == null) return;
            var vfx = new GameObject("Consecrated ground").AddComponent<ConsecrationVfx>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            vfx.transform.SetParent(run.ProjectileRoot, false);
            vfx.run = run;
            vfx.center = center;
            vfx.duration = duration;
            vfx.radius = radius;
            vfx.mesh = new FlameMesh(vfx.gameObject, 2);
            HeroVfx.Pulse(run.ProjectileRoot, center, radius, FlameMesh.Alpha(Pale, 0.8f), 0.4f);
            ScreenFx.Flash(new Color(1f, 0.95f, 0.75f, 0.12f), 0.15f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (run == null || run.ProjectileRoot == null || age >= duration + FadeTime) { Destroy(gameObject); return; }
            float alpha = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((duration + FadeTime - age) / FadeTime);
            float time = Time.time;
            if (age >= nextWave && age < duration)
            {
                nextWave = age + Interval;
                Smite();
            }
            float wave = Mathf.Clamp01((age - (nextWave - Interval)) / 0.5f);

            mesh.Begin();
            // Warm light pooled on the floor.
            mesh.Disc(center, radius, FlameMesh.Alpha(Pale, 0.16f * alpha), FlameMesh.Alpha(Gold, 0.08f * alpha), 48);
            // The wave of light rolling outward.
            if (wave < 1f) mesh.Ring(center, radius * wave, 0.25f * (1f - wave), FlameMesh.Alpha(Color.white, 0.7f * (1f - wave) * alpha), FlameMesh.Alpha(Gold, 0f), 48);
            // Double rim with rune ticks between the rings.
            mesh.Ring(center, radius, 0.06f, FlameMesh.Alpha(Gold, 0.85f * alpha), 64);
            mesh.Ring(center, radius * 0.88f, 0.03f, FlameMesh.Alpha(Gold, 0.6f * alpha), 64);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12f - time * 0.15f;
                Vector2 dir = FlameMesh.Polar(a, 1f);
                float len = i % 3 == 0 ? 0.1f : 0.05f;
                mesh.Bar(center + dir * (radius * 0.94f - len * 0.5f), dir, len, 0.03f, FlameMesh.Alpha(Pale, 0.8f * alpha), FlameMesh.Alpha(Pale, 0.8f * alpha));
            }
            // An eight-pointed star turning slowly the other way: two squares' outlines.
            float star = radius * 0.72f, spin = time * 0.3f;
            for (int square = 0; square < 2; square++)
                for (int i = 0; i < 4; i++)
                {
                    float a0 = spin + square * Mathf.PI / 4f + i * Mathf.PI / 2f, a1 = a0 + Mathf.PI / 2f;
                    Vector2 p0 = center + FlameMesh.Polar(a0, star), p1 = center + FlameMesh.Polar(a1, star);
                    Line(p0, p1, 0.035f, FlameMesh.Alpha(Gold, 0.55f * alpha));
                }
            mesh.Ring(center, radius * 0.3f, 0.03f, FlameMesh.Alpha(Gold, 0.6f * alpha), 32);
            // A cross of light at the heart.
            float cross = radius * 0.22f;
            mesh.Bar(center + Vector2.down * cross, Vector2.up, cross * 2f, 0.09f, FlameMesh.Alpha(Pale, 0.9f * alpha), FlameMesh.Alpha(Pale, 0.9f * alpha));
            mesh.Bar(center + Vector2.left * cross * 0.7f + Vector2.up * cross * 0.35f, Vector2.right, cross * 1.4f, 0.09f, FlameMesh.Alpha(Pale, 0.9f * alpha), FlameMesh.Alpha(Pale, 0.9f * alpha));
            // Holy fire licking up round the rim: white at the root, gold, then gone.
            for (int i = 0; i < 28; i++)
            {
                float seed = FlameMesh.Hash(i, 3.7f), a = i * Mathf.PI * 2f / 28f;
                float flicker = 0.6f + 0.4f * Mathf.Sin(time * (7f + seed * 6f) + seed * 30f);
                Vector2 root = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                float h = (0.22f + 0.2f * seed) * flicker, w = 0.09f + 0.04f * seed;
                Vector2 sway = Vector2.right * Mathf.Sin(time * 5f + seed * 20f) * w * 0.4f;
                mesh.Triangle(root - Vector2.right * w, root + Vector2.up * h + sway, root + Vector2.right * w,
                    FlameMesh.Alpha(Gold, 0.8f * alpha), FlameMesh.Alpha(Amber, 0f), FlameMesh.Alpha(Gold, 0.8f * alpha));
                mesh.Triangle(root - Vector2.right * w * 0.5f, root + Vector2.up * h * 0.55f + sway * 0.6f, root + Vector2.right * w * 0.5f,
                    FlameMesh.Alpha(Color.white, alpha), FlameMesh.Alpha(Pale, 0.1f * alpha), FlameMesh.Alpha(Color.white, alpha));
            }
            // Motes of light drifting up off the ground.
            for (int i = 0; i < 14; i++)
            {
                float seed = FlameMesh.Hash(i, 9.1f), cycle = Mathf.Repeat(time * (0.5f + seed * 0.4f) + seed, 1f);
                float a = seed * Mathf.PI * 2f + i, r = radius * Mathf.Sqrt(FlameMesh.Hash(i, 4.4f)) * 0.9f;
                Vector2 p = center + FlameMesh.Polar(a, r) + Vector2.up * cycle * 1.1f;
                mesh.Bar(p, Vector2.up, 0.14f, 0.04f, FlameMesh.Alpha(Pale, 0.8f * Mathf.Sin(cycle * Mathf.PI) * alpha), FlameMesh.Alpha(Gold, 0f));
            }
            mesh.Commit();
        }

        /// <summary>A pillar of light slams down on each enemy standing in the ground (visual only).</summary>
        private void Smite()
        {
            foreach (var enemy in run.Enemies)
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(center, enemy.transform.position) <= radius + enemy.HitRadius)
                    HolySmiteVfx.Play(run.ProjectileRoot, enemy.transform.position);
        }

        private void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            float length = Vector2.Distance(from, to);
            if (length < 0.001f) return;
            mesh.Bar(from, (to - from) / length, length, width, color, color);
        }

        private void OnDestroy() => mesh?.Release();
    }

    /// <summary>A short pillar of holy light striking down on one spot, with a flash and a splash of light at its foot.</summary>
    public sealed class HolySmiteVfx : MeshEffect
    {
        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f), Pale = new Color(1f, 0.96f, 0.78f);
        private Vector2 at;

        public static void Play(Transform root, Vector2 at)
        {
            var effect = Spawn<HolySmiteVfx>(root, 0.35f, 10);
            if (effect == null) return;
            effect.at = at;
            effect.Redraw();
            HeroVfx.Sparks(root, at, Pale, 5, 2.5f, 0.25f, Vector2.up, 120f, 0.7f);
        }

        protected override void Draw(float t)
        {
            float strike = EaseOut(Mathf.Clamp01(t / 0.25f)), fade = 1f - t;
            float width = 0.28f * (1f - 0.5f * t);
            Vector2 top = at + Vector2.up * 5f, bottom = at + Vector2.up * 5f * (1f - strike);
            Mesh.Quad(bottom - Vector2.right * width, top - Vector2.right * width * 0.6f, top + Vector2.right * width * 0.6f, bottom + Vector2.right * width,
                FlameMesh.Alpha(Gold, 0.6f * fade), FlameMesh.Alpha(Gold, 0f), FlameMesh.Alpha(Gold, 0f), FlameMesh.Alpha(Gold, 0.6f * fade));
            Mesh.Quad(bottom - Vector2.right * width * 0.35f, top - Vector2.right * width * 0.2f, top + Vector2.right * width * 0.2f, bottom + Vector2.right * width * 0.35f,
                FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Pale, 0f), FlameMesh.Alpha(Pale, 0f), FlameMesh.Alpha(Color.white, fade));
            if (strike >= 1f) Mesh.Ellipse(at, 0.5f * (0.6f + t), 0.2f * (0.6f + t), FlameMesh.Alpha(Color.white, 0.8f * fade), FlameMesh.Alpha(Gold, 0f), 20);
        }
    }
}
