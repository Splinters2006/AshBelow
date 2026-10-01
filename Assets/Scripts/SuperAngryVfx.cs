using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Super Angry: for as long as the Brawler is furious, the floor under her is scorched and cracked open, glowing
    /// from below; a great flame roars up behind her inside a ring of smaller ones, arcs of rage crackle over her, a
    /// throbbing anger mark hangs by her head and embers stream up off the floor. It opens on impact frames (for the
    /// Brawler's own player), a burst of spikes, two shockwaves and a pillar of fire, pounds like a pulse while it
    /// lasts, flickers as the last second runs out and goes up in a puff of smoke when it ends. Purely visual.
    /// </summary>
    public sealed class SuperAngryVfx : MonoBehaviour
    {
        private const float FadeOut = 0.4f, OpenTime = 0.5f, ImpactTime = 0.14f, FlameRing = 0.95f;
        private const int Cracks = 10, Flames = 10, Arcs = 5, Embers = 20;
        private const float Tau = Mathf.PI * 2f;
        private static readonly Color Scorch = new Color(0.08f, 0.02f, 0.01f), Red = new Color(1f, 0.12f, 0.08f);
        // Behind the hero (ground, the great flame, far flames) and in front of her (near flames, arcs, embers, the
        // anger mark, the opening burst). The impact frames cover the whole screen, above everything in the world.
        private FlameMesh back, front, overlay;
        private Transform hero;
        private float age, duration;

        /// <param name="impact">Open on full-screen impact frames (only for the player who is the Brawler).</param>
        public static SuperAngryVfx Play(Transform root, Transform hero, float duration, bool impact = false)
        {
            if (root == null || hero == null) return null;
            var effect = new GameObject("Super Angry").AddComponent<SuperAngryVfx>();
            effect.transform.SetParent(root, false);
            effect.hero = hero;
            effect.duration = Mathf.Max(0.5f, duration);
            effect.back = new FlameMesh(effect.gameObject, 2);
            var near = new GameObject("Super Angry (front)");
            near.transform.SetParent(effect.transform, false);
            effect.front = new FlameMesh(near, 9);
            if (impact)
            {
                var screen = new GameObject("Super Angry (impact frames)");
                screen.transform.SetParent(effect.transform, false);
                effect.overlay = new FlameMesh(screen, 30);
            }
            return effect;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (hero == null || age >= duration + FadeOut) { Destroy(gameObject); return; }
            float appear = Mathf.Clamp01(age / 0.25f);
            float fade = appear * Mathf.Clamp01((duration + FadeOut - age) / FadeOut);
            // The last second gutters like a candle.
            if (duration - age < 1f) fade *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(age * 16f));
            Vector2 center = hero.position;
            Color fury = HeroBuffs.FuryColor;

            back.Begin();
            front.Begin();
            DrawGround(center, appear, fade, fury);
            // The great flame at her back, and a hot glow around her.
            back.Flame(center + Vector2.down * 0.45f, Vector2.up, 1.5f * appear, 2.5f * appear, 0.37f, 0.85f * fade);
            back.Ellipse(center + Vector2.up * 0.3f, 1.2f, 1.5f, FlameMesh.Alpha(fury, 0.35f * fade), FlameMesh.Alpha(Red, 0f), 24);
            for (int i = 0; i < Flames; i++)
            {
                float a = age * 0.9f + i * Tau / Flames;
                // The far half of the ring burns behind her.
                bool far = Mathf.Sin(a) > 0f;
                Vector2 foot = center + new Vector2(Mathf.Cos(a) * FlameRing, Mathf.Sin(a) * FlameRing * 0.5f - 0.35f);
                (far ? back : front).Flame(foot, Vector2.up, 0.3f, (far ? 0.75f : 0.55f) * appear, i * 0.173f, fade * (far ? 0.9f : 0.75f));
            }
            DrawArcs(center, fade, fury);
            DrawEmbers(center, fade, fury);
            DrawAngerMark(center + new Vector2(0.48f, 0.95f), fade);
            if (age < OpenTime) DrawOpening(center, age / OpenTime, fury);
            if (age > duration) DrawEnding(center, (age - duration) / FadeOut, fury);
            back.Commit();
            front.Commit();
            if (overlay == null) return;
            overlay.Begin();
            if (age < ImpactTime) DrawImpact(center, age / ImpactTime, fury);
            overlay.Commit();
        }

        /// <summary>Scorched floor split by glowing cracks, inside a ring of heat that pounds outward twice a second.</summary>
        private void DrawGround(Vector2 center, float appear, float fade, Color fury)
        {
            Vector2 feet = center + Vector2.down * 0.35f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 12f);
            back.Disc(feet, 2.3f * appear, FlameMesh.Alpha(Scorch, 0.65f * fade), FlameMesh.Alpha(Red, 0f), 40);
            back.Disc(feet, 1.1f * appear, FlameMesh.Alpha(FlameMesh.Yellow, (0.3f + 0.15f * pulse) * fade), FlameMesh.Alpha(fury, 0f), 32);
            float grow = 1f - (1f - Mathf.Clamp01(age / 0.3f)) * (1f - Mathf.Clamp01(age / 0.3f));
            for (int i = 0; i < Cracks; i++)
            {
                float a = (i + FlameMesh.Hash(i, 5.3f) * 0.6f) * Tau / Cracks, length = (1.6f + 1.2f * FlameMesh.Hash(i, 8.8f)) * grow;
                Vector2 at = feet + FlameMesh.Polar(a, 0.35f);
                for (int step = 0; step < 5; step++)
                {
                    float bend = a + (FlameMesh.Hash(i, step + 0.7f) - 0.5f) * 1.3f, taper = 1f - step / 5f;
                    Vector2 dir = FlameMesh.Polar(bend, 1f);
                    float glow = (0.5f + 0.4f * pulse) * fade;
                    back.Bar(at, dir, length / 5f, 0.16f * taper + 0.03f, FlameMesh.Alpha(Red, 0.6f * glow * taper), FlameMesh.Alpha(Red, 0.6f * glow * (taper - 0.2f)));
                    back.Bar(at, dir, length / 5f, 0.06f * taper + 0.012f, FlameMesh.Alpha(FlameMesh.Yellow, glow * taper), FlameMesh.Alpha(fury, glow * (taper - 0.2f)));
                    at += dir * length / 5f;
                }
            }
            float beat = Mathf.Repeat(age * 2f, 1f);
            back.Ring(feet, 0.6f + 2.2f * beat, 0.14f * (1f - beat) + 0.01f, FlameMesh.Alpha(fury, 0.75f * (1f - beat) * fade), 56);
        }

        /// <summary>Jagged arcs of rage snapping around her, jumping to a new shape many times a second.</summary>
        private void DrawArcs(Vector2 center, float fade, Color fury)
        {
            float frame = Mathf.Floor(age * 16f);
            for (int i = 0; i < Arcs; i++)
            {
                // Each arc is only there for some of the frames.
                if (FlameMesh.Hash(i + 0.5f, frame) < 0.35f) continue;
                float a = FlameMesh.Hash(i, frame) * Tau;
                Vector2 at = center + new Vector2(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.45f + 0.2f);
                for (int step = 0; step < 4; step++)
                {
                    Vector2 dir = FlameMesh.Polar(a + (FlameMesh.Hash(i * 7 + step, frame + 3.1f) - 0.5f) * 2.2f, 1f);
                    float length = 0.22f + 0.2f * FlameMesh.Hash(i * 3 + step, frame + 1.7f);
                    front.Bar(at, dir, length, 0.1f, FlameMesh.Alpha(fury, 0.5f * fade), FlameMesh.Alpha(fury, 0.5f * fade));
                    front.Bar(at, dir, length, 0.035f, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(FlameMesh.Core, fade));
                    at += dir * length;
                }
            }
        }

        /// <summary>Embers torn off the floor, streaking up past her.</summary>
        private void DrawEmbers(Vector2 center, float fade, Color fury)
        {
            for (int i = 0; i < Embers; i++)
            {
                float seed = FlameMesh.Hash(i, 1.3f);
                float phase = Mathf.Repeat(age * (0.7f + 0.7f * seed) + FlameMesh.Hash(i, 7.1f), 1f);
                float glow = Mathf.Sin(phase * Mathf.PI) * fade;
                Vector2 at = center + new Vector2((FlameMesh.Hash(i, 3.7f) - 0.5f) * 2.6f + Mathf.Sin(age * 3f + i) * 0.1f, -0.5f + phase * 3f);
                front.Bar(at, Vector2.down, 0.3f + 0.3f * seed, 0.05f, FlameMesh.Alpha(i % 3 == 0 ? FlameMesh.Yellow : fury, 0.8f * glow), FlameMesh.Alpha(Red, 0f));
                front.Diamond(at, 0.04f + 0.04f * seed, FlameMesh.Alpha(i % 4 == 0 ? Color.white : FlameMesh.Yellow, glow));
            }
        }

        /// <summary>The anger mark: four bent veins around an empty middle, throbbing by her head.</summary>
        private void DrawAngerMark(Vector2 at, float fade)
        {
            float throb = 1f + 0.25f * Mathf.Abs(Mathf.Sin(age * 9f));
            float gap = 0.05f * throb, arm = 0.15f * throb;
            Color red = FlameMesh.Alpha(Red, fade), dark = FlameMesh.Alpha(Scorch, 0.9f * fade), hot = FlameMesh.Alpha(FlameMesh.Core, fade);
            for (int pass = 0; pass < 3; pass++)
            {
                // A dark outline, the red vein, then a hot line down its middle.
                float width = pass == 0 ? 0.13f : pass == 1 ? 0.08f : 0.025f;
                Color color = pass == 0 ? dark : pass == 1 ? red : hot;
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        Vector2 corner = at + new Vector2(sx * gap, sy * gap);
                        front.Bar(corner - new Vector2(sx, 0f) * width * 0.5f, new Vector2(sx, 0f), arm + width * 0.5f, width, color, color);
                        front.Bar(corner - new Vector2(0f, sy) * width * 0.5f, new Vector2(0f, sy), arm + width * 0.5f, width, color, color);
                    }
            }
        }

        /// <summary>The moment she snaps: a burst of spikes, two shockwaves and a pillar of fire.</summary>
        private void DrawOpening(Vector2 center, float t, Color fury)
        {
            float rest = 1f - t;
            front.Ring(center, 0.5f + 5f * t, 0.35f * rest + 0.02f, FlameMesh.Alpha(fury, rest), FlameMesh.Alpha(Red, 0f), 64);
            front.Ring(center, 0.3f + 3.2f * t, 0.16f * rest + 0.02f, FlameMesh.Alpha(Color.white, 0.85f * rest), 56);
            front.Bar(center + Vector2.down * 0.4f, Vector2.up, 7f, 1.5f * rest * rest, FlameMesh.Alpha(FlameMesh.Core, 0.9f * rest), FlameMesh.Alpha(fury, 0f));
            front.Bar(center + Vector2.down * 0.4f, Vector2.up, 7f, 2.6f * rest, FlameMesh.Alpha(fury, 0.4f * rest), FlameMesh.Alpha(Red, 0f));
            for (int i = 0; i < 16; i++)
            {
                Vector2 dir = FlameMesh.Polar((i + FlameMesh.Hash(i, 2.1f) * 0.6f) * Tau / 16f, 1f), across = Vector2.Perpendicular(dir) * 0.18f * rest;
                float near = 0.5f + 1.5f * t, far = near + (1.2f + 1.6f * FlameMesh.Hash(i, 6.6f)) * (0.4f + t);
                Color root = FlameMesh.Alpha(i % 2 == 0 ? FlameMesh.Yellow : fury, rest);
                front.Triangle(center + dir * near - across, center + dir * far, center + dir * near + across, root, FlameMesh.Alpha(Color.white, rest), root);
            }
        }

        /// <summary>The moment it burns out: the heat is drawn back in and leaves a puff of smoke.</summary>
        private void DrawEnding(Vector2 center, float t, Color fury)
        {
            float rest = 1f - Mathf.Clamp01(t);
            front.Ring(center, 0.2f + 2.6f * rest, 0.2f * rest + 0.02f, FlameMesh.Alpha(fury, rest), FlameMesh.Alpha(Red, 0f), 56);
            front.Disc(center + Vector2.up * (0.3f + 0.8f * t), 0.5f + 0.7f * t, FlameMesh.Alpha(Scorch, 0.5f * rest), FlameMesh.Alpha(Scorch, 0f), 24);
        }

        /// <summary>
        /// Impact frames: three hard frames with no blending between them. A black screen split by a red burst, the
        /// same in black on white, then white on orange as it lets go, with the anger mark stamped huge over her.
        /// </summary>
        private void DrawImpact(Vector2 center, float t, Color fury)
        {
            int frame = t < 0.4f ? 0 : t < 0.7f ? 1 : 2;
            Color ground = frame == 0 ? Color.black : frame == 1 ? Color.white : FlameMesh.Alpha(fury, 0.7f * (1f - (t - 0.7f) / 0.3f));
            Color ink = frame == 0 ? Red : frame == 1 ? Color.black : Color.white;
            var view = Camera.main;
            Vector2 middle = view != null ? (Vector2)view.transform.position : center;
            overlay.Rect(middle - new Vector2(60f, 40f), middle + new Vector2(60f, 40f), ground);
            for (int i = 0; i < 28; i++)
            {
                float angle = (i + FlameMesh.Hash(i, frame + 1.3f)) / 28f * Tau, near = 2.2f + 2.5f * FlameMesh.Hash(i, frame + 5.9f);
                Vector2 dir = FlameMesh.Polar(angle, 1f), across = Vector2.Perpendicular(dir) * (0.8f + 2f * FlameMesh.Hash(i, 2.2f));
                overlay.Triangle(center + dir * near, center + dir * 45f + across, center + dir * 45f - across, ink, ink, ink);
            }
            float size = frame == 0 ? 1f : frame == 1 ? 1.15f : 1.3f;
            Vector2 mark = center + new Vector2(1.1f, 1.5f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector2 corner = mark + new Vector2(sx, sy) * 0.22f * size;
                    overlay.Bar(corner - new Vector2(sx, 0f) * 0.14f * size, new Vector2(sx, 0f), 0.85f * size, 0.28f * size, ink, ink);
                    overlay.Bar(corner - new Vector2(0f, sy) * 0.14f * size, new Vector2(0f, sy), 0.85f * size, 0.28f * size, ink, ink);
                }
        }

        private void OnDestroy()
        {
            back?.Release();
            front?.Release();
            overlay?.Release();
        }
    }
}
