using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A world-space mesh rebuilt every frame for hellfire and frost effects: fills, rings, flickering flame tongues, ice shards and embers.
    /// Owners call <see cref="Begin"/>, add shapes, then <see cref="Commit"/>.
    /// </summary>
    public sealed class FlameMesh
    {
        public static readonly Color Core = new Color(1f, 0.95f, 0.7f);
        public static readonly Color Yellow = new Color(1f, 0.78f, 0.2f);
        public static readonly Color Orange = new Color(1f, 0.42f, 0.06f);
        public static readonly Color Crimson = new Color(0.78f, 0.06f, 0.08f);
        public static readonly Color Ember = new Color(0.35f, 0.02f, 0.04f);
        private const float Tau = Mathf.PI * 2f;
        private static Material sharedMaterial;
        private static int materialUsers;
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Color> colors = new List<Color>(4096);
        private readonly List<int> triangles = new List<int>(6144);
        private readonly Mesh mesh;
        public MeshRenderer Renderer { get; }
        /// <summary>World size of one pixel of a <see cref="Pixelated"/> mesh.</summary>
        public const float Pixel = 0.1f;
        /// <summary>
        /// Draws every shape as pixel art instead of smooth geometry: edges snap to a <see cref="Pixel"/> grid, gradients
        /// become flat colour bands and flames flicker in steps. For the fire and ice guardians, to match their sprites.
        /// </summary>
        public bool Pixelated { get; set; }

        public FlameMesh(GameObject owner, int sortingOrder)
        {
            if (sharedMaterial == null)
                sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Hellfire (shared)", hideFlags = HideFlags.HideAndDontSave };
            materialUsers++;
            mesh = new Mesh { name = "Hellfire geometry", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            owner.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = owner.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = sharedMaterial;
            Renderer.sortingOrder = sortingOrder;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
        }

        public void Begin() { vertices.Clear(); colors.Clear(); triangles.Clear(); }

        public void Commit()
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        public void Release()
        {
            if (mesh != null) Object.Destroy(mesh);
            if (--materialUsers <= 0 && sharedMaterial != null)
            { materialUsers = 0; Object.Destroy(sharedMaterial); sharedMaterial = null; }
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(ca); colors.Add(cb); colors.Add(cc);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            colors.Add(ca); colors.Add(cb); colors.Add(cc); colors.Add(cd);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }

        public void Rect(Vector2 min, Vector2 max, Color color)
        {
            if (Pixelated)
            {
                min = Snap(min);
                max = new Vector2(Mathf.Max(min.x + Pixel, Snap(max.x)), Mathf.Max(min.y + Pixel, Snap(max.y)));
            }
            Quad(min, new Vector2(min.x, max.y), max, new Vector2(max.x, min.y), color, color, color, color);
        }

        /// <summary>A rectangle from <paramref name="from"/> along <paramref name="direction"/>, fading between two colours.</summary>
        public void Bar(Vector2 from, Vector2 direction, float length, float width, Color start, Color end)
        {
            if (Pixelated) { PixelBar(from, direction, length, width, start, end); return; }
            Vector2 side = Vector2.Perpendicular(direction) * width * 0.5f, to = from + direction * length;
            Quad(from - side, from + side, to + side, to - side, start, start, end, end);
        }

        public void Disc(Vector2 center, float radius, Color inner, Color outer, int segments = 40)
        {
            if (Pixelated) { PixelDisc(center, radius, 1f, inner, outer); return; }
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Tau / segments, a1 = (i + 1) * Tau / segments;
                Triangle(center, center + Polar(a0, radius), center + Polar(a1, radius), inner, outer, outer);
            }
        }

        public void Ring(Vector2 center, float radius, float width, Color color, int segments = 48) => Ring(center, radius, width, color, color, segments);

        public void Ring(Vector2 center, float radius, float width, Color inner, Color outer, int segments = 48)
        {
            if (Pixelated)
            {
                width = Mathf.Max(width, Pixel);
                if (inner == outer || width < Pixel * 2.5f) Annulus(center, radius - width * 0.5f, radius + width * 0.5f, 1f, Color.Lerp(inner, outer, 0.5f));
                else
                {
                    Annulus(center, radius - width * 0.5f, radius, 1f, inner);
                    Annulus(center, radius, radius + width * 0.5f, 1f, outer);
                }
                return;
            }
            float r0 = Mathf.Max(0f, radius - width * 0.5f), r1 = radius + width * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Tau / segments, a1 = (i + 1) * Tau / segments;
                Quad(center + Polar(a0, r0), center + Polar(a0, r1), center + Polar(a1, r1), center + Polar(a1, r0), inner, outer, outer, inner);
            }
        }

        public void Ellipse(Vector2 center, float rx, float ry, Color inner, Color outer, int segments = 28)
        {
            if (Pixelated) { PixelDisc(center, rx, ry / Mathf.Max(0.001f, rx), inner, outer); return; }
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Tau / segments, a1 = (i + 1) * Tau / segments;
                Triangle(center, center + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry),
                    center + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry), inner, outer, outer);
            }
        }

        /// <summary>
        /// One flickering tongue of fire rising from <paramref name="root"/> along <paramref name="up"/>:
        /// a crimson outer flame with an orange body and a white-hot core.
        /// </summary>
        public void Flame(Vector2 root, Vector2 up, float width, float height, float seed, float alpha = 1f)
        {
            if (Pixelated) { PixelFlame(root, up, width, height, seed, alpha); return; }
            float time = Time.time;
            float flicker = 0.7f + 0.3f * Mathf.Sin(time * (9f + seed * 7f) + seed * 40f) + 0.15f * Mathf.Sin(time * 23f + seed * 13f);
            height *= flicker;
            Vector2 across = Vector2.Perpendicular(up), side = across * width * 0.5f;
            Vector2 sway = across * Mathf.Sin(time * 6f + seed * 30f) * width * 0.35f;
            Vector2 tip = root + up * height + sway;
            // A dark, smoky backing so the flame reads against bright floors.
            Triangle(root - side * 1.3f, root + up * height * 1.08f + sway * 1.1f, root + side * 1.3f, Alpha(Ember, 0.55f * alpha), Alpha(Ember, 0f), Alpha(Ember, 0.55f * alpha));
            // A smaller tongue licks up one side and leans the other way.
            float lean = Mathf.Repeat(seed, 1f) < 0.5f ? -1f : 1f;
            Vector2 lickRoot = root + side * lean * 0.6f;
            float lickHeight = height * (0.5f + 0.12f * Mathf.Sin(time * 13f + seed * 21f));
            Triangle(lickRoot - side * 0.4f, lickRoot + up * lickHeight - sway * 0.6f + side * lean * 0.5f, lickRoot + side * 0.4f,
                Alpha(Crimson, 0.85f * alpha), Alpha(Orange, 0f), Alpha(Orange, 0.85f * alpha));
            Triangle(root - side, tip, root + side, Alpha(Crimson, 0.85f * alpha), Alpha(Orange, 0f), Alpha(Crimson, 0.85f * alpha));
            Vector2 mid = root + up * height * 0.72f + sway * 0.8f;
            Triangle(root - side * 0.68f, mid, root + side * 0.68f, Alpha(Orange, alpha), Alpha(Yellow, 0.1f * alpha), Alpha(Orange, alpha));
            Vector2 glow = root + up * height * 0.5f + sway * 0.6f;
            Triangle(root - side * 0.45f, glow, root + side * 0.45f, Alpha(Yellow, alpha), Alpha(Yellow, 0.2f * alpha), Alpha(Yellow, alpha));
            Vector2 core = root + up * height * 0.3f + sway * 0.4f;
            Triangle(root - side * 0.24f, core, root + side * 0.24f, Alpha(Core, alpha), Alpha(Yellow, 0.4f * alpha), Alpha(Core, alpha));
            // Sparks break off the tip and drift up.
            for (int i = 0; i < 2; i++)
            {
                float s = Hash(seed * 13.7f + i, 4.2f), rise = Mathf.Repeat(time * (0.9f + s * 0.8f) + s, 1f);
                Vector2 spark = root + up * height * (0.75f + 0.7f * rise) + across * (s - 0.5f) * width * 1.1f + sway * rise;
                Quad(spark - up * width * 0.07f, spark - across * width * 0.04f, spark + up * width * 0.07f, spark + across * width * 0.04f,
                    Alpha(Yellow, (1f - rise) * alpha), Alpha(Orange, (1f - rise) * alpha), Alpha(Yellow, (1f - rise) * alpha), Alpha(Orange, (1f - rise) * alpha));
            }
        }

        /// <summary>
        /// A faceted ice shard jutting from <paramref name="root"/> along <paramref name="up"/>: a lit facet and a shaded
        /// one either side of a bright ridge, a pale tip, and a glint that twinkles now and then.
        /// </summary>
        public void Crystal(Vector2 root, Vector2 up, float width, float height, float seed, Color glint, Color light, Color shade, Color dark, float alpha = 1f)
        {
            if (alpha <= 0.01f || height <= 0.01f) return;
            if (Pixelated) { PixelCrystal(root, up, width, height, seed, glint, light, shade, dark, alpha); return; }
            Vector2 across = Vector2.Perpendicular(up), half = across * width * 0.5f, ridge = across * width * 0.12f;
            Vector2 shoulder = root + up * height * 0.6f, tip = root + up * height;
            Color l = Alpha(light, alpha), s = Alpha(shade, alpha), d = Alpha(dark, alpha), g = Alpha(glint, alpha);
            Quad(root + half * 0.72f, shoulder + half, shoulder + ridge, root + ridge, l, l, g, l);
            Quad(root + ridge, shoulder + ridge, shoulder - half, root - half * 0.72f, s, s, d, d);
            Triangle(shoulder + half, tip, shoulder + ridge, g, g, g);
            Triangle(shoulder + ridge, tip, shoulder - half, l, g, s);
        }

        public void Diamond(Vector2 center, float size, Color color)
        {
            if (Pixelated) { PixelDiamond(center, size, color); return; }
            Vector2 up = Vector2.up * size, side = Vector2.right * size * 0.55f;
            Quad(center - up, center - side, center + up, center + side, color, color, color, color);
        }

        // ---------------------------------------------------------------- pixel art

        private static float Snap(float value) => Mathf.Round(value / Pixel) * Pixel;
        private static Vector2 Snap(Vector2 value) => new Vector2(Snap(value.x), Snap(value.y));

        private void Block(float xMin, float yMin, float xMax, float yMax, Color color)
            => Quad(new Vector2(xMin, yMin), new Vector2(xMin, yMax), new Vector2(xMax, yMax), new Vector2(xMax, yMin), color, color, color, color);

        /// <summary>
        /// A flat band between two radii, one row of pixels at a time; <paramref name="squash"/> flattens it into an
        /// ellipse. An inner radius of 0 fills the whole disc.
        /// </summary>
        private void Annulus(Vector2 center, float r0, float r1, float squash, Color color)
        {
            if (color.a <= 0.01f || r1 <= 0f) return;
            center = Snap(center);
            r0 = Mathf.Max(0f, r0);
            int rows = Mathf.Max(1, Mathf.CeilToInt(r1 * squash / Pixel));
            for (int row = -rows; row < rows; row++)
            {
                float n = (row + 0.5f) * Pixel / squash;
                if (Mathf.Abs(n) >= r1) continue;
                float y = center.y + row * Pixel, outer = Mathf.Max(Pixel, Snap(Mathf.Sqrt(r1 * r1 - n * n)));
                float inner = Mathf.Abs(n) < r0 ? Snap(Mathf.Sqrt(r0 * r0 - n * n)) : 0f;
                if (inner <= 0f) { Block(center.x - outer, y, center.x + outer, y + Pixel, color); continue; }
                // A ring never thins to nothing, so its outline stays unbroken.
                outer = Mathf.Max(outer, inner + Pixel);
                Block(center.x - outer, y, center.x - inner, y + Pixel, color);
                Block(center.x + inner, y, center.x + outer, y + Pixel, color);
            }
        }

        /// <summary>A disc whose glow falls off in three flat bands instead of a gradient.</summary>
        private void PixelDisc(Vector2 center, float radius, float squash, Color inner, Color outer)
        {
            if (radius < Pixel * 3f) { Annulus(center, 0f, radius, squash, Color.Lerp(inner, outer, 0.3f)); return; }
            Annulus(center, 0f, radius * 0.4f, squash, inner);
            Annulus(center, radius * 0.4f, radius * 0.72f, squash, Color.Lerp(inner, outer, 0.45f));
            Annulus(center, radius * 0.72f, radius, squash, Color.Lerp(inner, outer, 0.8f));
        }

        /// <summary>A thick pixel line: one column (or row) of pixels per step, its colour fading in bands.</summary>
        private void PixelBar(Vector2 from, Vector2 direction, float length, float width, Color start, Color end)
        {
            if (length <= 0f) return;
            const int Bands = 5;
            bool wide = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
            float major = wide ? direction.x : direction.y, minor = wide ? direction.y : direction.x;
            if (Mathf.Abs(major) < 0.001f) return;
            float origin = Snap(wide ? from.x : from.y), across = wide ? from.y : from.x;
            float half = Mathf.Max(Pixel, width / Mathf.Abs(major)) * 0.5f, sign = Mathf.Sign(major);
            int steps = Mathf.Max(1, Mathf.RoundToInt(length * Mathf.Abs(major) / Pixel));
            // A straight bar needs only one block per colour band.
            int run = Mathf.Abs(minor) < 0.001f ? (start == end ? steps : Mathf.Max(1, Mathf.CeilToInt(steps / (float)Bands))) : 1;
            for (int i = 0; i < steps; i += run)
            {
                int count = Mathf.Min(run, steps - i);
                float u = (i + count * 0.5f) / steps;
                Color color = start == end ? start : Color.Lerp(start, end, (Mathf.Floor(u * Bands) + 0.5f) / Bands);
                if (color.a <= 0.01f) continue;
                float a = origin + sign * i * Pixel, b = a + sign * count * Pixel, mid = across + minor / Mathf.Abs(major) * (i + count * 0.5f) * Pixel;
                float low = Snap(mid - half), high = Mathf.Max(low + Pixel, Snap(mid + half));
                if (wide) Block(Mathf.Min(a, b), low, Mathf.Max(a, b), high, color);
                else Block(low, Mathf.Min(a, b), high, Mathf.Max(a, b), color);
            }
        }

        /// <summary>A small pixel gem: a single pixel when tiny, otherwise rows stepping out to a point.</summary>
        private void PixelDiamond(Vector2 center, float size, Color color)
        {
            if (color.a <= 0.01f) return;
            center = Snap(center);
            int n = Mathf.Clamp(Mathf.RoundToInt(size * 1.3f / Pixel), 1, 6);
            if (n == 1) { Block(center.x, center.y, center.x + Pixel, center.y + Pixel, color); return; }
            for (int row = -n; row < n; row++)
            {
                float half = Pixel * Mathf.Max(1, Mathf.RoundToInt((n - Mathf.Abs(row + 0.5f)) * 0.6f));
                Block(center.x - half, center.y + row * Pixel, center.x + half, center.y + (row + 1) * Pixel, color);
            }
        }

        /// <summary>
        /// A tongue of fire built from stacked rows of pixels in five flat colours, from a smoky outline to a white-hot
        /// core. Its edges ripple upward, a smaller tongue licks up one side and sparks break off the tip; it all
        /// flickers a few frames a second.
        /// </summary>
        private void PixelFlame(Vector2 root, Vector2 up, float width, float height, float seed, float alpha)
        {
            if (alpha <= 0.01f) return;
            float time = Mathf.Floor(Time.time * 12f) / 12f;
            height *= 0.7f + 0.3f * Mathf.Sin(time * (9f + seed * 7f) + seed * 40f) + 0.15f * Mathf.Sin(time * 23f + seed * 13f);
            Vector2 across = Vector2.Perpendicular(up), sway = across * Mathf.Sin(time * 6f + seed * 30f) * width * 0.35f;
            float ripple = time * 9f + seed * 30f;
            FlameLayer(root, up, sway, width * 1.2f, height * 1.05f, 8, ripple, Alpha(Ember, 0.6f * alpha));
            if (width >= Pixel * 4f)
            {
                Vector2 lick = root + across * (Mathf.Repeat(seed, 1f) < 0.5f ? -1f : 1f) * width * 0.3f;
                FlameLayer(lick, up, -sway * 0.6f, width * 0.42f, height * 0.6f, 5, ripple + 2f, Alpha(Crimson, 0.9f * alpha));
                FlameLayer(lick, up, -sway * 0.5f, width * 0.22f, height * 0.38f, 3, ripple + 2f, Alpha(Orange, alpha));
            }
            FlameLayer(root, up, sway, width, height, 8, ripple, Alpha(Crimson, 0.9f * alpha));
            FlameLayer(root, up, sway * 0.8f, width * 0.68f, height * 0.72f, 6, ripple, Alpha(Orange, alpha));
            FlameLayer(root, up, sway * 0.6f, width * 0.42f, height * 0.5f, 4, ripple, Alpha(Yellow, alpha));
            FlameLayer(root, up, sway * 0.4f, width * 0.22f, height * 0.28f, 2, ripple, Alpha(Core, alpha));
            for (int i = 0; i < 2; i++)
            {
                float s = Hash(seed * 13.7f + i, 4.2f), rise = Mathf.Repeat(time * (0.9f + s * 0.8f) + s, 1f);
                Vector2 spark = Snap(root + up * height * (0.75f + 0.7f * rise) + across * (s - 0.5f) * width * 1.1f + sway * rise);
                Block(spark.x, spark.y, spark.x + Pixel, spark.y + Pixel, Alpha(Color.Lerp(Yellow, Crimson, rise), (1f - rise) * alpha));
            }
        }

        /// <summary>One colour of a pixel flame: rows that swell just above the root, taper to a point and ripple sideways.</summary>
        private void FlameLayer(Vector2 root, Vector2 up, Vector2 sway, float width, float height, int rows, float ripple, Color color)
        {
            float step = Mathf.Max(Pixel, height / rows);
            Vector2 across = Vector2.Perpendicular(up);
            for (int i = 0; i * step < height; i++)
            {
                float u = (i + 0.5f) * step / Mathf.Max(step, height);
                float thick = Mathf.Max(Pixel, width * (u < 0.22f ? 0.78f + u : Mathf.Pow(1f - (u - 0.22f) / 0.78f, 0.8f)));
                Vector2 mid = root + up * (i + 0.5f) * step + sway * u * u + across * Mathf.Sin(ripple - u * 4.5f) * width * 0.16f * u;
                Cell(mid, up, thick, step, color);
            }
        }

        /// <summary>A snapped block <paramref name="thick"/> across and <paramref name="step"/> along <paramref name="up"/>.</summary>
        private void Cell(Vector2 mid, Vector2 up, float thick, float step, Color color)
        {
            float ax = Mathf.Abs(up.x), ay = Mathf.Abs(up.y);
            Vector2 half = new Vector2(ay * thick + ax * step, ax * thick + ay * step) * 0.5f;
            Vector2 min = Snap(mid - half);
            Block(min.x, min.y, Mathf.Max(min.x + Pixel, Snap(mid.x + half.x)), Mathf.Max(min.y + Pixel, Snap(mid.y + half.y)), color);
        }

        /// <summary>
        /// A pixel ice shard: each row has a lit facet, a bright ridge, a shaded facet and a dark edge, with the odd
        /// fracture line across it and a pale tip. A cross-shaped glint twinkles on its shoulder.
        /// </summary>
        private void PixelCrystal(Vector2 root, Vector2 up, float width, float height, float seed, Color glint, Color light, Color shade, Color dark, float alpha)
        {
            Vector2 across = Vector2.Perpendicular(up);
            float step = Mathf.Max(Pixel, height / 8f);
            Color g = Alpha(glint, alpha), l = Alpha(light, alpha), s = Alpha(shade, alpha), d = Alpha(dark, alpha);
            for (int i = 0; i * step < height; i++)
            {
                float u = (i + 0.5f) * step / Mathf.Max(step, height);
                bool tip = u >= 0.6f;
                float half = width * 0.5f * (tip ? (1f - u) / 0.4f : 0.72f + 0.28f * u / 0.6f);
                Vector2 mid = root + up * (i + 0.5f) * step;
                if (half < Pixel * 1.2f) { Cell(mid, up, Pixel, step, tip ? g : l); continue; }
                // From the lit side across to the shaded one.
                float ridge = half * 0.25f;
                bool fracture = !tip && Hash(seed * 31.3f + i, 2.7f) > 0.8f;
                Cell(mid + across * (half + ridge + Pixel * 0.5f) * 0.5f, up, half - ridge - Pixel * 0.5f, step, tip ? g : fracture ? g : l);
                Cell(mid + across * ridge, up, Pixel, step, g);
                Cell(mid + across * (ridge - half + Pixel * 0.5f) * 0.5f, up, half + ridge - Pixel * 1.5f, step, tip ? l : fracture ? d : s);
                Cell(mid - across * (half - Pixel * 0.5f), up, Pixel, step, d);
            }
            if (width < Pixel * 3f || Hash(seed * 17.3f, Mathf.Floor(Time.time * 5f)) < 0.82f) return;
            Vector2 spot = Snap(root + up * height * 0.6f + across * width * 0.25f);
            Color white = Alpha(Color.white, alpha);
            Block(spot.x - Pixel, spot.y, spot.x + Pixel * 2f, spot.y + Pixel, white);
            Block(spot.x, spot.y - Pixel, spot.x + Pixel, spot.y + Pixel * 2f, white);
        }

        public static Vector2 Polar(float angle, float radius) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        public static Color Alpha(Color color, float alpha) { color.a *= Mathf.Clamp01(alpha); return color; }

        /// <summary>A stable pseudo-random value in [0, 1) for effect seeds.</summary>
        public static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
