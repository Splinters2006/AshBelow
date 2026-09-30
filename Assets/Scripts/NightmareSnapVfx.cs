using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Nightmare Snap. <see cref="Snap"/> is the Demoness's side: her fingers snap in a violet flash with comic snap
    /// lines, and a ripple of nightmare rolls out to the edge of its reach, a dark band with a violet rim and thorns.
    /// <see cref="Tether"/> is each victim's: a thread of shadow shoots from her to the victim and pulls taut, the
    /// shackle of paralysis around the victim shatters into shards, the thread breaks and whips back, and a nightmare
    /// eye opens over the victim, wider the more paralysis was snapped.
    /// </summary>
    public sealed class NightmareSnapVfx : MeshEffect
    {
        private const float SnapLife = 0.7f, TetherLife = 0.75f, Taut = 0.2f;
        private static readonly Color Violet = new Color(0.66f, 0.3f, 1f), Abyss = new Color(0.07f, 0.02f, 0.12f), Pale = new Color(0.96f, 0.92f, 1f),
            Iris = new Color(0.85f, 0.15f, 0.35f);
        private bool tether;
        private Vector2 from, to;
        private float radius, strength;

        public static void Snap(Transform root, Vector2 at, float radius)
        {
            var effect = Spawn<NightmareSnapVfx>(root, SnapLife, 4);
            if (effect == null) return;
            effect.from = at;
            effect.radius = radius;
            effect.Redraw();
            HeroVfx.Sparks(root, at + new Vector2(0.25f, 0.45f), Pale, 8, 3f, 0.2f, null, 360f, 0.7f);
        }

        /// <summary>One victim's snapped paralysis; <paramref name="strength"/> 0-1 is how much of it was left.</summary>
        public static void Tether(Transform root, Vector2 from, Vector2 to, float strength)
        {
            var effect = Spawn<NightmareSnapVfx>(root, TetherLife, 10);
            if (effect == null) return;
            effect.tether = true;
            effect.from = from;
            effect.to = to;
            effect.strength = Mathf.Clamp01(strength);
            effect.Redraw();
        }

        protected override void Draw(float t)
        {
            if (tether) DrawTether(t);
            else DrawSnap(t);
        }

        private void DrawSnap(float t)
        {
            float fade = 1f - t;
            // The snap itself, at her raised hand.
            Vector2 hand = from + new Vector2(0.25f, 0.45f);
            if (t < 0.35f)
            {
                float flash = 1f - t / 0.35f;
                Mesh.Disc(hand, 0.12f + 0.3f * (1f - flash), FlameMesh.Alpha(Color.white, flash), FlameMesh.Alpha(Violet, 0f), 16);
                for (int i = 0; i < 6; i++)
                {
                    Vector2 dir = FlameMesh.Polar(i * Mathf.PI / 3f + 0.3f, 1f);
                    Mesh.Bar(hand + dir * (0.15f + 0.25f * (1f - flash)), dir, 0.18f * flash + 0.02f, 0.045f, FlameMesh.Alpha(Pale, flash), FlameMesh.Alpha(Violet, 0f));
                }
            }
            // The ripple: a dark band rolling out with a violet rim and thorns pointing outward along it.
            float reach = radius * EaseOut(Mathf.Clamp01(t / 0.6f));
            float band = Mathf.Lerp(0.2f, 0.9f, t);
            Mesh.Ring(from, Mathf.Max(0.05f, reach - band * 0.5f), band, FlameMesh.Alpha(Abyss, 0f), FlameMesh.Alpha(Abyss, 0.55f * fade), 56);
            Mesh.Ring(from, reach, 0.07f * fade + 0.01f, FlameMesh.Alpha(Violet, 0.9f * fade), 56);
            Mesh.Ring(from, reach - 0.08f, 0.025f, FlameMesh.Alpha(Pale, 0.6f * fade), 56);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f + FlameMesh.Hash(i, radius) * 0.2f;
                Vector2 dir = FlameMesh.Polar(a, 1f), side = Vector2.Perpendicular(dir);
                Vector2 root = from + dir * reach;
                float spike = (0.18f + 0.12f * FlameMesh.Hash(i, 4.4f)) * fade;
                Mesh.Triangle(root - side * 0.05f, root + dir * spike, root + side * 0.05f, FlameMesh.Alpha(Violet, fade), FlameMesh.Alpha(Pale, 0f), FlameMesh.Alpha(Violet, fade));
            }
        }

        private void DrawTether(float t)
        {
            Vector2 span = to - from;
            float length = span.magnitude;
            Vector2 dir = length > 0.001f ? span / length : Vector2.right, side = Vector2.Perpendicular(dir);
            float after = Mathf.Clamp01((t - Taut) / (1f - Taut)), fade = 1f - after;
            // The thread: out to the victim, then snapped in two, each half whipping back toward its own end.
            if (t < Taut)
            {
                float reach = EaseOut(t / Taut) * length;
                Thread(from, from + dir * reach, side, 1f, 0.35f * (1f - t / Taut));
            }
            else if (after < 0.6f)
            {
                float retract = EaseOut(after / 0.6f), a = 1f - after / 0.6f;
                Vector2 mid = from + span * 0.5f;
                Thread(from, Vector2.Lerp(mid, from, retract), side, a, 0.25f * a);
                Thread(to, Vector2.Lerp(mid, to, retract), side, a, 0.25f * a);
            }
            if (t < Taut) return;
            // The shackle breaks: its ring flies apart in shards.
            float burst = EaseOut(after);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + 0.2f;
                Vector2 out1 = FlameMesh.Polar(a, 1f), across = Vector2.Perpendicular(out1);
                Vector2 c = to + out1 * (0.45f + 0.7f * burst) + Vector2.down * 0.3f * after * after;
                Mesh.Triangle(c - across * 0.08f, c + out1 * 0.14f, c + across * 0.08f, FlameMesh.Alpha(Pale, fade), FlameMesh.Alpha(Violet, fade), FlameMesh.Alpha(Pale, fade));
            }
            Mesh.Disc(to, 0.35f + 0.5f * burst, FlameMesh.Alpha(Violet, 0.5f * fade * fade), FlameMesh.Alpha(Violet, 0f), 24);
            // The nightmare eye: opens over the victim, glares, then shuts.
            float open = Mathf.Sin(Mathf.Clamp01(after / 0.85f) * Mathf.PI);
            if (open > 0.01f) Eye(to + Vector2.up * 0.85f, 0.28f + 0.22f * strength, open, fade);
        }

        /// <summary>A wavering thread of shadow with a pale core.</summary>
        private void Thread(Vector2 a, Vector2 b, Vector2 side, float alpha, float wobble)
        {
            const int Segments = 8;
            Vector2 previous = a;
            for (int i = 1; i <= Segments; i++)
            {
                float u = i / (float)Segments;
                Vector2 p = Vector2.Lerp(a, b, u) + side * Mathf.Sin(u * Mathf.PI * 3f + Age * 30f) * wobble * Mathf.Sin(u * Mathf.PI);
                float length = Vector2.Distance(previous, p);
                if (length > 0.001f)
                {
                    Vector2 d = (p - previous) / length;
                    Mesh.Bar(previous, d, length, 0.09f, FlameMesh.Alpha(Violet, 0.6f * alpha), FlameMesh.Alpha(Violet, 0.6f * alpha));
                    Mesh.Bar(previous, d, length, 0.03f, FlameMesh.Alpha(Pale, alpha), FlameMesh.Alpha(Pale, alpha));
                }
                previous = p;
            }
        }

        /// <summary>An almond eye: a violet glow, pale white, a blood-red iris and a slit pupil; <paramref name="open"/> 0-1.</summary>
        private void Eye(Vector2 at, float width, float open, float alpha)
        {
            float height = width * 0.45f * open;
            const int Segments = 16;
            Mesh.Ellipse(at, width * 1.5f, width * 0.8f * open + 0.05f, FlameMesh.Alpha(Violet, 0.45f * alpha), FlameMesh.Alpha(Violet, 0f), 20);
            // The white: an almond from two arcs meeting at the corners.
            for (int i = 0; i < Segments; i++)
            {
                float u0 = i / (float)Segments, u1 = (i + 1) / (float)Segments;
                float x0 = Mathf.Lerp(-width, width, u0), x1 = Mathf.Lerp(-width, width, u1);
                float h0 = Mathf.Sin(u0 * Mathf.PI) * height, h1 = Mathf.Sin(u1 * Mathf.PI) * height;
                Mesh.Quad(at + new Vector2(x0, -h0), at + new Vector2(x0, h0), at + new Vector2(x1, h1), at + new Vector2(x1, -h1),
                    FlameMesh.Alpha(Pale, alpha), FlameMesh.Alpha(Pale, alpha), FlameMesh.Alpha(Pale, alpha), FlameMesh.Alpha(Pale, alpha));
            }
            if (height < 0.02f) return;
            float iris = Mathf.Min(height * 0.95f, width * 0.45f);
            Mesh.Disc(at, iris, FlameMesh.Alpha(Color.Lerp(Iris, Color.white, 0.2f), alpha), FlameMesh.Alpha(Iris * 0.6f, alpha), 16);
            Mesh.Ellipse(at, iris * 0.22f, iris * 0.85f, FlameMesh.Alpha(Color.black, alpha), FlameMesh.Alpha(Color.black, alpha), 10);
            Mesh.Diamond(at + new Vector2(-iris * 0.35f, iris * 0.35f), iris * 0.18f, FlameMesh.Alpha(Color.white, 0.9f * alpha));
        }
    }
}
