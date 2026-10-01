using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Shield Taunt: the Knight bellows and a ward of rage stands around him for as long as he holds it. The floor
    /// inside darkens, the ward's edge throbs with shield plates wheeling round it, chevrons stream in from outside
    /// to say "come at me", steam boils off him and a pair of exclamation marks pounds over his head. Every bolt the
    /// ward stops flashes where it hit and ripples round the edge; with Retribution, every hit soaked up joins a ring
    /// of sparks circling him, which is what goes off in <see cref="Retribution"/>. Purely visual.
    /// </summary>
    public sealed class ShieldTauntVfx : MonoBehaviour
    {
        private const float FadeOut = 0.3f, OpenTime = 0.4f, BlockTime = 0.3f, BurstTime = 0.55f;
        private const int Chevrons = 10, Plates = 6, Embers = 12, MaxBlocks = 8, MaxPips = 12;
        private const float Tau = Mathf.PI * 2f;
        private static readonly Color Scorch = new Color(0.1f, 0.01f, 0.01f);
        // Behind the hero (floor, ward, chevrons) and in front of him (steam, the mark, block flashes, bursts).
        private FlameMesh back, front;
        private Transform hero;
        private Vector2 origin;
        private float age, duration, radius;
        private bool burst;
        private int hits, blocks;
        private readonly float[] blockAngles = new float[MaxBlocks], blockTimes = new float[MaxBlocks];

        /// <summary>The ward around <paramref name="hero"/>, as wide as the taunt's reach.</summary>
        public static ShieldTauntVfx Play(Transform root, Transform hero, float duration, float radius)
        {
            if (root == null || hero == null) return null;
            var effect = Create(root, "Shield Taunt");
            effect.hero = hero;
            effect.duration = Mathf.Max(0.3f, duration);
            effect.radius = radius;
            return effect;
        }

        /// <summary>Retribution going off: the stored rage blows out to <paramref name="radius"/>, bigger for every hit.</summary>
        public static void Retribution(Transform root, Vector2 center, float radius, int hits)
        {
            if (root == null) return;
            var effect = Create(root, "Retribution");
            effect.burst = true;
            effect.origin = center;
            effect.radius = radius;
            effect.hits = hits;
        }

        private static ShieldTauntVfx Create(Transform root, string name)
        {
            var effect = new GameObject(name).AddComponent<ShieldTauntVfx>();
            effect.transform.SetParent(root, false);
            effect.back = new FlameMesh(effect.gameObject, 2);
            var near = new GameObject(name + " (front)");
            near.transform.SetParent(effect.transform, false);
            effect.front = new FlameMesh(near, 9);
            return effect;
        }

        /// <summary>A bolt stopped on the ward, <paramref name="offset"/> away from the Knight.</summary>
        public void Block(Vector2 offset)
        {
            int slot = blocks++ % MaxBlocks;
            blockAngles[slot] = Mathf.Atan2(offset.y, offset.x);
            blockTimes[slot] = age;
        }

        /// <summary>One more hit stored up for Retribution.</summary>
        public void Charge() => hits++;

        /// <summary>The taunt was cut short; fade out now.</summary>
        public void End() => duration = Mathf.Min(duration, age);

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (burst ? age >= BurstTime : hero == null || age >= duration + FadeOut) { Destroy(gameObject); return; }
            back.Begin();
            front.Begin();
            if (burst) DrawBurst(age / BurstTime);
            else
            {
                float appear = Mathf.Clamp01(age / 0.2f);
                float fade = appear * Mathf.Clamp01((duration + FadeOut - age) / FadeOut);
                // The last half second flickers: the ward is about to drop.
                if (duration - age < 0.5f) fade *= 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(age * 20f));
                Vector2 center = hero.position;
                DrawWard(center, appear, fade);
                DrawSteam(center, fade);
                DrawPips(center, fade);
                DrawMark(center + Vector2.up * 1.05f, fade);
                DrawBlocks(center);
                if (age < OpenTime) DrawOpening(center, age / OpenTime);
                if (age > duration) DrawEnding(center, (age - duration) / FadeOut);
            }
            back.Commit();
            front.Commit();
        }

        /// <summary>Darkened floor inside a throbbing edge, plates wheeling round it and chevrons streaming in.</summary>
        private void DrawWard(Vector2 center, float appear, float fade)
        {
            Color rage = HeroBuffs.TauntColor, edge = Color.Lerp(rage, FlameMesh.Yellow, 0.35f);
            float throb = 0.5f + 0.5f * Mathf.Sin(age * 18f), beat = Mathf.Repeat(age * 2.5f, 1f);
            float r = radius * (1f - (1f - appear) * (1f - appear));
            back.Disc(center, r, FlameMesh.Alpha(Scorch, 0.45f * fade), FlameMesh.Alpha(rage, 0.12f * fade), 48);
            back.Ring(center, r - 0.3f, 0.6f, FlameMesh.Alpha(rage, 0f), FlameMesh.Alpha(rage, (0.3f + 0.15f * throb) * fade), 56);
            back.Ring(center, r, 0.06f + 0.03f * throb, FlameMesh.Alpha(edge, 0.9f * fade), 56);
            // A heartbeat pounding out from him to the edge.
            back.Ring(center, 0.4f + (r - 0.4f) * beat, 0.12f * (1f - beat) + 0.01f, FlameMesh.Alpha(rage, 0.6f * (1f - beat) * fade), 48);
            for (int i = 0; i < Plates; i++)
            {
                float a = age * 1.6f + i * Tau / Plates;
                Arc(back, center, r + 0.13f, a, a + 0.55f, 0.09f, FlameMesh.Alpha(edge, 0.85f * fade));
                Arc(back, center, r + 0.13f, a + 0.08f, a + 0.47f, 0.03f, FlameMesh.Alpha(Color.white, 0.7f * fade));
            }
            for (int i = 0; i < Chevrons; i++)
            {
                float phase = Mathf.Repeat(age * 1.4f + FlameMesh.Hash(i, 4.2f), 1f);
                Vector2 dir = FlameMesh.Polar(i * Tau / Chevrons + 0.3f * FlameMesh.Hash(i, 9.1f), 1f);
                // The point leads inward, the two arms trail behind it.
                Vector2 tip = center + dir * (r + 0.3f + (1f - phase) * r * 0.9f);
                Color bright = FlameMesh.Alpha(rage, 0.75f * Mathf.Sin(phase * Mathf.PI) * fade), clear = FlameMesh.Alpha(rage, 0f);
                for (int side = -1; side <= 1; side += 2)
                    back.Bar(tip, (dir + Vector2.Perpendicular(dir) * side * 0.8f).normalized, 0.32f, 0.07f, bright, clear);
            }
        }

        /// <summary>Steam of rage boiling up off him.</summary>
        private void DrawSteam(Vector2 center, float fade)
        {
            Color rage = HeroBuffs.TauntColor;
            for (int i = 0; i < Embers; i++)
            {
                float seed = FlameMesh.Hash(i, 1.3f);
                float phase = Mathf.Repeat(age * (0.9f + 0.8f * seed) + FlameMesh.Hash(i, 7.1f), 1f);
                float glow = Mathf.Sin(phase * Mathf.PI) * fade;
                Vector2 at = center + new Vector2((FlameMesh.Hash(i, 3.7f) - 0.5f) * 1.1f + Mathf.Sin(age * 4f + i) * 0.06f, 0.2f + phase * 1.6f);
                front.Bar(at, Vector2.down, 0.18f + 0.2f * seed, 0.05f, FlameMesh.Alpha(i % 3 == 0 ? FlameMesh.Yellow : rage, 0.8f * glow), FlameMesh.Alpha(rage, 0f));
            }
        }

        /// <summary>Retribution's stored hits: one spark per hit, circling him.</summary>
        private void DrawPips(Vector2 center, float fade)
        {
            int pips = Mathf.Min(hits, MaxPips);
            for (int i = 0; i < pips; i++)
            {
                float a = age * 3f + i * Tau / pips;
                Vector2 at = center + new Vector2(Mathf.Cos(a) * 0.7f, Mathf.Sin(a) * 0.35f + 0.1f);
                front.Diamond(at, 0.12f, FlameMesh.Alpha(HeroBuffs.TauntColor, 0.55f * fade));
                front.Diamond(at, 0.06f, FlameMesh.Alpha(FlameMesh.Core, fade));
            }
        }

        /// <summary>Two exclamation marks pounding over his head.</summary>
        private void DrawMark(Vector2 at, float fade)
        {
            float throb = 1f + 0.2f * Mathf.Abs(Mathf.Sin(age * 10f));
            for (int pass = 0; pass < 2; pass++)
            {
                // A dark outline, then the mark itself.
                float outline = pass == 0 ? 0.05f : 0f;
                Color color = FlameMesh.Alpha(pass == 0 ? Scorch : Color.Lerp(HeroBuffs.TauntColor, FlameMesh.Yellow, 0.25f), fade);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 up = new Vector2(side * 0.22f, 1f).normalized, foot = at + new Vector2(side * 0.11f, 0f);
                    front.Bar(foot + up * (0.16f * throb - outline * 0.5f), up, 0.34f * throb + outline, 0.11f + outline, color, color);
                    front.Diamond(foot, 0.065f * throb + outline * 0.6f, color);
                }
            }
        }

        /// <summary>Each stopped bolt: a flare where it struck and a ripple running both ways round the edge.</summary>
        private void DrawBlocks(Vector2 center)
        {
            Color steel = ShieldTaunt.ShieldColor;
            for (int i = 0; i < Mathf.Min(blocks, MaxBlocks); i++)
            {
                float t = (age - blockTimes[i]) / BlockTime;
                if (t >= 1f) continue;
                float rest = 1f - t, a = blockAngles[i];
                Arc(front, center, radius, a - 0.55f, a + 0.55f, 0.3f * rest + 0.04f, FlameMesh.Alpha(steel, 0.6f * rest));
                Arc(front, center, radius, a - 0.4f, a + 0.4f, 0.1f * rest + 0.02f, FlameMesh.Alpha(Color.white, rest));
                front.Diamond(center + FlameMesh.Polar(a, radius), 0.3f * rest + 0.05f, FlameMesh.Alpha(Color.white, rest));
                for (int side = -1; side <= 1; side += 2)
                    Arc(front, center, radius, a + side * (0.55f + 1.6f * t), a + side * (0.85f + 1.6f * t), 0.06f, FlameMesh.Alpha(steel, 0.7f * rest));
            }
        }

        /// <summary>The bellow: two shockwaves and a crown of spikes.</summary>
        private void DrawOpening(Vector2 center, float t)
        {
            Color rage = HeroBuffs.TauntColor;
            float rest = 1f - t;
            front.Ring(center, 0.4f + radius * 1.6f * t, 0.3f * rest + 0.02f, FlameMesh.Alpha(rage, rest), FlameMesh.Alpha(rage, 0f), 64);
            front.Ring(center, 0.3f + radius * t, 0.14f * rest + 0.02f, FlameMesh.Alpha(Color.white, 0.8f * rest), 56);
            front.Disc(center, 0.5f + 0.9f * t, FlameMesh.Alpha(FlameMesh.Core, 0.6f * rest * rest), FlameMesh.Alpha(rage, 0f), 24);
            Spikes(center, 14, 0.4f + 1.1f * t, 0.9f + 1.2f * t, 0.16f * rest, rest);
        }

        /// <summary>The ward dropping: its edge snaps back into him.</summary>
        private void DrawEnding(Vector2 center, float t)
        {
            float rest = 1f - Mathf.Clamp01(t);
            front.Ring(center, 0.2f + radius * rest, 0.16f * rest + 0.02f, FlameMesh.Alpha(HeroBuffs.TauntColor, rest), 56);
        }

        private void DrawBurst(float t)
        {
            Color rage = HeroBuffs.TauntColor;
            float rest = 1f - t, grow = 1f - rest * rest * rest, power = Mathf.Clamp01(hits / 8f);
            back.Disc(origin, radius * grow, FlameMesh.Alpha(Scorch, 0.5f * rest), FlameMesh.Alpha(rage, 0.3f * rest), 48);
            front.Disc(origin, radius * 0.5f * (0.4f + grow), FlameMesh.Alpha(FlameMesh.Core, 0.8f * rest * rest), FlameMesh.Alpha(rage, 0f), 32);
            front.Ring(origin, radius * grow, (0.35f + 0.3f * power) * rest + 0.03f, FlameMesh.Alpha(FlameMesh.Yellow, rest), FlameMesh.Alpha(rage, 0f), 64);
            front.Ring(origin, radius * grow * 0.7f, 0.12f * rest + 0.02f, FlameMesh.Alpha(Color.white, 0.8f * rest), 56);
            Spikes(origin, 12 + 2 * Mathf.Min(hits, 10), radius * (0.15f + 0.45f * grow), radius * (0.35f + 0.7f * grow), 0.22f * rest, rest);
        }

        /// <summary>A crown of spikes from <paramref name="near"/> out to around <paramref name="far"/>.</summary>
        private void Spikes(Vector2 center, int count, float near, float far, float halfWidth, float alpha)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = FlameMesh.Polar((i + FlameMesh.Hash(i, 2.1f) * 0.6f) * Tau / count, 1f), across = Vector2.Perpendicular(dir) * halfWidth;
                float tip = Mathf.Lerp(near, far, 0.5f + 0.5f * FlameMesh.Hash(i, 6.6f));
                Color root = FlameMesh.Alpha(i % 2 == 0 ? FlameMesh.Yellow : HeroBuffs.TauntColor, alpha);
                front.Triangle(center + dir * near - across, center + dir * tip, center + dir * near + across, root, FlameMesh.Alpha(Color.white, alpha), root);
            }
        }

        private static void Arc(FlameMesh mesh, Vector2 center, float radius, float from, float to, float width, Color color)
        {
            const int segments = 8;
            float inner = radius - width * 0.5f, outer = radius + width * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                Vector2 d0 = FlameMesh.Polar(Mathf.Lerp(from, to, i / (float)segments), 1f), d1 = FlameMesh.Polar(Mathf.Lerp(from, to, (i + 1) / (float)segments), 1f);
                mesh.Quad(center + d0 * inner, center + d0 * outer, center + d1 * outer, center + d1 * inner, color, color, color, color);
            }
        }

        private void OnDestroy()
        {
            back?.Release();
            front?.Release();
        }
    }
}
