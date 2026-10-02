using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Ball Lightning: a slow orb that drifts forward for a few seconds, zapping every enemy it passes. It is drawn as a
    /// glowing sphere: a white-hot heart shading out to electric blue, a bright rim and a highlight, with forks of
    /// lightning crawling over its surface and a few leaping off it, inside a flickering halo, with sparks circling it.
    /// Teammates see the same orb drift; only the caster's machine zaps.
    /// </summary>
    public sealed class BallLightning : MonoBehaviour
    {
        public const float Speed = 3.3f, Duration = 4f, Reach = 1.4f, ZapInterval = 0.4f, OrbRadius = 0.32f;
        private const int ArcCount = 4, ArcPoints = 6;
        private DungeonRun run;
        private DungeonPlayer player;
        private Vector2 direction, position;
        private float until, nextZap, nextCrackle, age;
        private int damage;
        private bool ghost;
        private FlameMesh mesh;
        private readonly Vector2[,] arcs = new Vector2[ArcCount, ArcPoints];

        public static void Launch(DungeonPlayer player, Vector2 aim, float duration, int damage)
        {
            var ball = Create(player.Run, player.transform.position, aim, duration);
            ball.player = player;
            ball.damage = damage;
            CoopFx.BallLightning(player.Run, player.transform.position, aim, duration);
        }

        public static void SpawnGhost(DungeonRun run, Vector2 origin, Vector2 aim, float duration) => Create(run, origin, aim, duration).ghost = true;

        private static BallLightning Create(DungeonRun run, Vector2 origin, Vector2 aim, float duration)
        {
            var ball = new GameObject("Ball lightning").AddComponent<BallLightning>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            ball.transform.SetParent(run.ProjectileRoot, false);
            ball.run = run;
            ball.position = origin;
            ball.direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
            ball.until = Time.time + duration;
            ball.mesh = new FlameMesh(ball.gameObject, 7);
            HeroVfx.Sparks(run.ProjectileRoot, origin, CombatDamage.ShockColor, 8, 3f, 0.25f);
            return ball;
        }

        private void Update()
        {
            if (run == null || run.ProjectileRoot == null || (!ghost && player == null)) { Destroy(gameObject); return; }
            if (Time.time >= until)
            {
                HeroVfx.Pulse(run.ProjectileRoot, position, 0.8f, CombatDamage.ShockColor, 0.2f);
                HeroVfx.Sparks(run.ProjectileRoot, position, Color.white, 10, 4f, 0.25f);
                Destroy(gameObject);
                return;
            }
            if (!run.IsPlaying) return;
            age += Time.deltaTime;
            Vector2 next = position + direction * Speed * Time.deltaTime;
            if (run.Map.CanStand(next, 0.2f)) position = next;
            Draw();
            if (ghost || Time.time < nextZap) return;
            nextZap = Time.time + ZapInterval;
            foreach (var enemy in run.Enemies.ToArray())
            {
                if (enemy == null || enemy.Health <= 0 || Vector2.Distance(position, enemy.transform.position) > Reach + enemy.HitRadius) continue;
                CombatVfx.Bolt(run.ProjectileRoot, position, enemy.transform.position, CombatDamage.ShockColor);
                CoopFx.Bolt(run, position, enemy.transform.position, CombatDamage.ShockColor);
                // Every Wizard ability sets off its element.
                CombatDamage.Apply(player, enemy, damage, DamageElement.Lightning, position, 0.2f);
            }
        }

        private void Draw()
        {
            Color shock = CombatDamage.ShockColor;
            float time = Time.time, fadeIn = Mathf.Clamp01(age / 0.15f);
            float radius = OrbRadius * fadeIn * (1f + 0.06f * Mathf.Sin(time * 31f) + 0.04f * Mathf.Sin(time * 53f));
            // New forks crawl over it many times a second.
            if (time >= nextCrackle)
            {
                nextCrackle = time + 0.06f;
                for (int a = 0; a < ArcCount; a++)
                {
                    bool leaping = a == ArcCount - 1;
                    float start = Random.value * Mathf.PI * 2f, end = start + Random.Range(1.5f, 3.2f);
                    for (int p = 0; p < ArcPoints; p++)
                    {
                        float u = p / (float)(ArcPoints - 1);
                        // Surface forks wander just inside the rim; the last one leaps out into the air.
                        float r = leaping ? radius * (0.8f + u * 1.6f) : radius * Random.Range(0.35f, 0.95f);
                        float angle = leaping ? start + Random.Range(-0.25f, 0.25f) : Mathf.Lerp(start, end, u);
                        arcs[a, p] = FlameMesh.Polar(angle, r);
                    }
                }
            }
            mesh.Begin();
            // A faint wake trailing behind as it drifts.
            mesh.Quad(position - Vector2.Perpendicular(direction) * radius * 0.8f, position + Vector2.Perpendicular(direction) * radius * 0.8f,
                position - direction * radius * 3f, position - direction * radius * 3f,
                FlameMesh.Alpha(shock, 0.25f * fadeIn), FlameMesh.Alpha(shock, 0.25f * fadeIn), FlameMesh.Alpha(shock, 0f), FlameMesh.Alpha(shock, 0f));
            // Flickering halo.
            mesh.Disc(position, radius * (2.6f + 0.3f * Mathf.Sin(time * 23f)), FlameMesh.Alpha(shock, 0.28f * fadeIn), FlameMesh.Alpha(shock, 0f), 32);
            // The sphere: white-hot heart out to electric blue, a darker limb and a bright rim.
            mesh.Disc(position, radius, FlameMesh.Alpha(Color.white, fadeIn), FlameMesh.Alpha(Color.Lerp(shock, new Color(0.2f, 0.35f, 0.9f), 0.35f), fadeIn), 32);
            mesh.Ring(position, radius, 0.04f, FlameMesh.Alpha(Color.Lerp(shock, Color.white, 0.5f), fadeIn), 32);
            // A highlight up and to the left sells it as round.
            mesh.Ellipse(position + new Vector2(-0.35f, 0.4f) * radius, radius * 0.3f, radius * 0.2f, FlameMesh.Alpha(Color.white, 0.8f * fadeIn), FlameMesh.Alpha(Color.white, 0f), 12);
            // The forks.
            for (int a = 0; a < ArcCount; a++)
                for (int p = 0; p < ArcPoints - 1; p++)
                {
                    Vector2 from = position + arcs[a, p], to = position + arcs[a, p + 1];
                    float length = Vector2.Distance(from, to);
                    if (length < 0.001f) continue;
                    Vector2 dir = (to - from) / length;
                    mesh.Bar(from, dir, length, 0.05f, FlameMesh.Alpha(shock, 0.6f * fadeIn), FlameMesh.Alpha(shock, 0.6f * fadeIn));
                    mesh.Bar(from, dir, length, 0.02f, FlameMesh.Alpha(Color.white, fadeIn), FlameMesh.Alpha(Color.white, fadeIn));
                }
            // Sparks circling it on tilted orbits.
            for (int i = 0; i < 3; i++)
            {
                float a = time * (5f + i * 1.3f) + i * 2.1f;
                Vector2 p = position + new Vector2(Mathf.Cos(a) * radius * 1.7f, Mathf.Sin(a) * radius * (0.6f + 0.4f * i));
                mesh.Diamond(p, 0.045f, FlameMesh.Alpha(Color.white, 0.9f * fadeIn));
            }
            mesh.Commit();
        }

        private void OnDestroy() => mesh?.Release();
    }
}
