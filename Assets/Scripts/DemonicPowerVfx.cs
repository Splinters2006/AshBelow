using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Demonic Power: for as long as the Demoness's tail is split in two, a summoning circle turns on the ground under
    /// her: a band of burning runes between two rings, a pentagram turning against it with dark flames on its points
    /// and a second, smaller band of runes around her feet. A pair of horns of soulfire stands over her head, runes
    /// circle her (passing behind and in front) and embers stream up off the floor. With Dread Presence the edge of
    /// the aura is a great ring of runes of its own, crowned with spikes, a wave of light running around it. It opens
    /// on a shockwave that throws runes outward and a pillar of light, beats like a heart while it lasts, flickers as
    /// the last second runs out and collapses in on her when it ends. Purely visual.
    /// </summary>
    public sealed class DemonicPowerVfx : MonoBehaviour
    {
        private const float FadeOut = 0.4f, OpenTime = 0.55f, SigilRadius = 1.5f, BandHeight = 0.2f, OrbitWidth = 1.15f, OrbitDepth = 0.5f;
        private const int SigilRunes = 16, FeetRunes = 8, Orbiters = 5, Embers = 18;
        private const float Tau = Mathf.PI * 2f;
        private static readonly Color Soulfire = new Color(0.6f, 0.12f, 1f);
        // Behind the hero (ground, horns, far runes) and in front of her (near runes, embers, the opening flash).
        private FlameMesh back, front;
        private Transform hero;
        private float age, duration, auraRadius;

        /// <param name="auraRadius">Dread Presence's reach, or zero without the upgrade.</param>
        public static DemonicPowerVfx Play(Transform root, Transform hero, float duration, float auraRadius = 0f)
        {
            if (root == null || hero == null) return null;
            var effect = new GameObject("Demonic Power").AddComponent<DemonicPowerVfx>();
            effect.transform.SetParent(root, false);
            effect.hero = hero;
            effect.duration = Mathf.Max(0.5f, duration);
            effect.auraRadius = auraRadius;
            effect.back = new FlameMesh(effect.gameObject, 2);
            var near = new GameObject("Demonic Power (front)");
            near.transform.SetParent(effect.transform, false);
            effect.front = new FlameMesh(near, 9);
            return effect;
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (hero == null || age >= duration + FadeOut) { Destroy(gameObject); return; }
            float appear = EaseOut(age / 0.35f);
            float fade = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((duration + FadeOut - age) / FadeOut);
            // The last second gutters like a candle.
            if (duration - age < 1f) fade *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(age * 16f));
            Vector2 center = hero.position;

            back.Begin();
            front.Begin();
            DrawGround(center, appear, fade);
            if (auraRadius > 0f) DrawDreadRing(center, appear, fade);
            DrawHorns(center + Vector2.up * 0.55f, appear, fade);
            for (int i = 0; i < Orbiters; i++)
            {
                float a = age * 2.3f + i * Tau / Orbiters;
                Vector2 at = center + new Vector2(Mathf.Cos(a) * OrbitWidth, Mathf.Sin(a) * OrbitDepth + 0.45f + 0.08f * Mathf.Sin(age * 3f + i * 1.9f));
                // The far half of the orbit passes behind her, and looks smaller for it.
                bool far = Mathf.Sin(a) > 0f;
                float glow = fade * appear * (far ? 0.6f : 1f);
                var layer = far ? back : front;
                layer.Ellipse(at, 0.28f, 0.28f, FlameMesh.Alpha(Soulfire, 0.4f * glow), FlameMesh.Alpha(Soulfire, 0f), 14);
                PentagramVfx.Rune(layer, i * 3 + 1, at, Vector2.up, far ? 0.26f : 0.34f, 0.03f, FlameMesh.Alpha(DemonessAttack.Violet, 0.6f * glow), FlameMesh.Alpha(DemonessAttack.Pale, glow));
            }
            DrawEmbers(center, fade);
            if (age < OpenTime) DrawOpening(center, age / OpenTime);
            if (age > duration) DrawEnding(center, (age - duration) / FadeOut);
            back.Commit();
            front.Commit();
        }

        /// <summary>A pool of darkness and the summoning circle turning on it.</summary>
        private void DrawGround(Vector2 center, float appear, float fade)
        {
            Color violet = DemonessAttack.Violet, pale = DemonessAttack.Pale;
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 4f), r = SigilRadius * appear;
            back.Disc(center, 2.4f * appear, FlameMesh.Alpha(DemonessAttack.Abyss, 0.6f * fade), FlameMesh.Alpha(violet, 0f), 40);
            RuneBand(center, r, BandHeight, SigilRunes, age * 0.5f, fade, 0f);
            // The pentagram inside the band, turning the other way, a dark flame burning on each of its points.
            float inner = r - BandHeight * 1.6f, spin = -age * 0.4f + Mathf.PI / 2f;
            for (int i = 0; i < 5; i++)
            {
                Vector2 from = center + FlameMesh.Polar(spin + i * Tau / 5f, inner), to = center + FlameMesh.Polar(spin + (i + 2) * Tau / 5f, inner);
                Vector2 direction = (to - from).normalized;
                float length = Vector2.Distance(from, to);
                back.Bar(from, direction, length, 0.13f, FlameMesh.Alpha(violet, 0.45f * fade), FlameMesh.Alpha(violet, 0.45f * fade));
                back.Bar(from, direction, length, 0.04f, FlameMesh.Alpha(pale, (0.6f + 0.4f * pulse) * fade), FlameMesh.Alpha(pale, (0.6f + 0.4f * pulse) * fade));
            }
            for (int i = 0; i < 5; i++)
            {
                Vector2 tip = center + FlameMesh.Polar(spin + i * Tau / 5f, inner);
                float height = (0.55f + 0.25f * Mathf.Sin(age * 7f + i * 2.1f)) * appear;
                back.Bar(tip, Vector2.up, height, 0.2f, FlameMesh.Alpha(Soulfire, 0.65f * fade), FlameMesh.Alpha(violet, 0f));
                back.Bar(tip, Vector2.up, height * 0.65f, 0.08f, FlameMesh.Alpha(DemonessAttack.Abyss, 0.9f * fade), FlameMesh.Alpha(DemonessAttack.Abyss, 0f));
                back.Diamond(tip, 0.09f + 0.04f * pulse, FlameMesh.Alpha(pale, fade));
            }
            // A small band of runes close around her feet, turning fast.
            RuneBand(center, 0.62f * appear, 0.13f, FeetRunes, -age * 1.3f, fade, 3.7f);
            // And it beats like a heart: a ring rolls out from the circle every second.
            float beat = Mathf.Repeat(age, 1f);
            back.Ring(center, SigilRadius + 1.6f * beat, 0.09f * (1f - beat) + 0.01f, FlameMesh.Alpha(violet, 0.6f * (1f - beat) * fade), 64);
        }

        /// <summary>
        /// A ring of runes: two rings, <paramref name="outer"/> and one <paramref name="height"/> and a bit inside it,
        /// a strip of shadow between them and <paramref name="count"/> runes burning on it. A wave of light chases
        /// around the band, flaring each rune as it passes.
        /// </summary>
        private void RuneBand(Vector2 center, float outer, float height, int count, float spin, float alpha, float seed)
        {
            if (outer <= height * 2f || alpha <= 0f) return;
            Color violet = DemonessAttack.Violet, pale = DemonessAttack.Pale;
            float band = outer - height * 0.8f, innerRing = outer - height * 1.6f;
            int segments = Mathf.Clamp(Mathf.CeilToInt(outer * 22f), 40, 128);
            back.Ring(center, band, height * 1.6f, FlameMesh.Alpha(DemonessAttack.Abyss, 0.5f * alpha), FlameMesh.Alpha(DemonessAttack.Abyss, 0.5f * alpha), segments);
            back.Ring(center, outer, 0.06f, FlameMesh.Alpha(violet, 0.85f * alpha), segments);
            back.Ring(center, innerRing, 0.035f, FlameMesh.Alpha(pale, 0.5f * alpha), segments);
            float wave = Mathf.Repeat(age * 0.45f + seed, 1f);
            for (int i = 0; i < count; i++)
            {
                float a = spin + i * Tau / count;
                Vector2 up = FlameMesh.Polar(a, 1f);
                // How close the wave of light is to this rune, around the ring.
                float behind = Mathf.Repeat(wave - i / (float)count, 1f);
                float lit = Mathf.Clamp01(1f - behind * 5f);
                float glow = alpha * (0.5f + 0.15f * Mathf.Sin(age * 9f + i * 2.3f + seed) + 0.35f * lit);
                PentagramVfx.Rune(back, i + Mathf.RoundToInt(seed * 10f), center + up * band, up, height, 0.03f + 0.02f * lit,
                    FlameMesh.Alpha(Color.Lerp(violet, Soulfire, lit), 0.5f * glow), FlameMesh.Alpha(pale, glow));
                // A tick between each rune and the next.
                Vector2 tick = FlameMesh.Polar(a + Mathf.PI / count, 1f);
                back.Bar(center + tick * innerRing, tick, height * 1.6f, 0.025f, FlameMesh.Alpha(violet, 0.6f * alpha), FlameMesh.Alpha(violet, 0.6f * alpha));
            }
        }

        /// <summary>Dread Presence's edge: a great ring of runes exactly as wide as the aura, with a crown of spikes and lights standing on it.</summary>
        private void DrawDreadRing(Vector2 center, float appear, float fade)
        {
            Color violet = DemonessAttack.Violet;
            float r = auraRadius * appear, pulse = 0.5f + 0.5f * Mathf.Sin(age * 4f);
            int runes = Mathf.Max(12, Mathf.RoundToInt(auraRadius * 7f)), spikes = runes / 2;
            // A faint wash of dread over everything inside.
            back.Ring(center, r * 0.8f, r * 0.4f, FlameMesh.Alpha(violet, 0f), FlameMesh.Alpha(Soulfire, 0.13f * fade), 96);
            RuneBand(center, r, 0.3f, runes, -age * 0.12f, fade * 0.9f, 6.1f);
            for (int i = 0; i < spikes; i++)
            {
                float a = age * 0.12f + i * Tau / spikes;
                Vector2 outward = FlameMesh.Polar(a, 1f), across = Vector2.Perpendicular(outward) * 0.08f, foot = center + outward * r;
                back.Triangle(foot - across, foot + outward * (0.24f + 0.1f * pulse), foot + across,
                    FlameMesh.Alpha(violet, 0.8f * fade), FlameMesh.Alpha(violet, 0f), FlameMesh.Alpha(violet, 0.8f * fade));
                if (i % 3 == 0)
                    back.Bar(foot, Vector2.up, 0.9f + 0.35f * Mathf.Sin(age * 5f + i), 0.1f, FlameMesh.Alpha(Soulfire, 0.45f * fade), FlameMesh.Alpha(Soulfire, 0f));
            }
        }

        /// <summary>Two horns of soulfire curving up over her head.</summary>
        private void DrawHorns(Vector2 at, float appear, float fade)
        {
            const int Segments = 7;
            float alpha = fade * appear, flicker = 0.85f + 0.15f * Mathf.Sin(age * 11f);
            Color root = FlameMesh.Alpha(Soulfire, 0.75f * alpha * flicker), edge = FlameMesh.Alpha(DemonessAttack.Pale, 0.9f * alpha);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 last = HornPoint(at, side, 0f, appear), lastAcross = Vector2.right * 0.14f;
                for (int i = 1; i <= Segments; i++)
                {
                    float u = i / (float)Segments;
                    Vector2 point = HornPoint(at, side, u, appear);
                    Vector2 across = Vector2.Perpendicular((point - last).normalized) * 0.14f * (1f - u);
                    // Pale along the outer curve, soulfire on the inside, fading to nothing at the tip.
                    Color outer = FlameMesh.Alpha(edge, 1f - u * 0.6f), innerColor = FlameMesh.Alpha(root, 1f - u * 0.6f);
                    back.Quad(last - lastAcross * side, last + lastAcross * side, point + across * side, point - across * side, outer, innerColor, innerColor, outer);
                    last = point;
                    lastAcross = across;
                }
                back.Ellipse(HornPoint(at, side, 0.5f, appear), 0.4f, 0.5f, FlameMesh.Alpha(Soulfire, 0.22f * alpha), FlameMesh.Alpha(Soulfire, 0f), 14);
            }
        }

        private static Vector2 HornPoint(Vector2 at, int side, float u, float appear)
            => at + new Vector2(side * (0.2f + 0.42f * Mathf.Sin(u * Mathf.PI * 0.8f)), 0.85f * u) * appear;

        /// <summary>Embers of soulfire shaken loose from the floor, drifting up past her and winking out.</summary>
        private void DrawEmbers(Vector2 center, float fade)
        {
            for (int i = 0; i < Embers; i++)
            {
                float seed = FlameMesh.Hash(i, 1.3f);
                float phase = Mathf.Repeat(age * (0.4f + 0.4f * seed) + FlameMesh.Hash(i, 7.1f), 1f);
                float glow = Mathf.Sin(phase * Mathf.PI) * fade;
                Vector2 at = center + new Vector2((FlameMesh.Hash(i, 3.7f) - 0.5f) * 2.8f + Mathf.Sin(age * 2f + i) * 0.12f, -0.5f + phase * 2.4f);
                front.Bar(at, Vector2.down, 0.2f + 0.18f * seed, 0.05f, FlameMesh.Alpha(Soulfire, 0.7f * glow), FlameMesh.Alpha(Soulfire, 0f));
                front.Diamond(at, 0.045f + 0.04f * seed, FlameMesh.Alpha(i % 4 == 0 ? DemonessAttack.Pale : DemonessAttack.Violet, glow));
            }
        }

        /// <summary>The moment her tail splits: shockwaves race outward, runes are thrown ahead of them and a pillar of light collapses onto her.</summary>
        private void DrawOpening(Vector2 center, float t)
        {
            Color violet = DemonessAttack.Violet, pale = DemonessAttack.Pale;
            float rest = 1f - t, reach = Mathf.Max(4.5f, auraRadius);
            front.Ring(center, 0.5f + reach * t, 0.3f * rest + 0.02f, FlameMesh.Alpha(Soulfire, rest), FlameMesh.Alpha(violet, 0f), 72);
            front.Ring(center, 0.3f + 3f * t, 0.14f * rest + 0.02f, FlameMesh.Alpha(pale, 0.8f * rest), 56);
            front.Bar(center + Vector2.down * 0.4f, Vector2.up, 7f, 1.3f * rest * rest, FlameMesh.Alpha(pale, 0.85f * rest), FlameMesh.Alpha(violet, 0f));
            front.Bar(center + Vector2.down * 0.4f, Vector2.up, 7f, 2.3f * rest, FlameMesh.Alpha(Soulfire, 0.35f * rest), FlameMesh.Alpha(violet, 0f));
            for (int i = 0; i < 12; i++)
            {
                Vector2 dir = FlameMesh.Polar((i + FlameMesh.Hash(i, 2.1f)) * Tau / 12f, 1f);
                Vector2 at = center + dir * (0.6f + (2.5f + 2.5f * FlameMesh.Hash(i, 6.6f)) * EaseOut(t));
                front.Bar(at, -dir, 0.8f * rest, 0.06f, FlameMesh.Alpha(violet, 0.7f * rest), FlameMesh.Alpha(violet, 0f));
                PentagramVfx.Rune(front, i, at, Vector2.up, 0.3f + 0.25f * t, 0.035f, FlameMesh.Alpha(Soulfire, 0.6f * rest), FlameMesh.Alpha(pale, rest));
            }
        }

        /// <summary>The moment it lets go: everything that poured out of her is drawn back in and snuffed.</summary>
        private void DrawEnding(Vector2 center, float t)
        {
            float rest = 1f - Mathf.Clamp01(t);
            front.Ring(center, 0.2f + 3f * rest, 0.2f * rest + 0.02f, FlameMesh.Alpha(DemonessAttack.Pale, rest), FlameMesh.Alpha(Soulfire, 0f), 56);
            front.Ring(center, 0.1f + 1.7f * rest * rest, 0.1f * rest + 0.02f, FlameMesh.Alpha(Soulfire, rest), 48);
            front.Bar(center + Vector2.down * 0.4f, Vector2.up, 4.5f, 0.8f * rest, FlameMesh.Alpha(DemonessAttack.Pale, 0.6f * rest), FlameMesh.Alpha(Soulfire, 0f));
        }

        private static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));

        private void OnDestroy()
        {
            back?.Release();
            front?.Release();
        }
    }
}
