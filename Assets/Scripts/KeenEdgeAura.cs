using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// While Sharpened Dagger is up, one little blade per point of bonus damage circles the Assassin, points outward,
    /// on a razor-thin ring that a glint keeps sweeping around. It flickers as the edge is about to dull. Purely visual.
    /// </summary>
    public sealed class KeenEdgeAura : MonoBehaviour
    {
        private const float Orbit = 0.8f;
        private const int MaxBlades = 8;
        private static readonly List<KeenEdgeAura> active = new List<KeenEdgeAura>();
        private FlameMesh mesh;
        private Transform hero;
        private int blades;
        private float age, until, popAt = float.NegativeInfinity;

        /// <summary>Starts the aura around <paramref name="hero"/>, or refreshes the one already there with the new bonus.</summary>
        public static void Show(Transform root, Transform hero, int bonus, float duration)
        {
            if (root == null || hero == null) return;
            foreach (var existing in active)
            {
                if (existing == null || existing.hero != hero) continue;
                if (bonus > existing.blades) existing.popAt = existing.age;
                existing.blades = bonus;
                existing.until = existing.age + duration;
                return;
            }
            var aura = new GameObject("Keen edge").AddComponent<KeenEdgeAura>();
            aura.transform.SetParent(root, false);
            aura.hero = hero;
            aura.blades = bonus;
            aura.until = duration;
            aura.popAt = 0f;
            aura.mesh = new FlameMesh(aura.gameObject, 8);
            active.Add(aura);
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (hero == null || age >= until) { Destroy(gameObject); return; }
            float remaining = until - age;
            float fade = Mathf.Clamp01(remaining / 0.4f) * Mathf.Clamp01(age / 0.2f);
            if (remaining < 1.5f) fade *= 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(age * 14f));
            Color edge = SharpenedDagger.EdgeColor;
            Vector2 center = hero.position;

            mesh.Begin();
            mesh.Ring(center, Orbit, 0.025f, FlameMesh.Alpha(edge, 0.35f * fade), 64);
            // A glint racing around the ring, trailing off behind it.
            float glintAngle = age * 3.2f;
            for (int i = 0; i < 6; i++)
                mesh.Diamond(center + FlameMesh.Polar(glintAngle - i * 0.07f, Orbit), 0.07f * (1f - i / 6f), FlameMesh.Alpha(Color.white, fade * (1f - i / 6f)));

            int shown = Mathf.Min(blades, MaxBlades);
            float spin = age * 1.8f;
            for (int i = 0; i < shown; i++)
            {
                float a = spin + i * Mathf.PI * 2f / shown;
                Vector2 outward = FlameMesh.Polar(a, 1f), across = Vector2.Perpendicular(outward);
                // The newest blade pops in large and settles.
                float pop = i == shown - 1 ? Mathf.Clamp01((age - popAt) / 0.25f) : 1f;
                float s = 1.7f - 0.7f * (1f - (1f - pop) * (1f - pop));
                Vector2 at = center + outward * Orbit;
                Vector2 tip = at + outward * 0.2f * s, hilt = at - outward * 0.04f * s;
                BladeShape(hilt - across * 0.05f * s, tip, hilt + across * 0.05f * s, fade);
                mesh.Bar(hilt - across * 0.08f * s, across, 0.16f * s, 0.035f * s, FlameMesh.Alpha(ShadowstepVfx.Smoke, fade), FlameMesh.Alpha(ShadowstepVfx.Smoke, fade));
                // Each blade flashes as the glint passes it.
                float glint = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, glintAngle * Mathf.Rad2Deg)) / 25f);
                if (glint > 0f)
                {
                    mesh.Bar(tip - outward * 0.12f * glint, outward, 0.24f * glint, 0.03f, FlameMesh.Alpha(Color.white, glint * fade), FlameMesh.Alpha(Color.white, glint * fade));
                    mesh.Bar(tip - across * 0.1f * glint, across, 0.2f * glint, 0.03f, FlameMesh.Alpha(Color.white, glint * fade), FlameMesh.Alpha(Color.white, glint * fade));
                }
            }
            mesh.Commit();
        }

        private void BladeShape(Vector2 left, Vector2 tip, Vector2 right, float fade)
        {
            Color steel = new Color(0.72f, 0.78f, 0.9f, fade), bright = new Color(1f, 1f, 1f, fade);
            mesh.Triangle(left, tip, right, steel, bright, steel);
        }

        private void OnDestroy()
        {
            active.Remove(this);
            mesh?.Release();
        }
    }
}
