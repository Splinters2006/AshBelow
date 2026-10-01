using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Avatar of Death: for as long as the Reaper is the incarnation of death, the ground under him goes dark inside a
    /// turning soul-fire sigil, a hooded skull with burning eyes looms over his shoulders, spectral scythe blades
    /// circle him (passing behind and in front), and souls stream up off the floor. It opens with a shockwave and a
    /// pillar of light, and flickers as the last second runs out. Purely visual.
    /// </summary>
    public sealed class AvatarOfDeathVfx : MonoBehaviour
    {
        private const float FadeOut = 0.4f, SigilRadius = 1.5f, OrbitWidth = 1.25f, OrbitDepth = 0.6f, SkullHeight = 1.6f;
        private const int Blades = 3, Wisps = 16, Ticks = 12;
        private const float Tau = Mathf.PI * 2f;
        private static readonly Color Dark = new Color(0.02f, 0.05f, 0.05f);
        // Behind the hero (ground, cloak, skull, far blades) and in front of him (near blades, souls, the opening flash).
        private FlameMesh back, front;
        private Transform hero;
        private float age, duration;

        public static AvatarOfDeathVfx Play(Transform root, Transform hero, float duration)
        {
            if (root == null || hero == null) return null;
            var effect = new GameObject("Avatar of Death").AddComponent<AvatarOfDeathVfx>();
            effect.transform.SetParent(root, false);
            effect.hero = hero;
            effect.duration = Mathf.Max(0.5f, duration);
            effect.back = new FlameMesh(effect.gameObject, 2);
            var near = new GameObject("Avatar of Death (front)");
            near.transform.SetParent(effect.transform, false);
            effect.front = new FlameMesh(near, 9);
            return effect;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (hero == null || age >= duration + FadeOut) { Destroy(gameObject); return; }
            float appear = Mathf.Clamp01(age / 0.35f);
            float fade = appear * Mathf.Clamp01((duration + FadeOut - age) / FadeOut);
            // The last second gutters like a candle.
            if (duration - age < 1f) fade *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(age * 16f));
            Vector2 center = hero.position;
            Color soul = ReaperAttack.Soul, bone = ReaperAttack.Bone, shade = ReaperAttack.Shade;

            back.Begin();
            front.Begin();
            DrawGround(center, appear, fade, soul, bone, shade);
            DrawSpecter(center + Vector2.up * (SkullHeight + 0.07f * Mathf.Sin(age * 2.2f)), appear, fade, soul, bone, shade);
            for (int i = 0; i < Blades; i++)
            {
                float a = age * 3.1f + i * Tau / Blades;
                Vector2 at = center + new Vector2(Mathf.Cos(a) * OrbitWidth, Mathf.Sin(a) * OrbitDepth + 0.1f);
                // The far half of the orbit passes behind him, and looks smaller for it.
                bool far = Mathf.Sin(a) > 0f;
                Blade(far ? back : front, at, a + Mathf.PI * 0.5f + age * 6f, far ? 0.3f : 0.38f, fade * (far ? 0.7f : 1f), soul, bone);
            }
            DrawSouls(center, fade, soul);
            if (age < 0.55f) DrawOpening(center, age / 0.55f, soul, bone);
            back.Commit();
            front.Commit();
        }

        /// <summary>A pool of darkness and the sigil turning on it: two rings, a wheel of ticks and a counter-turning circle of sparks.</summary>
        private void DrawGround(Vector2 center, float appear, float fade, Color soul, Color bone, Color shade)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 4f);
            back.Disc(center, 2.5f * appear, FlameMesh.Alpha(Dark, 0.6f * fade), FlameMesh.Alpha(shade, 0f), 40);
            back.Ring(center, SigilRadius * appear, 0.06f, FlameMesh.Alpha(soul, (0.6f + 0.3f * pulse) * fade), 64);
            back.Ring(center, SigilRadius * 0.74f * appear, 0.03f, FlameMesh.Alpha(bone, 0.45f * fade), 56);
            for (int i = 0; i < Ticks; i++)
            {
                float a = age * 0.8f + i * Tau / Ticks;
                Vector2 outward = FlameMesh.Polar(a, 1f);
                back.Bar(center + outward * SigilRadius * 0.8f * appear, outward, SigilRadius * 0.17f, i % 3 == 0 ? 0.07f : 0.035f,
                    FlameMesh.Alpha(soul, 0.8f * fade), FlameMesh.Alpha(soul, 0f));
            }
            for (int i = 0; i < 6; i++)
                back.Diamond(center + FlameMesh.Polar(-age * 1.3f + i * Tau / 6f, SigilRadius * appear), 0.07f + 0.03f * pulse, FlameMesh.Alpha(bone, fade));
        }

        /// <summary>Death looking over his shoulder: a dark hood and ragged cloak, a pale skull and two eyes of soul-fire.</summary>
        private void DrawSpecter(Vector2 at, float appear, float fade, Color soul, Color bone, Color shade)
        {
            float alpha = fade * appear;
            Color cloth = FlameMesh.Alpha(Dark, 0.75f * alpha), hem = FlameMesh.Alpha(shade, 0f);
            // The cloak hangs in tatters that sway, fading out before they reach the ground.
            for (int i = -3; i <= 3; i++)
            {
                float sway = Mathf.Sin(age * 2.6f + i * 1.3f) * 0.07f;
                back.Bar(at + new Vector2(i * 0.22f, -0.45f), new Vector2(sway, -1f).normalized, 1.05f + 0.35f * FlameMesh.Hash(i, 2.9f), 0.24f,
                    FlameMesh.Alpha(shade, 0.5f * alpha), hem);
            }
            // The hood: a dark peak with a soul-lit rim.
            back.Triangle(at + new Vector2(-0.8f, -0.55f), at + new Vector2(0f, 0.95f), at + new Vector2(0.8f, -0.55f),
                FlameMesh.Alpha(soul, 0.35f * alpha), FlameMesh.Alpha(soul, 0.5f * alpha), FlameMesh.Alpha(soul, 0.35f * alpha));
            back.Triangle(at + new Vector2(-0.72f, -0.52f), at + new Vector2(0f, 0.85f), at + new Vector2(0.72f, -0.52f), cloth, cloth, cloth);
            // The skull inside it: cranium, jaw, sockets, nose and a row of teeth.
            Color pale = FlameMesh.Alpha(bone, 0.9f * alpha), dim = FlameMesh.Alpha(new Color(0.6f, 0.62f, 0.56f), 0.85f * alpha);
            Color hollow = FlameMesh.Alpha(Color.black, 0.95f * alpha);
            back.Ellipse(at + new Vector2(0f, -0.22f), 0.25f, 0.2f, dim, dim, 20);
            back.Ellipse(at + new Vector2(0f, 0.06f), 0.4f, 0.4f, pale, dim, 28);
            back.Triangle(at + new Vector2(-0.05f, -0.16f), at + new Vector2(0f, -0.04f), at + new Vector2(0.05f, -0.16f), hollow, hollow, hollow);
            for (int i = -2; i <= 2; i++)
                back.Bar(at + new Vector2(i * 0.08f, -0.24f), Vector2.down, 0.12f, 0.02f, hollow, hollow);
            float flare = 0.5f + 0.5f * Mathf.Sin(age * 9f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 socket = at + new Vector2(side * 0.16f, 0.08f);
                back.Ellipse(socket, 0.11f, 0.13f, hollow, hollow, 16);
                back.Ellipse(socket, 0.2f, 0.22f, FlameMesh.Alpha(soul, 0.35f * alpha), FlameMesh.Alpha(soul, 0f), 16);
                // Each eye burns, its flame trailing up and outward.
                back.Bar(socket, new Vector2(side * 0.35f, 1f).normalized, 0.38f + 0.12f * flare, 0.09f, FlameMesh.Alpha(soul, 0.9f * alpha), FlameMesh.Alpha(soul, 0f));
                back.Diamond(socket, 0.065f + 0.02f * flare, FlameMesh.Alpha(Color.white, alpha));
            }
        }

        /// <summary>A spectral scythe blade: a crescent, thick in the middle and sharp at both ends, with a bright edge.</summary>
        private static void Blade(FlameMesh mesh, Vector2 at, float facing, float size, float alpha, Color soul, Color bone)
        {
            const int Segments = 8;
            const float Arc = 3.4f;
            Color edge = FlameMesh.Alpha(Color.white, alpha), body = FlameMesh.Alpha(bone, 0.85f * alpha), glow = FlameMesh.Alpha(soul, 0.4f * alpha);
            mesh.Ellipse(at, size * 1.3f, size * 1.3f, glow, FlameMesh.Alpha(soul, 0f), 16);
            for (int i = 0; i < Segments; i++)
            {
                float u0 = i / (float)Segments, u1 = (i + 1) / (float)Segments;
                float a0 = facing + (u0 - 0.5f) * Arc, a1 = facing + (u1 - 0.5f) * Arc;
                float w0 = size * 0.42f * Mathf.Sin(u0 * Mathf.PI), w1 = size * 0.42f * Mathf.Sin(u1 * Mathf.PI);
                mesh.Quad(at + FlameMesh.Polar(a0, size - w0), at + FlameMesh.Polar(a0, size), at + FlameMesh.Polar(a1, size), at + FlameMesh.Polar(a1, size - w1),
                    body, edge, edge, body);
            }
        }

        /// <summary>Souls torn loose from the floor, drifting up past him and winking out.</summary>
        private void DrawSouls(Vector2 center, float fade, Color soul)
        {
            for (int i = 0; i < Wisps; i++)
            {
                float seed = FlameMesh.Hash(i, 1.3f);
                float phase = Mathf.Repeat(age * (0.35f + 0.35f * seed) + FlameMesh.Hash(i, 7.1f), 1f);
                float glow = Mathf.Sin(phase * Mathf.PI) * fade;
                Vector2 at = center + new Vector2((FlameMesh.Hash(i, 3.7f) - 0.5f) * 2.6f + Mathf.Sin(age * 2f + i) * 0.12f, -0.5f + phase * 2.6f);
                front.Bar(at, Vector2.down, 0.22f + 0.2f * seed, 0.05f, FlameMesh.Alpha(soul, 0.7f * glow), FlameMesh.Alpha(soul, 0f));
                front.Diamond(at, 0.05f + 0.04f * seed, FlameMesh.Alpha(i % 4 == 0 ? Color.white : soul, glow));
            }
        }

        /// <summary>The moment of becoming: two shockwaves race outward and a pillar of light collapses onto him.</summary>
        private void DrawOpening(Vector2 center, float t, Color soul, Color bone)
        {
            float rest = 1f - t;
            front.Ring(center, 0.5f + 5f * t, 0.3f * rest + 0.02f, FlameMesh.Alpha(soul, rest), FlameMesh.Alpha(soul, 0f), 64);
            front.Ring(center, 0.3f + 3.2f * t, 0.14f * rest + 0.02f, FlameMesh.Alpha(bone, 0.8f * rest), 56);
            front.Bar(center + Vector2.down * 0.4f, Vector2.up, 7f, 1.4f * rest * rest, FlameMesh.Alpha(Color.white, 0.85f * rest), FlameMesh.Alpha(soul, 0f));
            front.Bar(center + Vector2.down * 0.4f, Vector2.up, 7f, 2.4f * rest, FlameMesh.Alpha(soul, 0.35f * rest), FlameMesh.Alpha(soul, 0f));
        }

        private void OnDestroy()
        {
            back?.Release();
            front?.Release();
        }
    }
}
