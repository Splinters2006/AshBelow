using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Archdemon's burning presence, kept sparse so his sprite and the arena's hazards stay readable: a shadow
    /// (his flight shadow while airborne), a faint glow, burning eyes, a few embers and, in flight, wings of fire.
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
            aura.behind = new FlameMesh(aura.gameObject, 3) { Pixelated = true };
            // A second renderer needs its own object.
            var frontObject = new GameObject("Hellfire aura front");
            frontObject.transform.SetParent(aura.transform, false);
            // In front of his body (4) and its detail layer (5).
            aura.front = new FlameMesh(frontObject, 6) { Pixelated = true };
            return aura;
        }

        private void LateUpdate()
        {
            if (boss == null || boss.Boss.Enemy.Health <= 0) { Destroy(gameObject); return; }
            Vector2 body = boss.transform.position, ground = boss.GroundPosition;
            float time = Time.time, altitude = boss.Altitude, lift = Mathf.Clamp01(altitude / 2.6f);
            bool enraged = boss.Boss.IsEnraged;

            behind.Begin();
            // His shadow (the flight shadow when airborne).
            behind.Ellipse(ground + Vector2.down * 1.15f, 1.6f - lift * 0.5f, 0.55f - lift * 0.15f,
                FlameMesh.Alpha(Color.black, 0.45f + lift * 0.2f), FlameMesh.Alpha(Color.black, 0f));
            // Wings of fire unfurl behind him only once he takes to the sky; on the ground his own wings read clean.
            if (lift > 0f)
            {
                float flap = Mathf.Sin(time * 7f) * 0.25f;
                float span = 2.3f + lift * 1.2f + (enraged ? 0.4f : 0f);
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 7; i++)
                    {
                        float u = (i + 1) / 7f;
                        float angle = Mathf.Lerp(0.35f, 1.35f, u) + flap;
                        Vector2 dir = new Vector2(Mathf.Cos(angle) * side, Mathf.Sin(angle) * 0.7f + 0.25f).normalized;
                        Vector2 root = body + new Vector2(side * 0.35f, 0.2f);
                        behind.Flame(root, dir, 0.55f + u * 0.25f, span * (1f - Mathf.Abs(u - 0.5f) * 0.8f), FlameMesh.Hash(i, side), 0.95f * lift);
                    }
            }
            behind.Disc(body, 1.4f + lift * 0.6f, FlameMesh.Alpha(FlameMesh.Orange, 0.18f + lift * 0.3f), FlameMesh.Alpha(FlameMesh.Crimson, 0f));
            behind.Commit();

            front.Begin();
            // His eyes burn; brighter and wilder once bloodied.
            float glare = (enraged ? 0.75f : 0.5f) + 0.2f * Mathf.Sin(time * (enraged ? 14f : 5f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = body + new Vector2(side * 0.21f, 0.37f) * (ArchdemonBoss.Size / 2.6f);
                front.Disc(eye, 0.24f, FlameMesh.Alpha(FlameMesh.Yellow, glare), FlameMesh.Alpha(FlameMesh.Orange, 0f), 14);
                if (enraged)
                    front.Bar(eye, new Vector2(side * 0.4f, 1f).normalized, 0.55f, 0.07f, FlameMesh.Alpha(FlameMesh.Core, glare), FlameMesh.Alpha(FlameMesh.Orange, 0f));
            }
            // A few drifting embers shed from his body.
            for (int i = 0; i < 6; i++)
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
