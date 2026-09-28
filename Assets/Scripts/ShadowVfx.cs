using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>Layered shadow magic drawn in one mesh per effect, with a shared material.</summary>
    public sealed class ShadowVfx : MonoBehaviour
    {
        private enum Shape { Rift, Execution, Death, Singularity, Aura }
        private const int EffectLimit = 32;
        private const float Tau = Mathf.PI * 2f;
        private static readonly List<ShadowVfx> active = new List<ShadowVfx>(EffectLimit);
        private static Material sharedMaterial;
        private static int materialUsers;
        private static readonly Color Void = new Color(0.025f, 0.012f, 0.07f);
        private static readonly Color Violet = new Color(0.43f, 0.12f, 0.95f);
        private static readonly Color Pink = new Color(0.98f, 0.3f, 0.92f);
        private static readonly Color Ice = new Color(0.35f, 0.94f, 1f);
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Color> colors = new List<Color>(4096);
        private readonly List<int> triangles = new List<int>(6144);
        private Mesh mesh;
        private Shape shape;
        private Vector2 direction;
        private float age, duration, radius, strength;
        private bool ownsMaterial;

        public static void Rift(Transform parent, Vector2 from, Vector2 to, float strength = 1f)
        {
            var effect = Create(parent, from, Shape.Rift, 0.48f);
            effect.direction = to - from;
            effect.strength = Mathf.Clamp(strength, 0.25f, 3f);
            effect.Draw();
        }

        public static void Execution(Transform parent, Vector2 center, float radius)
        {
            var effect = Create(parent, center, Shape.Execution, 1.05f);
            effect.radius = Mathf.Clamp(radius, 0.5f, 16f);
            effect.Draw();
        }

        public static void Death(Transform parent, Vector2 center)
        {
            var effect = Create(parent, center, Shape.Death, 0.65f);
            effect.Draw();
        }

        public static void Singularity(Transform parent, Vector2 center, float radius, float duration = 1.1f)
        {
            var effect = Create(parent, center, Shape.Singularity, Mathf.Max(0.35f, duration));
            effect.radius = Mathf.Clamp(radius, 0.5f, 12f);
            effect.Draw();
        }

        public static void Aura(Transform hero)
        {
            if (hero == null) return;
            foreach (var effect in hero.GetComponentsInChildren<ShadowVfx>())
                if (effect.shape == Shape.Aura) return;
            var aura = Create(hero, hero.position, Shape.Aura, 0f);
            aura.Draw();
        }

        private static ShadowVfx Create(Transform parent, Vector2 center, Shape shape, float duration)
        {
            // Reap old death bursts first so a large execution keeps its signature ring visible.
            if (shape != Shape.Aura && active.Count >= EffectLimit)
            {
                int index = active.FindIndex(effect => effect != null && effect.shape == Shape.Death);
                if (index < 0) index = 0;
                var oldest = active[index];
                active.RemoveAt(index);
                if (oldest != null) { oldest.gameObject.SetActive(false); Destroy(oldest.gameObject); }
            }
            var effect = new GameObject("Shadow " + shape).AddComponent<ShadowVfx>();
            effect.transform.SetParent(parent, false);
            effect.transform.position = center;
            effect.shape = shape;
            effect.duration = duration;
            if (shape != Shape.Aura) active.Add(effect);
            if (sharedMaterial == null)
                sharedMaterial = new Material(Shader.Find("Sprites/Default"))
                { name = "Shadow magic (shared)", hideFlags = HideFlags.HideAndDontSave };
            materialUsers++;
            effect.ownsMaterial = true;
            effect.mesh = new Mesh { name = "Shadow magic geometry" };
            effect.mesh.MarkDynamic();
            effect.gameObject.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            var renderer = effect.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            renderer.sortingOrder = shape == Shape.Aura ? 4 : 10;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return effect;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            age += Time.deltaTime;
            if (shape != Shape.Aura && age >= duration) { Destroy(gameObject); return; }
            Draw();
        }

        private void Draw()
        {
            vertices.Clear(); colors.Clear(); triangles.Clear();
            float t = duration > 0f ? Mathf.Clamp01(age / duration) : 0f;
            switch (shape)
            {
                case Shape.Rift: DrawRift(t); break;
                case Shape.Execution: DrawExecution(t); break;
                case Shape.Death: DrawDeath(t); break;
                case Shape.Singularity: DrawSingularity(t); break;
                case Shape.Aura: DrawAura(); break;
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private void DrawRift(float t)
        {
            Vector2 aim = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            Vector2 normal = Vector2.Perpendicular(aim);
            float fade = Mathf.Pow(1f - t, 1.35f);
            float width = (0.14f + strength * 0.1f) * (1f + t * 0.7f);
            Vector2 previous = Vector2.zero;
            const int steps = 28;
            for (int i = 1; i <= steps; i++)
            {
                float u = i / (float)steps;
                Vector2 next = direction * u + normal * Mathf.Sin(u * Mathf.PI) * (0.38f + t * 0.25f);
                float w = width * Mathf.Sin((u * 0.9f + 0.05f) * Mathf.PI);
                Stroke(previous, next, w * 2.8f, Alpha(Violet, fade * 0.18f));
                Stroke(previous, next, w * 1.5f, Alpha(Void, fade * 0.94f));
                Stroke(previous + normal * w, next + normal * w, 0.025f, Alpha(Pink, fade));
                Stroke(previous - normal * w, next - normal * w, 0.017f, Alpha(Ice, fade * 0.9f));
                previous = next;
            }
            float angle = Mathf.Atan2(aim.y, aim.x);
            Arc(direction, 0.6f + strength * 0.27f + t * 0.5f, angle - 1.25f, 2.5f,
                0.13f * fade, Alpha(Void, fade), 24);
            Arc(direction, 0.7f + strength * 0.27f + t * 0.5f, angle - 1.25f, 2.5f,
                0.045f * fade, Alpha(Pink, fade), 24);
            for (int i = 0; i < 9; i++)
            {
                float u = (i + 0.5f) / 9f;
                Vector2 p = direction * u + normal * Mathf.Sin(i * 7.13f) * t * 1.7f;
                Shard(p, angle + i * 2.4f + t * 3f, 0.1f + (1f - t) * 0.15f, Alpha(i % 3 == 0 ? Ice : Violet, fade));
            }
        }

        private void DrawExecution(float t)
        {
            float fade = Mathf.Clamp01((1f - t) * 1.8f);
            float burst = 1f - Mathf.Pow(1f - t, 4f);
            float r = Mathf.Lerp(0.3f, radius, burst);
            Disc(Vector2.zero, r, Alpha(Void, fade * 0.09f), 48);
            Arc(Vector2.zero, r, age * 0.4f, Tau, 0.2f, Alpha(Violet, fade * 0.2f), 72);
            Arc(Vector2.zero, r, age * 0.4f, Tau, 0.035f, Alpha(Pink, fade * 0.8f), 72);
            Arc(Vector2.zero, r * 0.84f, -age * 0.5f, Tau, 0.02f, Alpha(Ice, fade * 0.52f), 64);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Tau / 12f + age * 0.18f;
                Rune(Polar(angle, r * 0.91f), angle, Mathf.Min(0.3f, r * 0.09f), Alpha(i % 3 == 0 ? Ice : Pink, fade * 0.85f));
            }
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 2.39996f + t * 0.6f;
                float distance = r * (0.3f + (i % 7) * 0.095f);
                Shard(Polar(angle, distance), angle + t * 4f, (0.12f + i % 3 * 0.08f) * fade,
                    Alpha(i % 4 == 0 ? Ice : Violet, fade * 0.75f));
            }
            Star(Vector2.zero, 0.7f + burst * 0.7f, age * 0.5f, Alpha(Pink, fade * 0.55f));
        }

        private void DrawDeath(float t)
        {
            float fade = 1f - t;
            float r = 0.15f + t * 0.95f;
            Arc(Vector2.zero, r, t * 4f, Tau, 0.06f * fade, Alpha(Violet, fade * 0.65f), 24);
            Vector2 top = Vector2.up * (0.55f + t * 1.1f);
            Stroke(Vector2.down * 0.2f, top, 0.28f * fade, Alpha(Void, fade));
            Stroke(Vector2.down * 0.1f, top, 0.025f * fade, Alpha(Pink, fade));
            for (int i = 0; i < 11; i++)
            {
                float angle = i * 2.39996f;
                Vector2 p = Polar(angle + t, r * (0.4f + i % 3 * 0.3f)) + Vector2.up * t * 0.65f;
                Shard(p, angle - t * 4f, fade * (0.1f + i % 3 * 0.05f), Alpha(i % 3 == 0 ? Ice : Pink, fade));
            }
        }

        private void DrawSingularity(float t)
        {
            float fade = Mathf.Clamp01((1f - t) * 4f);
            float expand = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 5f));
            float r = radius * expand;
            Disc(Vector2.zero, r * 0.34f, Alpha(Void, fade * 0.84f), 48);
            for (int layer = 0; layer < 3; layer++)
            {
                float ring = r * (0.45f + layer * 0.24f);
                float spin = age * (layer % 2 == 0 ? 1.4f : -1.1f) + layer;
                for (int arc = 0; arc < 3; arc++)
                {
                    Arc(Vector2.zero, ring, spin + arc * Tau / 3f, 1.55f, 0.12f,
                        Alpha(Violet, fade * 0.28f), 20);
                    Arc(Vector2.zero, ring, spin + arc * Tau / 3f, 1.55f, 0.025f,
                        Alpha(layer == 1 ? Ice : Pink, fade * 0.85f), 20);
                }
            }
            for (int i = 0; i < 24; i++)
            {
                float orbit = Mathf.Repeat(i * 0.173f - t * 1.6f, 1f);
                float angle = i * 2.39996f + age * 3f;
                Shard(Polar(angle, r * (0.18f + orbit * 0.82f)), angle + 1f,
                    (0.08f + orbit * 0.19f) * fade, Alpha(i % 4 == 0 ? Ice : Violet, orbit * fade));
            }
            Star(Vector2.zero, r * 0.27f, -age, Alpha(Pink, fade * 0.8f));
        }

        private void DrawAura()
        {
            float pulse = 0.8f + Mathf.Sin(age * 2.5f) * 0.2f;
            Vector2 feet = new Vector2(0f, -0.35f);
            Arc(feet, 0.8f, age * 0.35f, Tau, 0.055f, Alpha(Violet, 0.23f), 48);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Tau / 6f + age * 0.35f;
                Rune(feet + Polar(angle, 0.79f), angle, 0.07f, Alpha(i % 2 == 0 ? Ice : Pink, 0.65f * pulse));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 inner = new Vector2(side * 0.27f, 0.25f);
                Vector2 outer = new Vector2(side * (0.8f + pulse * 0.1f), 0.3f + Mathf.Sin(age * 2f) * 0.08f);
                Vector2 tail = new Vector2(side * 0.38f, -0.73f);
                Triangle(inner, outer, tail, Alpha(Void, 0.85f));
                Stroke(outer, tail, 0.025f, Alpha(Violet, 0.75f));
                Shard(new Vector2(side * 0.58f, 0.76f + Mathf.Sin(age * 2.3f + side) * 0.08f),
                    age * side * 0.7f, 0.13f, Alpha(Pink, 0.7f * pulse));
            }
        }

        private void Star(Vector2 center, float size, float angle, Color color)
        {
            for (int i = 0; i < 4; i++)
                Shard(center + Polar(angle + i * Mathf.PI * 0.5f, size * 0.32f),
                    angle + i * Mathf.PI * 0.5f, size * 0.46f, color);
        }

        private void Rune(Vector2 center, float angle, float size, Color color)
        {
            Vector2 a = Polar(angle, size), b = Vector2.Perpendicular(a) * 0.55f;
            Stroke(center - a, center + a, 0.022f, color);
            Stroke(center - a * 0.5f + b, center + a * 0.5f - b, 0.018f, color);
            Stroke(center + a * 0.5f + b, center + a, 0.018f, color);
        }

        private void Shard(Vector2 center, float angle, float size, Color color)
        {
            Vector2 forward = Polar(angle, size), side = Vector2.Perpendicular(forward) * 0.32f;
            Triangle(center + forward, center + side, center - forward, color);
            Triangle(center + forward, center - forward, center - side, Alpha(color, 0.4f));
        }

        private void Arc(Vector2 center, float radius, float start, float span, float width, Color color, int segments)
        {
            Vector2 previous = center + Polar(start, radius);
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = center + Polar(start + span * i / segments, radius);
                Stroke(previous, next, width, color);
                previous = next;
            }
        }

        private void Disc(Vector2 center, float radius, Color color, int segments)
        {
            for (int i = 0; i < segments; i++)
                Triangle(center, center + Polar(i * Tau / segments, radius),
                    center + Polar((i + 1) * Tau / segments, radius), color);
        }

        private void Stroke(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 normal = Vector2.Perpendicular(to - from).normalized * width * 0.5f;
            int index = vertices.Count;
            vertices.Add(from - normal); vertices.Add(from + normal);
            vertices.Add(to + normal); vertices.Add(to - normal);
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }

        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }

        private static Vector2 Polar(float angle, float radius) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        private static Color Alpha(Color color, float alpha) { color.a *= alpha; return color; }

        private void OnDestroy()
        {
            active.Remove(this);
            if (mesh != null) Destroy(mesh);
            if (ownsMaterial && --materialUsers == 0 && sharedMaterial != null)
            { Destroy(sharedMaterial); sharedMaterial = null; }
        }
    }
}
