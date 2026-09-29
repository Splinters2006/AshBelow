using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Archdemon's ever-burning presence: scorched ground, a crown of flame, beating wings of fire and,
    /// while he flies, a shadow on the arena floor below him.
    /// </summary>
    public sealed class HellfireAura : MonoBehaviour
    {
        private ArchdemonBoss boss;
        private FlameMesh behind, front;

        public static HellfireAura Attach(ArchdemonBoss boss)
        {
            var aura = new GameObject("Hellfire aura").AddComponent<HellfireAura>();
            aura.transform.SetParent(boss.Boss.Enemy.Run.ProjectileRoot, false);
            aura.boss = boss;
            aura.behind = new FlameMesh(aura.gameObject, 3);
            // A second renderer needs its own object.
            var frontObject = new GameObject("Hellfire aura front");
            frontObject.transform.SetParent(aura.transform, false);
            aura.front = new FlameMesh(frontObject, 5);
            return aura;
        }

        private void LateUpdate()
        {
            if (boss == null || boss.Boss.Enemy.Health <= 0) { Destroy(gameObject); return; }
            Vector2 body = boss.transform.position, ground = boss.GroundPosition;
            float time = Time.time, altitude = boss.Altitude, lift = Mathf.Clamp01(altitude / 2.6f);
            bool enraged = boss.Boss.IsEnraged;

            behind.Begin();
            // Scorched ground (or the flight shadow when airborne).
            behind.Ellipse(ground + Vector2.down * 0.9f, 1.6f - lift * 0.5f, 0.55f - lift * 0.15f,
                FlameMesh.Alpha(Color.black, 0.45f + lift * 0.2f), FlameMesh.Alpha(Color.black, 0f));
            if (lift < 0.5f)
            {
                behind.Ring(ground + Vector2.down * 0.2f, 1.5f + 0.08f * Mathf.Sin(time * 6f), 0.12f, FlameMesh.Alpha(FlameMesh.Orange, 0.45f * (1f - lift * 2f)), 48);
                int groundFlames = enraged ? 14 : 9;
                for (int i = 0; i < groundFlames; i++)
                {
                    float angle = i * Mathf.PI * 2f / groundFlames + time * 0.4f;
                    Vector2 spot = ground + new Vector2(Mathf.Cos(angle) * 1.5f, Mathf.Sin(angle) * 0.6f - 0.4f);
                    behind.Flame(spot, Vector2.up, 0.45f, 0.8f, FlameMesh.Hash(i, 2.2f), 1f - lift * 2f);
                }
            }
            // Wings of fire beat behind him; they flare wide in flight.
            float flap = Mathf.Sin(time * (altitude > 0.2f ? 7f : 3f)) * 0.25f;
            float span = 2.3f + lift * 1.2f + (enraged ? 0.4f : 0f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 7; i++)
                {
                    float u = (i + 1) / 7f;
                    float angle = Mathf.Lerp(0.35f, 1.35f, u) + flap;
                    Vector2 dir = new Vector2(Mathf.Cos(angle) * side, Mathf.Sin(angle) * 0.7f + 0.25f).normalized;
                    Vector2 root = body + new Vector2(side * 0.35f, 0.2f);
                    behind.Flame(root, dir, 0.55f + u * 0.25f, span * (1f - Mathf.Abs(u - 0.5f) * 0.8f), FlameMesh.Hash(i, side), 0.95f);
                }
            behind.Disc(body, 1.4f + lift * 0.6f, FlameMesh.Alpha(FlameMesh.Orange, 0.3f + lift * 0.25f), FlameMesh.Alpha(FlameMesh.Crimson, 0f));
            behind.Commit();

            front.Begin();
            // A crown of flame over the horns.
            for (int i = -2; i <= 2; i++)
                front.Flame(body + new Vector2(i * 0.22f, 0.95f - Mathf.Abs(i) * 0.08f), Vector2.up, 0.3f, 0.9f - Mathf.Abs(i) * 0.12f, FlameMesh.Hash(i, 8.8f), 0.9f);
            // Drifting embers shed from his body.
            for (int i = 0; i < 16; i++)
            {
                float seed = FlameMesh.Hash(i, 4.4f), rise = Mathf.Repeat(time * (0.6f + seed) + seed, 1f);
                Vector2 p = body + new Vector2((seed - 0.5f) * 2.4f + Mathf.Sin(time * 2f + i) * 0.2f, -0.6f + rise * 2.6f);
                front.Diamond(p, 0.06f + seed * 0.05f, FlameMesh.Alpha(FlameMesh.Yellow, 1f - rise));
            }
            front.Commit();
        }

        private void OnDestroy()
        {
            behind?.Release();
            front?.Release();
        }
    }
}
