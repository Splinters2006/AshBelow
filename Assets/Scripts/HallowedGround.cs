using UnityEngine;

namespace Slopgame
{
    /// <summary>Holy Ground: where a holy sword struck, the ground glows for a while and slows enemies crossing it.</summary>
    public sealed class HallowedGround : MonoBehaviour
    {
        public const float Radius = 1.2f, Duration = 3f;
        private DungeonRun run;
        private float until, nextSlow;

        public static void Leave(DungeonRun run, Vector2 at)
        {
            var ground = new GameObject("Hallowed ground").AddComponent<HallowedGround>();
            ground.transform.SetParent(run.ProjectileRoot, false);
            ground.transform.position = at;
            ground.run = run;
            ground.until = Time.time + Duration;
            HallowedGroundVfx.Play(run, at, Duration, Radius);
            CoopFx.HallowedGround(run, at, Duration, Radius);
        }

        private void Update()
        {
            if (run == null || Time.time >= until) { Destroy(gameObject); return; }
            if (!run.IsPlaying || Time.time < nextSlow) return;
            nextSlow = Time.time + 0.3f;
            Vector2 at = transform.position;
            foreach (var enemy in run.Enemies)
                if (enemy != null && enemy.Health > 0 && Vector2.Distance(at, enemy.transform.position) <= Radius + enemy.HitRadius) enemy.Chill(0.4f);
        }
    }

    /// <summary>
    /// The hallowed patch: a pool of warm light inside a golden rim, with a small cross at its heart and motes rising
    /// off it. It holds its glow for as long as the ground slows, then fades. Purely visual; every machine draws its own.
    /// </summary>
    public sealed class HallowedGroundVfx : MonoBehaviour
    {
        private const float FadeTime = 0.35f;
        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f), Pale = new Color(1f, 0.96f, 0.78f);
        private DungeonRun run;
        private Vector2 center;
        private float age, duration, radius;
        private FlameMesh mesh;

        public static void Play(DungeonRun run, Vector2 center, float duration, float radius)
        {
            if (run == null || run.ProjectileRoot == null) return;
            var vfx = new GameObject("Hallowed ground glow").AddComponent<HallowedGroundVfx>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            vfx.transform.SetParent(run.ProjectileRoot, false);
            vfx.run = run;
            vfx.center = center;
            vfx.duration = duration;
            vfx.radius = radius;
            vfx.mesh = new FlameMesh(vfx.gameObject, 2);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (run == null || run.ProjectileRoot == null || age >= duration + FadeTime) { Destroy(gameObject); return; }
            float alpha = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((duration + FadeTime - age) / FadeTime);
            float time = Time.time, breathe = 0.85f + 0.15f * Mathf.Sin(time * 4f);

            mesh.Begin();
            mesh.Disc(center, radius, FlameMesh.Alpha(Pale, 0.3f * alpha * breathe), FlameMesh.Alpha(Gold, 0.14f * alpha), 40);
            mesh.Ring(center, radius, 0.06f, FlameMesh.Alpha(Gold, 0.85f * alpha), 48);
            mesh.Ring(center, radius * 0.8f, 0.03f, FlameMesh.Alpha(Gold, 0.5f * alpha * breathe), 48);
            // Rune ticks turning slowly round the rim.
            for (int i = 0; i < 12; i++)
            {
                Vector2 dir = FlameMesh.Polar(i * Mathf.PI / 6f + time * 0.3f, 1f);
                mesh.Bar(center + dir * radius * 0.84f, dir, radius * 0.12f, 0.03f, FlameMesh.Alpha(Pale, 0.8f * alpha), FlameMesh.Alpha(Pale, 0.8f * alpha));
            }
            // A cross of light at the heart.
            float cross = radius * 0.25f;
            mesh.Bar(center + Vector2.down * cross, Vector2.up, cross * 2f, 0.07f, FlameMesh.Alpha(Pale, 0.8f * alpha), FlameMesh.Alpha(Pale, 0.8f * alpha));
            mesh.Bar(center + Vector2.left * cross * 0.7f + Vector2.up * cross * 0.35f, Vector2.right, cross * 1.4f, 0.07f, FlameMesh.Alpha(Pale, 0.8f * alpha), FlameMesh.Alpha(Pale, 0.8f * alpha));
            // Motes of light drifting up off the ground.
            for (int i = 0; i < 8; i++)
            {
                float seed = FlameMesh.Hash(i, 6.3f), cycle = Mathf.Repeat(time * (0.5f + seed * 0.4f) + seed, 1f);
                Vector2 p = center + FlameMesh.Polar(seed * Mathf.PI * 2f + i, radius * Mathf.Sqrt(FlameMesh.Hash(i, 2.9f)) * 0.9f) + Vector2.up * cycle * 0.8f;
                mesh.Bar(p, Vector2.up, 0.12f, 0.04f, FlameMesh.Alpha(Pale, 0.8f * Mathf.Sin(cycle * Mathf.PI) * alpha), FlameMesh.Alpha(Gold, 0f));
            }
            mesh.Commit();
        }

        private void OnDestroy() => mesh?.Release();
    }
}
