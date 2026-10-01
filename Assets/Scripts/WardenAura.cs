using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Rime Warden's presence: a turning rune circle on the floor, an icy glow, snowflakes orbiting its body,
    /// frost-lit eyes, and a crystal of ice in its hands that swells while it winds up a spell.
    /// </summary>
    public sealed class WardenAura : MonoBehaviour
    {
        private AshWardenBoss boss;
        private FlameMesh behind, front;
        private float windupSince = -1f;

        public static WardenAura Attach(AshWardenBoss boss)
        {
            var aura = new GameObject("Warden aura").AddComponent<WardenAura>();
            aura.transform.SetParent(boss.Boss.Enemy.Run.ProjectileRoot, false);
            aura.boss = boss;
            aura.behind = new FlameMesh(aura.gameObject, 3) { Pixelated = true };
            // A second renderer needs its own object; it sits in front of the body (4) and its detail layer (5).
            var frontObject = new GameObject("Warden aura front");
            frontObject.transform.SetParent(aura.transform, false);
            aura.front = new FlameMesh(frontObject, 6) { Pixelated = true };
            return aura;
        }

        private void LateUpdate()
        {
            if (boss == null || boss.Boss.Enemy.Health <= 0) { Destroy(gameObject); return; }
            Vector2 body = boss.transform.position;
            float time = Time.time, scale = AshWardenBoss.Size / 1.8f;
            bool enraged = boss.Boss.IsEnraged, casting = boss.IsCharging;
            // Guests do not know the host's windup timer, so the gather animation runs on local time.
            if (casting && windupSince < 0f) windupSince = time;
            else if (!casting) windupSince = -1f;
            float gather = casting ? Mathf.Clamp01((time - windupSince) / 0.85f) : 0f;
            Color rune = enraged ? AshWardenBoss.Glacier : AshWardenBoss.Frost;

            behind.Begin();
            Vector2 feet = body + Vector2.down * 0.85f * scale;
            behind.Ellipse(feet, 1.1f * scale, 0.35f * scale, FlameMesh.Alpha(Color.black, 0.45f), FlameMesh.Alpha(Color.black, 0f));
            // The rune circle turns slowly, and quickly while the Warden casts.
            float spin = time * (casting ? 2.4f : 0.5f);
            float runeRadius = 1.35f * scale;
            behind.Ring(feet, runeRadius, 0.06f, FlameMesh.Alpha(rune, 0.35f + 0.35f * gather), 56);
            behind.Ring(feet, runeRadius * 0.72f, 0.04f, FlameMesh.Alpha(AbilityCatalog.Gold, 0.2f + 0.4f * gather), 48);
            for (int i = 0; i < 8; i++)
            {
                float angle = spin + i * Mathf.PI * 2f / 8f;
                Vector2 spot = feet + new Vector2(Mathf.Cos(angle) * runeRadius, Mathf.Sin(angle) * runeRadius * 0.45f);
                behind.Diamond(spot, 0.1f + 0.05f * gather, FlameMesh.Alpha(AbilityCatalog.Gold, 0.5f + 0.5f * gather));
            }
            behind.Disc(body, 1.2f * scale + 0.3f * gather, FlameMesh.Alpha(rune, 0.22f + 0.25f * gather), FlameMesh.Alpha(rune, 0f));
            // Cold mist rolls off the hem of the robe.
            for (int i = 0; i < 6; i++)
            {
                float seed = FlameMesh.Hash(i, 3.3f), rise = Mathf.Repeat(time * (0.35f + seed * 0.3f) + seed, 1f);
                Vector2 puff = feet + new Vector2((seed - 0.5f) * 1.4f * scale, rise * 1.2f);
                behind.Disc(puff, (0.25f + 0.25f * rise) * scale, FlameMesh.Alpha(new Color(0.75f, 0.9f, 1f), 0.25f * (1f - rise)), FlameMesh.Alpha(Color.white, 0f), 14);
            }
            behind.Commit();

            front.Begin();
            // Snowflakes orbit the Warden; they whirl faster (and there are more of them) once it is bloodied.
            int motes = enraged ? 12 : 8;
            for (int i = 0; i < motes; i++)
            {
                float seed = FlameMesh.Hash(i, 9.1f), angle = time * (enraged ? 2.2f : 1.3f) + i * Mathf.PI * 2f / motes;
                Vector2 p = body + new Vector2(Mathf.Cos(angle) * 1.05f, Mathf.Sin(angle) * 0.45f + 0.1f + Mathf.Sin(time * 2f + i) * 0.08f) * scale;
                // Motes passing behind the body fade out so the orbit reads as 3D.
                float nearSide = Mathf.Sin(angle) < 0f ? 1f : 0.35f;
                front.Diamond(p, (0.07f + seed * 0.04f) * scale, FlameMesh.Alpha(Color.Lerp(AshWardenBoss.Frost, Color.white, seed), 0.85f * nearSide));
            }
            // Frost-lit eyes behind the gold mask.
            float glare = (enraged ? 0.8f : 0.55f) + 0.2f * Mathf.Sin(time * (enraged ? 12f : 4f)) + 0.3f * gather;
            for (int side = -1; side <= 1; side += 2)
                front.Disc(body + new Vector2(side * 0.08f, 0.14f) * AshWardenBoss.Size, 0.12f * scale, FlameMesh.Alpha(Color.white, glare), FlameMesh.Alpha(AshWardenBoss.Frost, 0f), 12);
            // The ice crystal in its hands pulses, and swells as a spell gathers.
            Vector2 hands = body + new Vector2(0f, -0.2f) * AshWardenBoss.Size;
            float orb = (0.18f + 0.04f * Mathf.Sin(time * 6f) + 0.2f * gather) * scale;
            front.Disc(hands, orb * 2.2f, FlameMesh.Alpha(AshWardenBoss.Frost, 0.35f + 0.3f * gather), FlameMesh.Alpha(AshWardenBoss.Glacier, 0f), 20);
            front.Diamond(hands, orb * 1.1f, FlameMesh.Alpha(Color.white, 0.9f));
            if (casting)
            {
                // Sparks stream inward from the rune circle to the hands.
                for (int i = 0; i < 10; i++)
                {
                    float seed = FlameMesh.Hash(i, 1.9f), t = Mathf.Repeat(time * 1.6f + seed, 1f);
                    Vector2 from = hands + FlameMesh.Polar(seed * 40f + time * 0.5f, 1.9f * scale);
                    front.Diamond(Vector2.Lerp(from, hands, t * t), 0.06f + 0.05f * t, FlameMesh.Alpha(Color.Lerp(AshWardenBoss.Frost, Color.white, t), t));
                }
            }
            if (enraged)
                // Icicles jut from the crown once it is bloodied.
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 root = body + new Vector2(i * 0.12f, 0.46f - Mathf.Abs(i) * 0.05f) * AshWardenBoss.Size;
                    Vector2 up = (Vector2.up + Vector2.right * i * 0.3f).normalized;
                    float length = (0.45f - Mathf.Abs(i) * 0.12f) * scale * (0.9f + 0.1f * Mathf.Sin(time * 5f + i));
                    front.Bar(root, up, length, 0.12f * scale, FlameMesh.Alpha(AshWardenBoss.Frost, 0.9f), FlameMesh.Alpha(Color.white, 0.4f));
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
