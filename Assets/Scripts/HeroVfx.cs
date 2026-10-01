using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Mid-intensity combat effects for the regular heroes: slash sweeps, hit sparks, pulses,
    /// rising motes and a soft ground aura. Each effect is one small mesh sharing a single material.
    /// </summary>
    public sealed class HeroVfx : MonoBehaviour
    {
        private enum Shape { Slash, Sparks, Pulse, Motes, Aura }
        private const int EffectLimit = 48;
        private const float Tau = Mathf.PI * 2f;
        private static readonly List<HeroVfx> active = new List<HeroVfx>(EffectLimit);
        private static Material sharedMaterial;
        private static int materialUsers;
        private readonly List<Vector3> vertices = new List<Vector3>(512);
        private readonly List<Color> colors = new List<Color>(512);
        private readonly List<int> triangles = new List<int>(768);
        private Mesh mesh;
        private Shape shape;
        private Color color;
        private Vector2 direction;
        private float age, duration, radius, cone, size;
        private Vector2[] velocities;
        private float[] seeds;

        /// <summary>A bright crescent that sweeps across the attack cone, trailing a fading band.</summary>
        public static void Slash(Transform parent, Vector2 center, Vector2 aim, float reach, float coneAngle, Color color, float duration = 0.2f)
        {
            if (aim.sqrMagnitude < 0.0001f) return;
            var effect = Create(parent, center, Shape.Slash, duration, color);
            if (effect == null) return;
            effect.direction = aim.normalized;
            effect.radius = Mathf.Max(0.3f, reach);
            effect.cone = Mathf.Clamp(coneAngle, 10f, 360f) * Mathf.Deg2Rad;
            effect.Draw();
        }

        /// <summary>Short streaks that fly outward and slow down. A direction biases them into a spray.</summary>
        public static void Sparks(Transform parent, Vector2 center, Color color, int count = 8, float speed = 4f,
            float duration = 0.3f, Vector2? spray = null, float spreadDegrees = 360f, float size = 1f)
        {
            var effect = Create(parent, center, Shape.Sparks, duration, color);
            if (effect == null) return;
            count = Mathf.Clamp(count, 1, 40);
            effect.size = size;
            effect.velocities = new Vector2[count];
            effect.seeds = new float[count];
            float baseAngle = spray.HasValue && spray.Value.sqrMagnitude > 0.0001f ? Mathf.Atan2(spray.Value.y, spray.Value.x) : 0f;
            float spread = Mathf.Clamp(spreadDegrees, 0f, 360f) * Mathf.Deg2Rad;
            for (int i = 0; i < count; i++)
            {
                float angle = spray.HasValue ? baseAngle + Random.Range(-0.5f, 0.5f) * spread : Random.value * Tau;
                effect.velocities[i] = Polar(angle, speed * Random.Range(0.55f, 1.15f));
                effect.seeds[i] = Random.value;
            }
            effect.Draw();
        }

        /// <summary>A soft expanding disc with a crisp edge; good for casts, charge-ready pings and impacts.</summary>
        public static void Pulse(Transform parent, Vector2 center, float radius, Color color, float duration = 0.35f)
        {
            var effect = Create(parent, center, Shape.Pulse, duration, color);
            if (effect == null) return;
            effect.radius = Mathf.Clamp(radius, 0.1f, 12f);
            effect.Draw();
        }

        /// <summary>Small glowing motes that drift upward inside a radius (blessings, heals, wards).</summary>
        public static void Motes(Transform parent, Vector2 center, float radius, Color color, int count = 12, float duration = 0.9f)
        {
            var effect = Create(parent, center, Shape.Motes, duration, color);
            if (effect == null) return;
            count = Mathf.Clamp(count, 1, 40);
            effect.radius = Mathf.Clamp(radius, 0.1f, 8f);
            effect.velocities = new Vector2[count];
            effect.seeds = new float[count];
            for (int i = 0; i < count; i++)
            {
                effect.velocities[i] = Random.insideUnitCircle * effect.radius;
                effect.seeds[i] = Random.value;
            }
            effect.Draw();
        }

        /// <summary>A faint class-coloured ring under the hero's feet with two slow orbiting motes.</summary>
        public static void Aura(Transform hero, Color color)
        {
            if (hero == null) return;
            foreach (var existing in hero.GetComponentsInChildren<HeroVfx>())
                if (existing.shape == Shape.Aura) return;
            var aura = Create(hero, hero.position, Shape.Aura, 0f, color);
            aura.Draw();
        }

        private static HeroVfx Create(Transform parent, Vector2 center, Shape shape, float duration, Color color)
        {
            if (parent == null) return null;
            if (shape != Shape.Aura && active.Count >= EffectLimit)
            {
                var oldest = active[0];
                active.RemoveAt(0);
                if (oldest != null) { oldest.gameObject.SetActive(false); Destroy(oldest.gameObject); }
            }
            var effect = new GameObject("Hero " + shape).AddComponent<HeroVfx>();
            effect.transform.SetParent(parent, false);
            effect.transform.position = center;
            // Keep world-space sizes even when parented to a scaled hero sprite.
            Vector3 scale = parent.lossyScale;
            effect.transform.localScale = new Vector3(Safe(scale.x), Safe(scale.y), 1f);
            effect.shape = shape;
            effect.duration = duration;
            effect.color = color;
            if (shape != Shape.Aura) active.Add(effect);
            if (sharedMaterial == null)
                sharedMaterial = new Material(Shader.Find("Sprites/Default"))
                { name = "Hero effects (shared)", hideFlags = HideFlags.HideAndDontSave };
            materialUsers++;
            effect.mesh = new Mesh { name = "Hero effect geometry" };
            effect.mesh.MarkDynamic();
            effect.gameObject.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            var renderer = effect.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;
            renderer.sortingOrder = shape == Shape.Aura ? 3 : 9;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return effect;
        }

        private static float Safe(float scale) => Mathf.Abs(scale) > 0.0001f ? 1f / scale : 1f;

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
                case Shape.Slash: DrawSlash(t); break;
                case Shape.Sparks: DrawSparks(t); break;
                case Shape.Pulse: DrawPulse(t); break;
                case Shape.Motes: DrawMotes(t); break;
                case Shape.Aura: DrawAura(); break;
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private void DrawSlash(float t)
        {
            float aim = Mathf.Atan2(direction.y, direction.x);
            float start = aim - cone * 0.5f;
            float sweep = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.8f));
            float fade = 1f - Mathf.Pow(t, 1.6f);
            float head = start + cone * sweep;
            float tail = Mathf.Max(start, head - cone * 0.75f);
            const int segments = 18;
            for (int i = 0; i < segments; i++)
            {
                float u0 = i / (float)segments, u1 = (i + 1) / (float)segments;
                float a0 = Mathf.Lerp(tail, head, u0), a1 = Mathf.Lerp(tail, head, u1);
                // Band gets thicker and brighter toward the leading edge.
                float w0 = Mathf.Lerp(0.05f, 0.34f, u0) * radius * 0.45f, w1 = Mathf.Lerp(0.05f, 0.34f, u1) * radius * 0.45f;
                Color c0 = Alpha(color, fade * u0 * 0.55f), c1 = Alpha(color, fade * u1 * 0.55f);
                Quad(Polar(a0, radius - w0), Polar(a0, radius), Polar(a1, radius), Polar(a1, radius - w1), c0, c0, c1, c1);
                Color e0 = Alpha(Color.white, fade * u0 * 0.9f), e1 = Alpha(Color.white, fade * u1 * 0.9f);
                Quad(Polar(a0, radius - 0.035f), Polar(a0, radius + 0.015f), Polar(a1, radius + 0.015f), Polar(a1, radius - 0.035f), e0, e0, e1, e1);
            }
            // A glint riding the leading edge.
            Diamond(Polar(head, radius - 0.05f), head, 0.2f * fade + 0.05f, Alpha(Color.white, fade));
        }

        private void DrawSparks(float t)
        {
            float fade = 1f - t;
            float travel = (1f - Mathf.Pow(1f - t, 2.2f)) * duration; // eases out, like drag
            for (int i = 0; i < velocities.Length; i++)
            {
                Vector2 v = velocities[i];
                Vector2 p = v * travel;
                Vector2 tailPoint = p - v.normalized * (0.08f + v.magnitude * 0.05f * fade) * size;
                Color c = Color.Lerp(Color.white, color, Mathf.Min(1f, t * 2f + seeds[i] * 0.3f));
                Stroke(tailPoint, p, 0.05f * size * (0.5f + fade), Alpha(c, fade));
            }
        }

        private void DrawPulse(float t)
        {
            float grow = 1f - Mathf.Pow(1f - t, 3f);
            float r = Mathf.Lerp(radius * 0.2f, radius, grow);
            float fade = 1f - t;
            Disc(Vector2.zero, r, Alpha(color, 0.16f * fade), 40);
            Ring(Vector2.zero, r, 0.08f * (0.4f + fade), Alpha(color, 0.9f * fade), 40);
            Ring(Vector2.zero, r * 0.72f, 0.03f, Alpha(Color.white, 0.45f * fade), 32);
        }

        private void DrawMotes(float t)
        {
            float fade = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            for (int i = 0; i < velocities.Length; i++)
            {
                float rise = (t + seeds[i] * 0.3f) * 1.3f;
                Vector2 p = velocities[i] + new Vector2(Mathf.Sin((t + seeds[i]) * 9f) * 0.08f, rise);
                float s = 0.06f + seeds[i] * 0.05f;
                Diamond(p, Mathf.PI * 0.5f, s * 1.6f, Alpha(color, fade * 0.35f));
                Diamond(p, Mathf.PI * 0.5f, s, Alpha(Color.Lerp(color, Color.white, 0.5f), fade));
            }
        }

        private void DrawAura()
        {
            float pulse = 0.75f + Mathf.Sin(age * 2.2f) * 0.25f;
            Vector2 feet = new Vector2(0f, -0.43f);
            // Squashed ellipse reads as a ground ring in top-down view.
            EllipseRing(feet, 0.55f, 0.2f, 0.035f, Alpha(color, 0.35f * pulse), 36);
            EllipseDisc(feet, 0.5f, 0.17f, Alpha(color, 0.08f * pulse), 28);
            for (int i = 0; i < 2; i++)
            {
                float a = age * 1.4f + i * Mathf.PI;
                Vector2 p = feet + new Vector2(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.2f);
                Diamond(p, Mathf.PI * 0.5f, 0.07f, Alpha(Color.Lerp(color, Color.white, 0.4f), 0.7f));
            }
        }

        private void Ring(Vector2 center, float r, float width, Color c, int segments) => EllipseRing(center, r, r, width, c, segments);
        private void Disc(Vector2 center, float r, Color c, int segments) => EllipseDisc(center, r, r, c, segments);

        private void EllipseRing(Vector2 center, float rx, float ry, float width, Color c, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Tau / segments, a1 = (i + 1) * Tau / segments;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Vector2 o0 = center + new Vector2(d0.x * rx, d0.y * ry), o1 = center + new Vector2(d1.x * rx, d1.y * ry);
                Quad(o0 - d0 * width * 0.5f, o0 + d0 * width * 0.5f, o1 + d1 * width * 0.5f, o1 - d1 * width * 0.5f, c, c, c, c);
            }
        }

        private void EllipseDisc(Vector2 center, float rx, float ry, Color c, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Tau / segments, a1 = (i + 1) * Tau / segments;
                Triangle(center, center + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry),
                    center + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry), c);
            }
        }

        private void Diamond(Vector2 center, float angle, float s, Color c)
        {
            Vector2 f = Polar(angle, s), side = Vector2.Perpendicular(f) * 0.45f;
            Triangle(center + f, center + side, center - f, c);
            Triangle(center + f, center - f, center - side, c);
        }

        private void Stroke(Vector2 from, Vector2 to, float width, Color c)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return;
            Vector2 n = Vector2.Perpendicular(delta).normalized * width * 0.5f;
            Quad(from - n, from + n, to + n, to - n, c, c, c, c);
        }

        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            colors.Add(ca); colors.Add(cb); colors.Add(cc); colors.Add(cd);
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

        private static Vector2 Polar(float angle, float r) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
        private static Color Alpha(Color c, float a) { c.a *= Mathf.Clamp01(a); return c; }

        private void OnDestroy()
        {
            active.Remove(this);
            if (mesh != null) Destroy(mesh);
            if (--materialUsers <= 0 && sharedMaterial != null)
            { materialUsers = 0; Destroy(sharedMaterial); sharedMaterial = null; }
        }
    }
}
