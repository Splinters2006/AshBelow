using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// A world-space mesh rebuilt every frame for hellfire effects: fills, rings, flickering flame tongues and embers.
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

        public FlameMesh(GameObject owner, int sortingOrder)
        {
            if (sharedMaterial == null)
                sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Hellfire (shared)", hideFlags = HideFlags.HideAndDontSave };
            materialUsers++;
            mesh = new Mesh { name = "Hellfire geometry" };
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
            => Quad(min, new Vector2(min.x, max.y), max, new Vector2(max.x, min.y), color, color, color, color);

        /// <summary>A rectangle from <paramref name="from"/> along <paramref name="direction"/>, fading between two colours.</summary>
        public void Bar(Vector2 from, Vector2 direction, float length, float width, Color start, Color end)
        {
            Vector2 side = Vector2.Perpendicular(direction) * width * 0.5f, to = from + direction * length;
            Quad(from - side, from + side, to + side, to - side, start, start, end, end);
        }

        public void Disc(Vector2 center, float radius, Color inner, Color outer, int segments = 40)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Tau / segments, a1 = (i + 1) * Tau / segments;
                Triangle(center, center + Polar(a0, radius), center + Polar(a1, radius), inner, outer, outer);
            }
        }

        public void Ring(Vector2 center, float radius, float width, Color color, int segments = 48) => Ring(center, radius, width, color, color, segments);

        public void Ring(Vector2 center, float radius, float width, Color inner, Color outer, int segments = 48)
        {
            float r0 = Mathf.Max(0f, radius - width * 0.5f), r1 = radius + width * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Tau / segments, a1 = (i + 1) * Tau / segments;
                Quad(center + Polar(a0, r0), center + Polar(a0, r1), center + Polar(a1, r1), center + Polar(a1, r0), inner, outer, outer, inner);
            }
        }

        public void Ellipse(Vector2 center, float rx, float ry, Color inner, Color outer, int segments = 28)
        {
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
            float time = Time.time;
            float flicker = 0.7f + 0.3f * Mathf.Sin(time * (9f + seed * 7f) + seed * 40f) + 0.15f * Mathf.Sin(time * 23f + seed * 13f);
            height *= flicker;
            Vector2 side = Vector2.Perpendicular(up) * width * 0.5f;
            Vector2 sway = Vector2.Perpendicular(up) * Mathf.Sin(time * 6f + seed * 30f) * width * 0.35f;
            Vector2 tip = root + up * height + sway;
            Triangle(root - side, tip, root + side, Alpha(Crimson, 0.85f * alpha), Alpha(Orange, 0f), Alpha(Crimson, 0.85f * alpha));
            Vector2 mid = root + up * height * 0.7f + sway * 0.8f;
            Triangle(root - side * 0.65f, mid, root + side * 0.65f, Alpha(Orange, alpha), Alpha(Yellow, 0.1f * alpha), Alpha(Orange, alpha));
            Vector2 core = root + up * height * 0.38f + sway * 0.5f;
            Triangle(root - side * 0.3f, core, root + side * 0.3f, Alpha(Core, alpha), Alpha(Yellow, 0.4f * alpha), Alpha(Core, alpha));
        }

        public void Diamond(Vector2 center, float size, Color color)
        {
            Vector2 up = Vector2.up * size, side = Vector2.right * size * 0.55f;
            Quad(center - up, center - side, center + up, center + side, color, color, color, color);
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
