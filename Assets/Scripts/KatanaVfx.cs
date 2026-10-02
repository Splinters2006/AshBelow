using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The Samurai's katana work: a crescent that sweeps across a cut, a straight thrust, the clean slice her dash
    /// leaves behind, the slow sheathe that ends her pose, and the impact frames that hit as it clicks shut.
    /// </summary>
    public sealed class KatanaVfx : MeshEffect
    {
        private enum Style { Crescent, Thrust, Slice, Sheathe, Impact, Bloom }
        private Style style;
        private Vector2 origin, aim, end;
        private float reach, cone, width;
        private bool reverse;
        private Color color;
        private Vector2[] cuts;

        /// <summary>A crescent of steel sweeping across the cone; <paramref name="reverse"/> swings it back the other way.</summary>
        public static void Crescent(Transform root, Vector2 origin, Vector2 aim, float reach, float coneAngle, Color color, bool reverse = false, float duration = 0.2f)
        {
            if (aim.sqrMagnitude < 0.0001f) return;
            var effect = Spawn<KatanaVfx>(root, duration);
            if (effect == null) return;
            effect.style = Style.Crescent;
            effect.origin = origin;
            effect.aim = aim.normalized;
            effect.reach = Mathf.Max(0.4f, reach);
            effect.cone = Mathf.Clamp(coneAngle, 10f, 360f) * Mathf.Deg2Rad;
            effect.reverse = reverse;
            effect.color = color;
            effect.Redraw();
        }

        /// <summary>A straight lunge with the point.</summary>
        public static void Thrust(Transform root, Vector2 origin, Vector2 aim, float reach, float halfWidth, Color color)
        {
            if (aim.sqrMagnitude < 0.0001f) return;
            var effect = Spawn<KatanaVfx>(root, 0.22f);
            if (effect == null) return;
            effect.style = Style.Thrust;
            effect.origin = origin;
            effect.aim = aim.normalized;
            effect.reach = Mathf.Max(0.5f, reach);
            effect.width = halfWidth;
            effect.color = color;
            effect.Redraw();
        }

        /// <summary>
        /// One clean line from <paramref name="from"/> to <paramref name="to"/> that hangs for a beat, then parts and bleeds away.
        /// With a <paramref name="halfWidth"/> it also washes the whole swath that far either side of the line (and round its ends).
        /// </summary>
        public static void Slice(Transform root, Vector2 from, Vector2 to, Color color, float duration = 0.45f, float halfWidth = 0f)
        {
            if ((to - from).sqrMagnitude < 0.0001f) return;
            var effect = Spawn<KatanaVfx>(root, duration, 10);
            if (effect == null) return;
            effect.style = Style.Slice;
            effect.origin = from;
            effect.end = to;
            effect.width = halfWidth;
            effect.color = color;
            effect.Redraw();
        }

        /// <summary>The katana slides home into its scabbard at her hip and clicks shut.</summary>
        public static void Sheathe(Transform root, Vector2 hero, Vector2 facing, Color color, float duration)
        {
            var effect = Spawn<KatanaVfx>(root, duration, 10);
            if (effect == null) return;
            effect.style = Style.Sheathe;
            effect.origin = hero;
            effect.aim = facing.x < 0f ? Vector2.left : Vector2.right;
            effect.color = color;
            effect.Redraw();
        }

        /// <summary>
        /// Impact frames: for a few frames the whole screen snaps to black, then white, then blood, with every cut in
        /// <paramref name="cuts"/> (pairs of points, one line each) drawn clean across it in the opposite colour.
        /// </summary>
        public static void Impact(Transform root, Vector2 hero, Vector2[] cuts, Color color, float duration)
        {
            var effect = Spawn<KatanaVfx>(root, duration, 30);
            if (effect == null) return;
            effect.style = Style.Impact;
            effect.origin = hero;
            effect.cuts = cuts;
            effect.color = color;
            effect.Redraw();
        }

        /// <summary>
        /// A bleeding death unfolds into a crimson flower whose petal tips reach the passive's actual damage radius: one
        /// crisp ring sweeps out to that edge, six smooth petals open and turn into place over a darker inner whorl,
        /// then the flower fades from the tips in, leaving the edge outlined for a moment.
        /// </summary>
        public static void CrimsonBloom(Transform root, Vector2 center, float radius)
        {
            var effect = Spawn<KatanaVfx>(root, 0.7f, 10);
            if (effect == null) return;
            effect.style = Style.Bloom;
            effect.origin = center;
            effect.reach = Mathf.Max(0.1f, radius);
            effect.aim = FlameMesh.Polar(Random.value * Mathf.PI * 2f, 1f);
            effect.Redraw();
        }

        private const int BloomPetals = 6;

        private void DrawBloom(float t)
        {
            float open = EaseOut(t / 0.42f), fade = t < 0.5f ? 1f : Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);
            float turn = Mathf.Atan2(aim.y, aim.x) + 0.35f * (1f - open);
            Color blood = SamuraiAttack.Blood, deep = new Color(blood.r * 0.45f, 0.01f, blood.b * 0.35f), pale = new Color(1f, 0.82f, 0.86f);
            Color clear = FlameMesh.Alpha(blood, 0f);
            // The edge of the burst: one crisp ring racing out to the radius, then a faint outline that lingers.
            float wave = EaseOut(t / 0.3f);
            if (wave < 1f) Mesh.Ring(origin, reach * wave, 0.06f, FlameMesh.Alpha(pale, 0.9f * (1f - wave * 0.5f)), FlameMesh.Alpha(blood, 0.8f * (1f - wave * 0.5f)), 64);
            Mesh.Ring(origin, reach, 0.035f, FlameMesh.Alpha(blood, 0.55f * open * fade), FlameMesh.Alpha(blood, 0.2f * open * fade), 64);
            // A darker inner whorl, set between the petals, gives the flower its depth.
            for (int i = 0; i < BloomPetals; i++)
                Petal(turn + (i + 0.5f) * Mathf.PI * 2f / BloomPetals - 0.2f * open, reach * 0.55f * open, reach * 0.16f * open,
                    FlameMesh.Alpha(deep, 0.85f * fade), FlameMesh.Alpha(deep, 0.95f * fade), FlameMesh.Alpha(blood, 0.5f * fade), 0f);
            // The petals: smooth crimson blades from the heart to the radius, each with a pale vein down its middle.
            for (int i = 0; i < BloomPetals; i++)
                Petal(turn + i * Mathf.PI * 2f / BloomPetals, reach * 0.97f * open, reach * 0.24f * open,
                    FlameMesh.Alpha(blood, 0.9f * fade), FlameMesh.Alpha(deep, fade), FlameMesh.Alpha(pale, 0.75f * fade), t);
            // The heart: a pale bead that pulses once and shrinks away.
            float heart = reach * (0.16f + 0.06f * Mathf.Sin(Mathf.Min(1f, t / 0.25f) * Mathf.PI)) * (1f - t);
            if (heart > 0.01f) Mesh.Disc(origin, heart, FlameMesh.Alpha(pale, fade), FlameMesh.Alpha(blood, 0.6f * fade), 24);
            Mesh.Disc(origin, reach * 0.06f * fade, FlameMesh.Alpha(Color.white, fade), clear, 16);
        }

        /// <summary>
        /// One smooth, pointed petal from the bloom's heart along <paramref name="angle"/>: <paramref name="body"/> down its
        /// middle shading to <paramref name="edge"/> at its rim, with a <paramref name="vein"/> line along its spine. It
        /// fades from the tip inward as <paramref name="wither"/> runs past halfway.
        /// </summary>
        private void Petal(float angle, float length, float width, Color body, Color edge, Color vein, float wither)
        {
            if (length < 0.02f || width < 0.005f) return;
            const int Segments = 12;
            Vector2 along = FlameMesh.Polar(angle, 1f), side = Vector2.Perpendicular(along);
            float cut = wither < 0.5f ? 1f : 1f - (wither - 0.5f) * 1.4f;
            for (int i = 0; i < Segments; i++)
            {
                float u0 = i / (float)Segments, u1 = (i + 1) / (float)Segments;
                // Swells from a narrow neck to its widest a third of the way out, then tapers to a sharp tip.
                float w0 = width * PetalWidth(u0), w1 = width * PetalWidth(u1);
                float a0 = Mathf.Clamp01((cut - u0) * 4f), a1 = Mathf.Clamp01((cut - u1) * 4f);
                Vector2 p0 = origin + along * length * u0, p1 = origin + along * length * u1;
                Color b0 = FlameMesh.Alpha(body, a0), b1 = FlameMesh.Alpha(body, a1), e0 = FlameMesh.Alpha(edge, a0), e1 = FlameMesh.Alpha(edge, a1);
                Mesh.Quad(p0, p0 + side * w0, p1 + side * w1, p1, b0, e0, e1, b1);
                Mesh.Quad(p0, p0 - side * w0, p1 - side * w1, p1, b0, e0, e1, b1);
                if (u0 > 0.08f && u1 < 0.85f)
                {
                    float v0 = width * 0.07f * (1f - u0), v1 = width * 0.07f * (1f - u1);
                    Color c0 = FlameMesh.Alpha(vein, a0), c1 = FlameMesh.Alpha(vein, a1);
                    Mesh.Quad(p0 - side * v0, p0 + side * v0, p1 + side * v1, p1 - side * v1, c0, c0, c1, c1);
                }
            }
        }

        private static float PetalWidth(float u) => Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(u), 0.65f));

        protected override void Draw(float t)
        {
            if (style == Style.Crescent) DrawCrescent(t);
            else if (style == Style.Thrust) DrawThrust(t);
            else if (style == Style.Slice) DrawSlice(t);
            else if (style == Style.Impact) DrawImpact(t);
            else if (style == Style.Bloom) DrawBloom(t);
            else DrawSheathe(t);
        }

        private void DrawCrescent(float t)
        {
            // The edge whips across the cone in the first third; the crescent it leaves hangs, drifts outward and is
            // eaten away from the tail. Whatever the steel's colour, its underside runs with blood.
            float head = EaseOut(t / 0.32f), tail = t < 0.3f ? 0f : 0.9f * (t - 0.3f) / 0.7f * ((t - 0.3f) / 0.7f);
            float fade = t < 0.45f ? 1f : 1f - (t - 0.45f) / 0.55f;
            float start = Mathf.Atan2(aim.y, aim.x) + (reverse ? cone : -cone) * 0.5f, turn = reverse ? -cone : cone;
            // The crescent's outer glow stops exactly at the cut's reach, so the blade never looks longer than it hits.
            float thickness = Mathf.Min(0.8f, reach * 0.34f) * (0.55f + 0.45f * fade), radius = reach - thickness * 0.3f;
            Color blood = SamuraiAttack.Blood, dark = new Color(blood.r * 0.25f, 0f, blood.b * 0.2f);
            const int Segments = 32;
            // A faint wash over the whole wedge the blade has crossed: the cut hits all the way in to her hands.
            Color wash = FlameMesh.Alpha(color, 0.2f * fade), washCore = FlameMesh.Alpha(color, 0.05f * fade);
            for (int i = 0; i < Segments / 2; i++)
                Mesh.Triangle(origin, origin + FlameMesh.Polar(start + turn * head * i / (Segments / 2), radius),
                    origin + FlameMesh.Polar(start + turn * head * (i + 1) / (Segments / 2), radius), washCore, wash, wash);
            for (int i = 0; i < Segments; i++)
            {
                float u0 = Mathf.Lerp(tail, head, i / (float)Segments), u1 = Mathf.Lerp(tail, head, (i + 1) / (float)Segments);
                float v0 = i / (float)Segments, v1 = (i + 1) / (float)Segments;
                float a0 = start + turn * u0, a1 = start + turn * u1;
                // A long thin tail swelling to a heavy belly, then cut off sharp just behind the edge.
                float w0 = CrescentWidth(v0) * thickness, w1 = CrescentWidth(v1) * thickness;
                float b0 = fade * Mathf.Lerp(0.35f, 1f, v0), b1 = fade * Mathf.Lerp(0.35f, 1f, v1);
                Vector2 d0 = FlameMesh.Polar(a0, 1f), d1 = FlameMesh.Polar(a1, 1f);
                // From the inside out: a dark bloody shadow, the coloured body, a hard white edge, and a thin glow beyond it.
                Band(d0, d1, radius - w0 * 1.35f, radius - w1 * 1.35f, radius - w0 * 0.6f, radius - w1 * 0.6f,
                    FlameMesh.Alpha(dark, 0f), FlameMesh.Alpha(blood, 0.75f * b0), FlameMesh.Alpha(blood, 0.75f * b1));
                Band(d0, d1, radius - w0 * 0.6f, radius - w1 * 0.6f, radius - w0 * 0.22f, radius - w1 * 0.22f,
                    FlameMesh.Alpha(blood, 0.75f * b0), FlameMesh.Alpha(color, b0), FlameMesh.Alpha(color, b1), FlameMesh.Alpha(blood, 0.75f * b1));
                Band(d0, d1, radius - w0 * 0.22f, radius - w1 * 0.22f, radius, radius,
                    FlameMesh.Alpha(Color.white, b0), FlameMesh.Alpha(Color.white, b0), FlameMesh.Alpha(Color.white, b1), FlameMesh.Alpha(Color.white, b1));
                Band(d0, d1, radius, radius, radius + w0 * 0.3f, radius + w1 * 0.3f,
                    FlameMesh.Alpha(color, 0.6f * b0), FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0.6f * b1));
                // Speed lines: hair-thin arcs inside the cut, where the flat of the blade passed.
                for (int line = 0; line < 3; line++)
                {
                    float r = radius * (0.5f + 0.15f * line), w = 0.035f * Mathf.Sin(v0 * Mathf.PI);
                    Band(d0, d1, r - w, r - w, r + w, r + w, FlameMesh.Alpha(color, 0.5f * b0), FlameMesh.Alpha(color, 0.5f * b1));
                }
            }
            // The point of the blade flares while it is still moving.
            float glint = Mathf.Clamp01(1f - t / 0.4f);
            if (glint > 0f)
            {
                Vector2 dir = FlameMesh.Polar(start + turn * head, 1f), tip = origin + dir * radius, tangent = Vector2.Perpendicular(dir);
                Color white = FlameMesh.Alpha(Color.white, glint), clear = FlameMesh.Alpha(Color.white, 0f);
                Mesh.Bar(tip, tangent, 0.7f * glint, 0.06f, white, clear);
                Mesh.Bar(tip, -tangent, 0.7f * glint, 0.06f, white, clear);
                Mesh.Bar(tip, dir, 0.45f * glint, 0.06f, white, clear);
                Mesh.Bar(tip, -dir, 0.45f * glint, 0.06f, white, clear);
                Mesh.Diamond(tip, 0.16f * glint, white);
            }
            // Blood flicked off the edge, thrown outward and along the swing.
            for (int i = 0; i < 9; i++)
            {
                float u = 0.1f + 0.85f * FlameMesh.Hash(i, origin.x + aim.y * 7.3f), born = u * 0.3f;
                if (t < born) continue;
                float life = (t - born) / (1f - born), angle = start + turn * u;
                Vector2 dir = FlameMesh.Polar(angle, 1f), tangent = Vector2.Perpendicular(dir) * Mathf.Sign(turn);
                float speed = 0.5f + 0.9f * FlameMesh.Hash(i, 4.4f);
                Vector2 at = origin + dir * (radius + speed * EaseOut(life) * 0.9f) + tangent * speed * life * 0.7f;
                Mesh.Bar(at, -(dir + tangent * 0.7f).normalized, 0.28f * (1f - life), 0.05f, FlameMesh.Alpha(i % 3 == 0 ? Color.white : blood, 1f - life), FlameMesh.Alpha(blood, 0f));
            }
        }

        /// <summary>How thick the crescent is along its length: 0 at the tail, fattest three-quarters of the way up, a sharp point at the edge.</summary>
        private static float CrescentWidth(float v) => Mathf.Pow(Mathf.Clamp01(v), 1.6f) * Mathf.Clamp01((1f - v) / 0.14f);

        /// <summary>One slice of an arc band between two directions from the origin, from an inner to an outer radius.</summary>
        private void Band(Vector2 d0, Vector2 d1, float inner0, float inner1, float outer0, float outer1, Color innerStart, Color outerStart, Color outerEnd, Color innerEnd)
            => Mesh.Quad(origin + d0 * inner0, origin + d0 * outer0, origin + d1 * outer1, origin + d1 * inner1, innerStart, outerStart, outerEnd, innerEnd);

        private void Band(Vector2 d0, Vector2 d1, float inner0, float inner1, float outer0, float outer1, Color inner, Color outerStart, Color outerEnd)
            => Band(d0, d1, inner0, inner1, outer0, outer1, inner, outerStart, outerEnd, inner);

        private void Band(Vector2 d0, Vector2 d1, float inner0, float inner1, float outer0, float outer1, Color start, Color end)
            => Band(d0, d1, inner0, inner1, outer0, outer1, start, start, end, end);

        private void DrawThrust(float t)
        {
            float extend = t < 0.25f ? EaseOut(t / 0.25f) : 1f, fade = t < 0.3f ? 1f : 1f - (t - 0.3f) / 0.7f;
            Vector2 side = Vector2.Perpendicular(aim), start = origin + aim * 0.3f, tip = origin + aim * reach * extend;
            Color blood = SamuraiAttack.Blood;
            // The lane the point drives down, with the air torn into streaks either side of it.
            // It is exactly the rectangle the thrust hits: full width from her hands to the point.
            Mesh.Quad(origin - side * width, origin + side * width, tip + side * width, tip - side * width,
                FlameMesh.Alpha(color, 0.12f * fade), FlameMesh.Alpha(color, 0.12f * fade), FlameMesh.Alpha(color, 0.45f * fade), FlameMesh.Alpha(color, 0.45f * fade));
            for (int i = 0; i < 6; i++)
            {
                float offset = (FlameMesh.Hash(i, 1.7f) - 0.5f) * 2f * width, from = 0.15f + 0.5f * FlameMesh.Hash(i, 8.3f);
                Mesh.Bar(origin + aim * reach * extend * from + side * offset, aim, reach * extend * (1f - from) * 0.8f, 0.03f,
                    FlameMesh.Alpha(Color.white, 0f), FlameMesh.Alpha(Color.white, 0.7f * fade));
            }
            // The blade itself: a long sliver of steel, hard white down the middle and bloody at the edges.
            Mesh.Triangle(start - side * 0.16f, tip, start + side * 0.16f,
                FlameMesh.Alpha(blood, 0f), FlameMesh.Alpha(blood, 0.9f * fade), FlameMesh.Alpha(blood, 0f));
            Mesh.Triangle(start - side * 0.07f, tip, start + side * 0.07f,
                FlameMesh.Alpha(Color.white, 0.6f * fade), FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(Color.white, 0.6f * fade));
            // Rings of air punched open along the lunge, as wide as the lane.
            if (t > 0.2f)
            {
                float burst = (t - 0.2f) / 0.8f, open = EaseOut(burst);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 at = origin + aim * reach * (0.45f + 0.25f * i);
                    float span = width * open, alpha = (1f - burst) * (0.5f + 0.25f * i);
                    Mesh.Bar(at - side * span, side, span * 2f, 0.05f * (1f - burst) + 0.01f, FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(Color.white, alpha));
                    Mesh.Bar(at + side * span, -side, span * 2f, 0.05f * (1f - burst) + 0.01f, FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(Color.white, alpha));
                }
            }
            float glint = Mathf.Clamp01(1f - Mathf.Abs(t - 0.25f) / 0.25f);
            if (glint <= 0f) return;
            Color white = FlameMesh.Alpha(Color.white, glint), clear = FlameMesh.Alpha(Color.white, 0f);
            Mesh.Bar(tip, side, width * glint, 0.06f, white, clear);
            Mesh.Bar(tip, -side, width * glint, 0.06f, white, clear);
            Mesh.Bar(tip, -aim, 0.5f * glint, 0.06f, white, clear);
            Mesh.Diamond(tip, 0.18f * glint, white);
        }

        private void DrawSlice(float t)
        {
            Vector2 along = (end - origin).normalized, side = Vector2.Perpendicular(along);
            float length = Vector2.Distance(origin, end);
            if (width > 0f) DrawSwath(along, side, length, 1f - t);
            if (t < 0.28f)
            {
                // The cut itself: it snaps the whole way across almost at once, fat and white over a bloody glow, then
                // tightens to a hair before it parts.
                float u = t / 0.28f, snap = EaseOut(u / 0.3f), half = Mathf.Lerp(0.13f, 0.04f, u);
                Vector2 head = origin + along * length * snap, middle = origin + along * length * snap * 0.5f;
                Mesh.Triangle(origin - along * 0.4f, middle + side * half * 4f, middle - side * half * 4f, FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0.8f), FlameMesh.Alpha(color, 0.8f));
                Mesh.Triangle(head + along * 0.4f, middle - side * half * 4f, middle + side * half * 4f, FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(color, 0.8f), FlameMesh.Alpha(color, 0.8f));
                Mesh.Triangle(origin - along * 0.3f, middle + side * half, middle - side * half, Color.white, Color.white, Color.white);
                Mesh.Triangle(head + along * 0.5f, middle - side * half, middle + side * half, Color.white, Color.white, Color.white);
                // A flare where the blade stops.
                float flare = 1f - u;
                Color white = FlameMesh.Alpha(Color.white, flare), clear = FlameMesh.Alpha(Color.white, 0f);
                Mesh.Bar(head, side, 0.9f * flare, 0.07f, white, clear);
                Mesh.Bar(head, -side, 0.9f * flare, 0.07f, white, clear);
                Mesh.Bar(head, along, 0.6f * flare, 0.07f, white, clear);
                Mesh.Diamond(head, 0.2f * flare, white);
                return;
            }
            // Then the two halves slide apart and the line bleeds out.
            float part = (t - 0.28f) / 0.72f, fade = 1f - part, open = EaseOut(part);
            Mesh.Bar(origin, along, length, 0.45f * open, FlameMesh.Alpha(color, 0.45f * fade), FlameMesh.Alpha(color, 0.15f * fade));
            for (int i = -1; i <= 1; i += 2)
            {
                Vector2 shift = side * i * 0.18f * open + along * i * 0.35f * open;
                Mesh.Bar(origin + shift, along, length, 0.07f * fade + 0.01f, FlameMesh.Alpha(Color.white, fade), FlameMesh.Alpha(color, fade));
            }
            // Blood spits out of the opening, square to the cut.
            for (int i = 0; i < 8; i++)
            {
                float where = FlameMesh.Hash(i, length), reachOut = (0.3f + 0.7f * FlameMesh.Hash(i, 6.1f)) * open;
                Vector2 dir = side * (i % 2 == 0 ? 1f : -1f);
                Mesh.Bar(origin + along * length * where + dir * reachOut, -dir, 0.3f * fade, 0.045f, FlameMesh.Alpha(color, fade), FlameMesh.Alpha(color, 0f));
            }
        }

        /// <summary>Everything within <see cref="width"/> of the slice's line: a lane with a rounded cap at each end and a bright rim.</summary>
        private void DrawSwath(Vector2 along, Vector2 side, float length, float fade)
        {
            Color fill = FlameMesh.Alpha(color, 0.2f * fade), rim = FlameMesh.Alpha(Color.white, 0.55f * fade), clear = FlameMesh.Alpha(color, 0f);
            Mesh.Bar(origin, along, length, width * 2f, fill, fill);
            for (int i = -1; i <= 1; i += 2)
                Mesh.Bar(origin + side * i * (width - 0.02f), along, length, 0.04f, rim, rim);
            const int Segments = 12;
            float angle = Mathf.Atan2(side.y, side.x);
            for (int cap = 0; cap < 2; cap++)
            {
                // The start's cap bulges backward, the end's forward.
                Vector2 center = cap == 0 ? origin : end;
                float from = cap == 0 ? angle : angle - Mathf.PI;
                for (int i = 0; i < Segments; i++)
                {
                    Vector2 a = FlameMesh.Polar(from + Mathf.PI * i / Segments, 1f), b = FlameMesh.Polar(from + Mathf.PI * (i + 1) / Segments, 1f);
                    Mesh.Triangle(center, center + a * width, center + b * width, fill, fill, fill);
                    Mesh.Quad(center + a * (width - 0.04f), center + a * width, center + b * width, center + b * (width - 0.04f), rim, rim, rim, rim);
                }
            }
        }

        /// <summary>
        /// 斬 (zan, "to cut down"), stroke by stroke in a unit box, in the order a brush writes it: 車 on the left, 斤 on the right.
        /// </summary>
        private static readonly Vector2[][] ZanStrokes =
        {
            new[] { new Vector2(0.08f, 0.86f), new Vector2(0.46f, 0.86f) },
            new[] { new Vector2(0.12f, 0.72f), new Vector2(0.12f, 0.38f) },
            new[] { new Vector2(0.12f, 0.72f), new Vector2(0.42f, 0.72f), new Vector2(0.42f, 0.38f) },
            new[] { new Vector2(0.12f, 0.55f), new Vector2(0.42f, 0.55f) },
            new[] { new Vector2(0.12f, 0.38f), new Vector2(0.42f, 0.38f) },
            new[] { new Vector2(0.03f, 0.24f), new Vector2(0.51f, 0.24f) },
            new[] { new Vector2(0.27f, 0.98f), new Vector2(0.27f, 0.02f) },
            new[] { new Vector2(0.92f, 0.94f), new Vector2(0.63f, 0.83f) },
            new[] { new Vector2(0.63f, 0.83f), new Vector2(0.63f, 0.45f), new Vector2(0.55f, 0.06f) },
            new[] { new Vector2(0.63f, 0.58f), new Vector2(0.99f, 0.58f) },
            new[] { new Vector2(0.83f, 0.58f), new Vector2(0.83f, 0.02f) }
        };

        /// <summary>
        /// Brushes 斬 into a square of <paramref name="size"/> centred on <paramref name="center"/>. <paramref name="written"/>
        /// runs 0..1 as the strokes go down one after another; each is a wide stroke of <paramref name="ink"/> with a thin <paramref name="core"/>.
        /// </summary>
        private void DrawZan(Vector2 center, float size, float written, Color ink, Color core)
        {
            float strokes = Mathf.Clamp01(written) * ZanStrokes.Length, width = size * 0.075f;
            Vector2 corner = center - Vector2.one * size * 0.5f;
            for (int i = 0; i < ZanStrokes.Length; i++)
            {
                float done = Mathf.Clamp01(strokes - i);
                if (done <= 0f) break;
                var stroke = ZanStrokes[i];
                float total = 0f;
                for (int p = 0; p + 1 < stroke.Length; p++) total += Vector2.Distance(stroke[p], stroke[p + 1]);
                float left = total * done;
                for (int p = 0; p + 1 < stroke.Length && left > 0f; p++)
                {
                    float length = Vector2.Distance(stroke[p], stroke[p + 1]), drawn = Mathf.Min(length, left);
                    Vector2 from = corner + stroke[p] * size, dir = (stroke[p + 1] - stroke[p]) / length;
                    left -= drawn;
                    // Run a little past each corner so the joints close up square.
                    Mesh.Bar(from - dir * width * 0.5f, dir, drawn * size + width, width, ink, ink);
                    Mesh.Bar(from, dir, drawn * size, width * 0.3f, core, core);
                }
            }
        }

        private void DrawSheathe(float t)
        {
            // The scabbard rests at her hip, pointing down and back; the blade is drawn up and out ahead of her.
            Vector2 mouth = origin + new Vector2(aim.x * 0.15f, -0.1f);
            Vector2 back = new Vector2(-aim.x * 0.8f, -0.6f).normalized, blade = -back, side = Vector2.Perpendicular(blade);
            Color scabbard = new Color(0.08f, 0.05f, 0.08f), gold = new Color(1f, 0.8f, 0.38f);
            const float Click = 0.72f;
            float fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
            // The room dims around her while the blade is out, and snaps back on the click.
            float hush = t < Click ? EaseOut(t / Click) : Mathf.Clamp01(1f - (t - Click) / 0.08f);
            Mesh.Ring(origin, 2.1f, 2.6f, FlameMesh.Alpha(Color.black, 0f), FlameMesh.Alpha(Color.black, 0.5f * hush));
            Mesh.Ring(origin, 5.4f, 4f, FlameMesh.Alpha(Color.black, 0.5f * hush), FlameMesh.Alpha(Color.black, 0f));
            // Blood is dragged in toward the scabbard, faster and faster.
            if (t < Click)
            {
                float pull = t / Click;
                for (int i = 0; i < 16; i++)
                {
                    float phase = Mathf.Repeat(pull * (1.5f + 2f * pull) + FlameMesh.Hash(i, 9.1f), 1f);
                    Vector2 dir = FlameMesh.Polar(FlameMesh.Hash(i, 3.7f) * Mathf.PI * 2f, 1f);
                    float far = Mathf.Lerp(2.8f, 0.3f, phase * phase);
                    Mesh.Bar(mouth + dir * (far + 0.25f + 0.5f * pull), -dir, 0.25f + 0.5f * pull, 0.04f,
                        FlameMesh.Alpha(color, 0f), FlameMesh.Alpha(i % 4 == 0 ? Color.white : color, (0.35f + 0.65f * pull) * (1f - phase)));
                }
            }
            Mesh.Bar(mouth, back, 1.15f, 0.13f, FlameMesh.Alpha(scabbard, fade), FlameMesh.Alpha(scabbard, fade));
            Mesh.Bar(mouth + side * 0.035f, back, 1.05f, 0.025f, FlameMesh.Alpha(Color.white, 0.3f * fade), FlameMesh.Alpha(Color.white, 0f));
            Mesh.Bar(mouth + back * 1.05f, back, 0.12f, 0.15f, FlameMesh.Alpha(gold, fade), FlameMesh.Alpha(gold, fade));
            Mesh.Bar(mouth, back, 0.16f, 0.17f, FlameMesh.Alpha(color, fade), FlameMesh.Alpha(color, fade));
            // Slow at first, then it snaps home. The hilt rides the end of the blade and stays out once it is in.
            float home = Mathf.Clamp01(t / 0.7f), left = 1f - home * home * home;
            Vector2 guard = mouth + blade * 1.3f * left;
            if (left > 0.01f)
            {
                // The edge burns red against the dark.
                Mesh.Quad(mouth - side * 0.13f, mouth + side * 0.13f, guard + side * 0.1f, guard - side * 0.1f,
                    FlameMesh.Alpha(color, 0.15f), FlameMesh.Alpha(color, 0.15f), FlameMesh.Alpha(color, 0.6f), FlameMesh.Alpha(color, 0.6f));
                Mesh.Quad(mouth - side * 0.04f, mouth + side * 0.04f, guard + side * 0.035f, guard - side * 0.035f, Color.white, Color.white, Color.white, Color.white);
                // Light runs down the edge as it goes in.
                Vector2 glintAt = mouth + blade * 1.3f * left * (1f - Mathf.Repeat(t * 4f, 1f));
                Mesh.Diamond(glintAt, 0.11f, FlameMesh.Alpha(Color.white, 0.95f));
            }
            Mesh.Bar(guard, blade, 0.36f, 0.075f, FlameMesh.Alpha(scabbard, fade), FlameMesh.Alpha(scabbard, fade));
            Mesh.Bar(guard + blade * 0.1f, blade, 0.06f, 0.085f, FlameMesh.Alpha(color, fade), FlameMesh.Alpha(color, fade));
            Mesh.Bar(guard + blade * 0.23f, blade, 0.06f, 0.085f, FlameMesh.Alpha(color, fade), FlameMesh.Alpha(color, fade));
            Mesh.Bar(guard - side * 0.13f, side, 0.26f, 0.055f, FlameMesh.Alpha(gold, fade), FlameMesh.Alpha(gold, fade));
            // The click: an eight-pointed star at the scabbard's mouth as the guard meets it, with a long flat glare through it.
            float click = Mathf.Clamp01(1f - Mathf.Abs(t - Click) / 0.18f);
            if (click > 0f)
            {
                Color white = FlameMesh.Alpha(Color.white, click), clear = FlameMesh.Alpha(Color.white, 0f);
                Mesh.Bar(mouth, Vector2.left, 3.4f * click, 0.07f, white, clear);
                Mesh.Bar(mouth, Vector2.right, 3.4f * click, 0.07f, white, clear);
                Mesh.Bar(mouth, Vector2.up, 1.1f * click, 0.07f, white, clear);
                Mesh.Bar(mouth, Vector2.down, 1.1f * click, 0.07f, white, clear);
                for (int i = 0; i < 4; i++)
                    Mesh.Bar(mouth, FlameMesh.Polar(Mathf.PI * (0.25f + 0.5f * i), 1f), 0.6f * click, 0.05f, FlameMesh.Alpha(color, click), FlameMesh.Alpha(color, 0f));
                Mesh.Disc(mouth, 0.4f * click, FlameMesh.Alpha(Color.white, 0.9f * click), FlameMesh.Alpha(color, 0f), 16);
            }
            // 斬 is brushed in the air above her while the blade slides home, burns white on the click, and is gone.
            float burn = Mathf.Clamp01(1f - Mathf.Abs(t - Click) / 0.12f), gone = t > Click ? Mathf.Clamp01(1f - (t - Click) / 0.22f) : 1f;
            DrawZan(origin + Vector2.up * 2.1f, 1.5f * (1f + 0.12f * burn), t / (Click - 0.1f),
                FlameMesh.Alpha(Color.Lerp(color, Color.white, burn), gone), FlameMesh.Alpha(Color.Lerp(scabbard, Color.white, burn), gone));
            // Then the shock of it rolls outward.
            if (t <= Click) return;
            float after = (t - Click) / (1f - Click), spread = EaseOut(after);
            Mesh.Ring(mouth, 0.3f + 2.6f * spread, 0.16f * (1f - after), FlameMesh.Alpha(color, 1f - after));
            Mesh.Ring(mouth, 0.2f + 1.5f * spread, 0.07f * (1f - after), FlameMesh.Alpha(Color.white, 1f - after));
        }

        private void DrawImpact(float t)
        {
            // Three hard frames, no blending between them: white on black, black on white, then white on blood as it lets go.
            int frame = t < 0.4f ? 0 : t < 0.7f ? 1 : 2;
            Color ground = frame == 0 ? Color.black : frame == 1 ? Color.white : FlameMesh.Alpha(color, 0.75f * (1f - (t - 0.7f) / 0.3f));
            Color ink = frame == 1 ? Color.black : Color.white;
            var view = Camera.main;
            Vector2 center = view != null ? (Vector2)view.transform.position : origin;
            Mesh.Rect(center - new Vector2(60f, 40f), center + new Vector2(60f, 40f), ground);
            // Speed lines racing in at her from the edges of the screen.
            for (int i = 0; i < 22; i++)
            {
                float angle = (i + FlameMesh.Hash(i, frame + 1.3f)) / 22f * Mathf.PI * 2f, near = 2.2f + 3.5f * FlameMesh.Hash(i, frame + 5.9f);
                Vector2 dir = FlameMesh.Polar(angle, 1f), across = Vector2.Perpendicular(dir) * (0.25f + 0.5f * FlameMesh.Hash(i, 2.2f));
                Mesh.Triangle(origin + dir * near, origin + dir * 45f + across * 4f, origin + dir * 45f - across * 4f, ink, ink, ink);
            }
            // Every cut, ruled clean across the whole screen.
            float half = frame == 0 ? 0.16f : frame == 1 ? 0.11f : 0.06f;
            for (int i = 0; cuts != null && i + 1 < cuts.Length; i += 2)
            {
                Vector2 along = (cuts[i + 1] - cuts[i]).normalized, side = Vector2.Perpendicular(along), middle = (cuts[i] + cuts[i + 1]) * 0.5f;
                if (frame == 0)
                {
                    Mesh.Triangle(middle - along * 30f, middle + side * half * 3f, middle - side * half * 3f, color, color, color);
                    Mesh.Triangle(middle + along * 30f, middle - side * half * 3f, middle + side * half * 3f, color, color, color);
                }
                Mesh.Triangle(middle - along * 30f, middle + side * half, middle - side * half, ink, ink, ink);
                Mesh.Triangle(middle + along * 30f, middle - side * half, middle + side * half, ink, ink, ink);
            }
            // 斬 stamped huge behind her, in the frame's ink.
            DrawZan(origin + Vector2.up * 2.6f, 3.2f, 1f, frame == 0 ? color : ink, ink);
            // And the glint where the guard met the scabbard.
            float star = frame == 0 ? 1f : frame == 1 ? 0.7f : 0.4f;
            Mesh.Bar(origin + Vector2.left * 1.6f * star, Vector2.right, 3.2f * star, 0.09f, ink, ink);
            Mesh.Bar(origin + Vector2.down * 1.6f * star, Vector2.up, 3.2f * star, 0.09f, ink, ink);
            Mesh.Diamond(origin, 0.35f * star, ink);
        }
    }
}
