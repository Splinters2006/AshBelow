using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Avatar of Death: for as long as the Reaper is the incarnation of death, the ground under him goes dark inside a
    /// turning soul-fire sigil (a hexagram inside a spiked crown, the floor cracked open around it), a hooded skull
    /// with burning eyes looms over his shoulders on wings of bone with a great scythe at its back, spectral scythe
    /// blades circle him (passing behind and in front), and souls stream up off the floor. It opens on impact frames
    /// (for the Reaper's own player), a shockwave and a pillar of light, beats like a heart while it lasts, flickers
    /// as the last second runs out and collapses in on him when it ends. Purely visual.
    /// </summary>
    public sealed class AvatarOfDeathVfx : MonoBehaviour
    {
        private const float FadeOut = 0.4f, ImpactTime = 0.2f, SigilRadius = 1.5f, OrbitWidth = 1.25f, OrbitDepth = 0.6f, SkullHeight = 1.6f;
        private const int Blades = 3, Wisps = 16, Ticks = 12, Cracks = 9, Feathers = 5;
        private const float Tau = Mathf.PI * 2f;
        private static readonly Color Dark = new Color(0.02f, 0.05f, 0.05f);
        // Behind the hero (ground, cloak, skull, far blades) and in front of him (near blades, souls, the opening flash).
        // The impact frames cover the whole screen, above everything in the world.
        private FlameMesh back, front, overlay;
        private Transform hero;
        private float age, duration;

        /// <param name="impact">Open on full-screen impact frames (only for the player who is the Reaper).</param>
        public static AvatarOfDeathVfx Play(Transform root, Transform hero, float duration, bool impact = false)
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
            if (impact)
            {
                var screen = new GameObject("Avatar of Death (impact frames)");
                screen.transform.SetParent(effect.transform, false);
                effect.overlay = new FlameMesh(screen, 30);
            }
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
            DrawWings(center + Vector2.up * SkullHeight, appear, fade, soul, bone, shade);
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
            if (age > duration) DrawEnding(center, (age - duration) / FadeOut, soul, bone);
            back.Commit();
            front.Commit();
            if (overlay == null) return;
            overlay.Begin();
            if (age < ImpactTime) DrawImpact(center, age / ImpactTime, soul);
            overlay.Commit();
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
            // A hexagram turning the other way inside it.
            float inner = SigilRadius * 0.74f * appear;
            for (int i = 0; i < 6; i++)
            {
                Vector2 from = center + FlameMesh.Polar(-age * 0.5f + i * Tau / 6f, inner), to = center + FlameMesh.Polar(-age * 0.5f + (i + 2) * Tau / 6f, inner);
                back.Bar(from, (to - from).normalized, Vector2.Distance(from, to), 0.035f, FlameMesh.Alpha(soul, 0.55f * fade), FlameMesh.Alpha(bone, 0.55f * fade));
            }
            // A crown of spikes on the outer ring, and a column of soul-light standing on every third one.
            for (int i = 0; i < Ticks; i++)
            {
                float a = age * 0.8f + (i + 0.5f) * Tau / Ticks;
                Vector2 outward = FlameMesh.Polar(a, 1f), across = Vector2.Perpendicular(outward) * 0.09f, foot = center + outward * SigilRadius * appear;
                back.Triangle(foot - across, foot + outward * (0.3f + 0.12f * pulse), foot + across,
                    FlameMesh.Alpha(soul, 0.8f * fade), FlameMesh.Alpha(soul, 0f), FlameMesh.Alpha(soul, 0.8f * fade));
                if (i % 3 == 0)
                    back.Bar(foot, Vector2.up, 1.5f + 0.5f * Mathf.Sin(age * 5f + i), 0.1f, FlameMesh.Alpha(soul, 0.5f * fade), FlameMesh.Alpha(soul, 0f));
            }
            // The floor splits open around him, soul-fire showing through the cracks.
            float grow = 1f - (1f - Mathf.Clamp01(age / 0.4f)) * (1f - Mathf.Clamp01(age / 0.4f));
            for (int i = 0; i < Cracks; i++)
            {
                float a = (i + FlameMesh.Hash(i, 5.3f) * 0.6f) * Tau / Cracks, length = (1.9f + 1.3f * FlameMesh.Hash(i, 8.8f)) * grow;
                Vector2 at = center + FlameMesh.Polar(a, 0.45f);
                for (int step = 0; step < 5; step++)
                {
                    float bend = a + (FlameMesh.Hash(i, step + 0.7f) - 0.5f) * 1.3f, taper = 1f - step / 5f;
                    Vector2 dir = FlameMesh.Polar(bend, 1f);
                    back.Bar(at, dir, length / 5f, 0.09f * taper + 0.015f, FlameMesh.Alpha(soul, (0.45f + 0.4f * pulse) * taper * fade), FlameMesh.Alpha(soul, (0.45f + 0.4f * pulse) * (taper - 0.2f) * fade));
                    at += dir * length / 5f;
                }
            }
            // And it beats like a heart: a ring rolls out from the sigil every second.
            float beat = Mathf.Repeat(age, 1f);
            back.Ring(center, SigilRadius + 2.4f * beat, 0.1f * (1f - beat) + 0.01f, FlameMesh.Alpha(soul, 0.7f * (1f - beat) * fade), 64);
        }

        /// <summary>Wings of bare bone spread from the specter's shoulders, a tattered membrane between the fingers, and the great scythe slung behind it.</summary>
        private void DrawWings(Vector2 at, float appear, float fade, Color soul, Color bone, Color shade)
        {
            float alpha = fade * appear, flap = Mathf.Sin(age * 2.1f) * 0.14f;
            // The scythe: a long dark shaft from hip to above the hood, its blade hooking over the skull.
            Vector2 butt = at + new Vector2(-1.25f, -1.7f), top = at + new Vector2(1.05f, 1.45f + 0.05f * Mathf.Sin(age * 1.7f)), shaft = (top - butt).normalized;
            back.Bar(butt, shaft, Vector2.Distance(butt, top) * appear, 0.13f, FlameMesh.Alpha(soul, 0.25f * alpha), FlameMesh.Alpha(soul, 0.5f * alpha));
            back.Bar(butt, shaft, Vector2.Distance(butt, top) * appear, 0.07f, FlameMesh.Alpha(Dark, 0.9f * alpha), FlameMesh.Alpha(Dark, 0.9f * alpha));
            Blade(back, top + new Vector2(-0.95f, -0.45f), 1.9f, 1.05f, 0.9f * alpha, soul, bone);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 shoulder = at + new Vector2(side * 0.45f, -0.35f), last = shoulder;
                for (int i = 0; i < Feathers; i++)
                {
                    // Fanned from nearly straight up to drooping outward; the top finger is the longest.
                    float angle = Mathf.Lerp(1.25f, -0.55f, i / (Feathers - 1f)) + flap * (1f - i * 0.15f), length = (2.3f - 0.3f * i) * appear;
                    Vector2 dir = new Vector2(side * Mathf.Cos(angle), Mathf.Sin(angle)), tip = shoulder + dir * length;
                    if (i > 0)
                        back.Triangle(shoulder, last, tip, FlameMesh.Alpha(Dark, 0.6f * alpha), FlameMesh.Alpha(shade, 0.3f * alpha), FlameMesh.Alpha(shade, 0.3f * alpha));
                    back.Bar(shoulder, dir, length, 0.13f, FlameMesh.Alpha(bone, 0.7f * alpha), FlameMesh.Alpha(soul, 0f));
                    back.Bar(shoulder, dir, length * 0.9f, 0.035f, FlameMesh.Alpha(Color.white, 0.85f * alpha), FlameMesh.Alpha(soul, 0f));
                    back.Diamond(tip, 0.06f, FlameMesh.Alpha(soul, 0.8f * alpha));
                    last = tip;
                }
            }
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
            // Shards of bone thrown out ahead of the shockwave.
            for (int i = 0; i < 18; i++)
            {
                Vector2 dir = FlameMesh.Polar((i + FlameMesh.Hash(i, 2.1f)) * Tau / 18f, 1f);
                float speed = 3.5f + 3f * FlameMesh.Hash(i, 6.6f);
                front.Bar(center + dir * (0.6f + speed * t), -dir, 0.9f * rest, 0.07f, FlameMesh.Alpha(i % 3 == 0 ? Color.white : bone, rest), FlameMesh.Alpha(soul, 0f));
            }
        }

        /// <summary>The moment it lets go: everything that poured out of him is drawn back in and snuffed.</summary>
        private void DrawEnding(Vector2 center, float t, Color soul, Color bone)
        {
            float rest = 1f - Mathf.Clamp01(t);
            front.Ring(center, 0.2f + 3.2f * rest, 0.22f * rest + 0.02f, FlameMesh.Alpha(bone, rest), FlameMesh.Alpha(soul, 0f), 56);
            front.Ring(center, 0.1f + 1.8f * rest * rest, 0.1f * rest + 0.02f, FlameMesh.Alpha(soul, rest), 48);
            front.Bar(center + Vector2.down * 0.4f, Vector2.up, 5f, 0.9f * rest, FlameMesh.Alpha(Color.white, 0.7f * rest), FlameMesh.Alpha(soul, 0f));
        }

        /// <summary>
        /// Impact frames: three hard frames with no blending between them. A giant skull stares out of a black screen
        /// with burning eyes, the same in black on white, then white on soul-fire as it lets go; speed lines race in
        /// from the edges of the screen throughout.
        /// </summary>
        private void DrawImpact(Vector2 center, float t, Color soul)
        {
            int frame = t < 0.4f ? 0 : t < 0.7f ? 1 : 2;
            Color ground = frame == 0 ? Color.black : frame == 1 ? Color.white : FlameMesh.Alpha(soul, 0.7f * (1f - (t - 0.7f) / 0.3f));
            Color ink = frame == 1 ? Color.black : Color.white, hole = frame == 1 ? Color.white : frame == 0 ? Color.black : (Color)ReaperAttack.Shade;
            Color eye = frame == 0 ? soul : hole;
            var view = Camera.main;
            Vector2 middle = view != null ? (Vector2)view.transform.position : center;
            overlay.Rect(middle - new Vector2(60f, 40f), middle + new Vector2(60f, 40f), ground);
            Vector2 skull = center + Vector2.up * 1.1f;
            for (int i = 0; i < 26; i++)
            {
                float angle = (i + FlameMesh.Hash(i, frame + 1.3f)) / 26f * Tau, near = 4.2f + 3f * FlameMesh.Hash(i, frame + 5.9f);
                Vector2 dir = FlameMesh.Polar(angle, 1f), across = Vector2.Perpendicular(dir) * (1f + 2.2f * FlameMesh.Hash(i, 2.2f));
                overlay.Triangle(skull + dir * near, skull + dir * 45f + across, skull + dir * 45f - across, ink, ink, ink);
            }
            // The scythe's blade sweeps behind the skull, edge to edge.
            float size = frame == 0 ? 1f : frame == 1 ? 1.12f : 1.2f;
            const int Segments = 20;
            for (int i = 0; i < Segments; i++)
            {
                float u0 = i / (float)Segments, u1 = (i + 1) / (float)Segments, a0 = 0.35f + u0 * 2.9f, a1 = 0.35f + u1 * 2.9f;
                float w0 = 1.3f * Mathf.Pow(u0, 1.4f) * Mathf.Clamp01((1f - u0) / 0.1f), w1 = 1.3f * Mathf.Pow(u1, 1.4f) * Mathf.Clamp01((1f - u1) / 0.1f);
                overlay.Quad(skull + FlameMesh.Polar(a0, (4.4f - w0) * size), skull + FlameMesh.Polar(a0, 4.4f * size), skull + FlameMesh.Polar(a1, 4.4f * size), skull + FlameMesh.Polar(a1, (4.4f - w1) * size),
                    ink, ink, ink, ink);
            }
            // The skull: cranium, cheekbones and jaw, with hollow sockets, nose and teeth.
            overlay.Ellipse(skull + new Vector2(0f, 0.35f) * size, 2.1f * size, 2f * size, ink, ink, 36);
            overlay.Ellipse(skull + new Vector2(0f, -1.3f) * size, 1.3f * size, 1.15f * size, ink, ink, 28);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 socket = skull + new Vector2(side * 0.85f, 0.15f) * size;
                // Slanted sockets, so it scowls.
                overlay.Quad(socket + new Vector2(-side * 0.6f, -0.35f) * size, socket + new Vector2(-side * 0.55f, 0.25f) * size,
                    socket + new Vector2(side * 0.6f, 0.75f) * size, socket + new Vector2(side * 0.5f, -0.45f) * size, hole, hole, hole, hole);
                overlay.Diamond(socket + new Vector2(0f, 0.1f) * size, 0.28f * size, eye);
                if (frame == 0)
                    overlay.Bar(socket + new Vector2(0f, 0.1f) * size, new Vector2(side * 0.45f, 1f).normalized, 2.6f, 0.3f, eye, FlameMesh.Alpha(eye, 0f));
            }
            overlay.Triangle(skull + new Vector2(-0.28f, -0.95f) * size, skull + new Vector2(0f, -0.35f) * size, skull + new Vector2(0.28f, -0.95f) * size, hole, hole, hole);
            for (int i = -3; i <= 3; i++)
                overlay.Bar(skull + new Vector2(i * 0.3f, -1.45f) * size, Vector2.down, 0.75f * size, 0.08f * size, hole, hole);
            overlay.Bar(skull + new Vector2(-1.05f, -1.8f) * size, Vector2.right, 2.1f * size, 0.07f * size, hole, hole);
            // A flat glare through the eyes.
            if (frame == 0) overlay.Bar(skull + new Vector2(-9f, 0.25f), Vector2.right, 18f, 0.08f, FlameMesh.Alpha(soul, 0.9f), FlameMesh.Alpha(soul, 0.9f));
        }

        private void OnDestroy()
        {
            back?.Release();
            front?.Release();
            overlay?.Release();
        }
    }
}
