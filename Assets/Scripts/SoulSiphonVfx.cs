using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Soul Siphon: a violet aura of orbiting soul wisps around the Demoness, and a curving stream of glowing souls
    /// flowing into her from every paralysed enemy in reach. Each machine draws its own copy from what it sees.
    /// </summary>
    public sealed class SoulSiphonVfx : MonoBehaviour
    {
        private static readonly Color Soul = new Color(0.78f, 0.55f, 1f), Core = new Color(0.97f, 0.92f, 1f), Deep = new Color(0.35f, 0.1f, 0.55f);
        private const int Wisps = 7, StreamSouls = 6;
        private DungeonRun run;
        private Transform target;
        private FlameMesh mesh;
        private float age, duration, radius;

        public static SoulSiphonVfx Play(DungeonRun run, Transform target, float duration, float radius)
        {
            if (run == null || run.ProjectileRoot == null || target == null) return null;
            var vfx = new GameObject("Soul siphon").AddComponent<SoulSiphonVfx>();
            vfx.transform.SetParent(run.ProjectileRoot, false);
            vfx.run = run;
            vfx.target = target;
            vfx.duration = duration;
            vfx.radius = radius;
            vfx.mesh = new FlameMesh(vfx.gameObject, 6);
            return vfx;
        }

        /// <summary>A drain tick landed: the aura flares and souls burst into her.</summary>
        public void Flare(int drained)
        {
            if (target == null) return;
            HeroVfx.Pulse(run.ProjectileRoot, target.position, 0.9f, Soul, 0.3f);
            HeroVfx.Motes(run.ProjectileRoot, target.position, 0.6f, Core, 6 + drained * 3, 0.7f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (target == null || age >= duration + 0.3f) { Destroy(gameObject); return; }
            float fade = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((duration + 0.3f - age) / 0.3f);
            Vector2 center = target.position;
            mesh.Begin();
            // The reach: a faint violet floor glow with a slowly turning edge.
            mesh.Disc(center, radius, FlameMesh.Alpha(Deep, 0.12f * fade), FlameMesh.Alpha(Deep, 0f), 40);
            mesh.Ring(center, radius, 0.04f, FlameMesh.Alpha(Soul, 0.35f * fade), 48);
            // Wisps circling close around her, bobbing as they go.
            for (int i = 0; i < Wisps; i++)
            {
                float angle = age * 2.4f + i * Mathf.PI * 2f / Wisps;
                Vector2 wisp = center + new Vector2(Mathf.Cos(angle) * 0.75f, Mathf.Sin(angle) * 0.4f + 0.15f + 0.08f * Mathf.Sin(age * 6f + i));
                Vector2 trail = wisp - new Vector2(-Mathf.Sin(angle) * 0.75f, Mathf.Cos(angle) * 0.4f).normalized * 0.22f;
                mesh.Bar(trail, (wisp - trail).normalized, Vector2.Distance(trail, wisp), 0.07f, FlameMesh.Alpha(Soul, 0f), FlameMesh.Alpha(Soul, 0.8f * fade));
                mesh.Diamond(wisp, 0.07f, FlameMesh.Alpha(Core, fade));
            }
            // A stream of souls from every paralysed enemy she can reach, bowing out to one side as it comes.
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || enemy.Health <= 0 || !enemy.IsParalyzed) continue;
                Vector2 from = enemy.transform.position;
                if (Vector2.Distance(from, center) > radius + enemy.HitRadius) continue;
                Vector2 bow = Vector2.Perpendicular(center - from) * 0.35f;
                Vector2 control = Vector2.Lerp(from, center, 0.5f) + bow;
                Vector2 last = from;
                for (int step = 1; step <= 10; step++)
                {
                    Vector2 point = Bezier(from, control, center, step / 10f);
                    mesh.Bar(last, (point - last).normalized, Vector2.Distance(last, point), 0.05f,
                        FlameMesh.Alpha(Deep, 0.35f * fade), FlameMesh.Alpha(Soul, 0.35f * fade));
                    last = point;
                }
                for (int k = 0; k < StreamSouls; k++)
                {
                    float t = Mathf.Repeat(age * 1.6f + k / (float)StreamSouls + (enemy.GetHashCode() % 97) * 0.137f, 1f);
                    Vector2 soul = Bezier(from, control, center, t);
                    float size = Mathf.Lerp(0.12f, 0.05f, t);
                    mesh.Disc(soul, size * 1.8f, FlameMesh.Alpha(Soul, 0.45f * fade), FlameMesh.Alpha(Soul, 0f), 10);
                    mesh.Diamond(soul, size, FlameMesh.Alpha(Core, fade));
                }
                // The victim's soul wavers as it is pulled out.
                mesh.Ring(from, enemy.HitRadius + 0.1f + 0.05f * Mathf.Sin(age * 12f), 0.05f, FlameMesh.Alpha(Soul, 0.7f * fade), 20);
            }
            mesh.Commit();
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) => Vector2.Lerp(Vector2.Lerp(a, b, t), Vector2.Lerp(b, c, t), t);

        private void OnDestroy() => mesh?.Release();
    }
}
