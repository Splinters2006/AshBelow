using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Divine Intervention: three little guardian angels circle the marked hero for as long as the mark lasts, bobbing
    /// and beating their wings in a warm glow, passing behind the hero on the far side of their circle, with a ring of
    /// light at the hero's feet. If the hero is saved, a pillar of light slams down on them, a golden shockwave rolls
    /// out and the angels flare and soar away; otherwise they quietly fade.
    /// </summary>
    public sealed class GuardianAngelsVfx : MonoBehaviour
    {
        private const int Count = 3;
        private const float Orbit = 0.75f, Scale = 0.6f, FadeTime = 0.35f, LeaveTime = 1.1f;
        private static readonly Color Glow = new Color(1f, 0.93f, 0.62f);
        private static readonly Dictionary<Transform, GuardianAngelsVfx> watching = new Dictionary<Transform, GuardianAngelsVfx>();
        private Transform root, hero;
        private float age, duration, leftAt = -1f;
        private FlameMesh mesh;
        private readonly SpriteRenderer[] bodies = new SpriteRenderer[Count], leftWings = new SpriteRenderer[Count], rightWings = new SpriteRenderer[Count];

        public static void Play(Transform root, Transform hero, float duration)
        {
            if (root == null || hero == null) return;
            if (watching.TryGetValue(hero, out var existing) && existing != null && existing.leftAt < 0f)
            {
                existing.duration = Mathf.Max(existing.duration, existing.age + duration);
                return;
            }
            var angels = new GameObject("Guardian angels").AddComponent<GuardianAngelsVfx>();
            // The owner stays at the origin: FlameMesh draws in its local space.
            angels.transform.SetParent(root, false);
            angels.root = root;
            angels.hero = hero;
            angels.duration = duration;
            angels.mesh = new FlameMesh(angels.gameObject, 3);
            for (int i = 0; i < Count; i++)
            {
                angels.leftWings[i] = angels.Layer("Left wing", AngelVfx.LeftWing);
                angels.rightWings[i] = angels.Layer("Right wing", AngelVfx.LeftWing);
                angels.bodies[i] = angels.Layer("Angel", AngelVfx.Body);
            }
            watching[hero] = angels;
            HeroVfx.Motes(root, hero.position, 0.8f, Glow, 16, 1f);
        }

        /// <summary>
        /// The hero was saved: a pillar of light and a shockwave mark the rescue while the angels soar away.
        /// Plays even if this machine never saw the angels arrive, so the save always reads.
        /// </summary>
        public static void Rescued(Transform root, Transform hero)
        {
            if (root == null || hero == null) return;
            if (!watching.TryGetValue(hero, out var angels) || angels == null || angels.leftAt >= 0f)
            {
                Play(root, hero, 0f);
                angels = watching[hero];
            }
            angels.leftAt = angels.age;
            Vector2 at = hero.position;
            HeroVfx.Pulse(root, at, 2.4f, FlameMesh.Alpha(Glow, 0.9f), 0.5f);
            HeroVfx.Pulse(root, at, 1.2f, Color.white, 0.25f);
            CombatVfx.Ring(root, at, 3f, Glow, 0.6f);
            HeroVfx.Motes(root, at, 1.2f, AbilityCatalog.Gold, 32, 1.4f);
            HeroVfx.Sparks(root, at, Glow, 20, 5f, 0.6f);
        }

        private SpriteRenderer Layer(string name, Sprite sprite)
        {
            var layer = new GameObject(name).AddComponent<SpriteRenderer>();
            layer.transform.SetParent(transform, false);
            layer.sprite = sprite;
            layer.enabled = false;
            return layer;
        }

        private void Update()
        {
            age += Time.deltaTime;
            bool leaving = leftAt >= 0f;
            float end = leaving ? leftAt + LeaveTime : duration + FadeTime;
            if (hero == null || age >= end) { Destroy(gameObject); return; }
            float alpha = Mathf.Clamp01(age / 0.3f) * (leaving ? 1f - (age - leftAt) / LeaveTime : Mathf.Clamp01((duration + FadeTime - age) / FadeTime));
            float rise = leaving ? Mathf.Pow((age - leftAt) / LeaveTime, 2f) * 3f : 0f;
            Vector2 center = hero.position;
            float time = Time.time;

            mesh.Begin();
            if (leaving)
            {
                // The rescue: a pillar of light slams down and thins out, and a golden shockwave rolls outward.
                float t = (age - leftAt) / LeaveTime, fade = 1f - t;
                float width = Mathf.Lerp(1.6f, 0.3f, Mathf.Sqrt(t));
                Vector2 top = center + Vector2.up * 12f, feet = center + Vector2.down * 0.45f;
                mesh.Bar(top, Vector2.down, 12.45f, width, FlameMesh.Alpha(Glow, 0f), FlameMesh.Alpha(Glow, 0.55f * fade));
                mesh.Bar(top, Vector2.down, 12.45f, width * 0.35f, FlameMesh.Alpha(Color.white, 0f), FlameMesh.Alpha(Color.white, 0.8f * fade));
                mesh.Ellipse(feet, 1.1f + t, 0.45f + 0.4f * t, FlameMesh.Alpha(Color.white, 0.6f * fade), FlameMesh.Alpha(Glow, 0f), 28);
                mesh.Ring(feet, 0.6f + 3f * t, 0.12f * fade + 0.02f, FlameMesh.Alpha(Glow, 0.8f * fade), 40);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
            mesh.Ellipse(center + Vector2.down * 0.45f, 0.75f, 0.3f, FlameMesh.Alpha(Glow, (0.25f + 0.1f * pulse) * alpha), FlameMesh.Alpha(Glow, 0f), 24);
            mesh.Ring(center + Vector2.down * 0.45f, 0.6f, 0.03f, FlameMesh.Alpha(Glow, 0.5f * alpha), 32);
            mesh.Commit();

            float px = Scale / AngelVfx.ArtPixelsPerUnit;
            for (int i = 0; i < Count; i++)
            {
                float a = time * 1.3f + i * Mathf.PI * 2f / Count;
                // A tilted circle: wide side to side, shallow front to back, so they pass behind the hero at the top.
                Vector2 feet = center + new Vector2(Mathf.Cos(a) * Orbit, Mathf.Sin(a) * Orbit * 0.35f + 0.35f + 0.06f * Mathf.Sin(time * 4f + i) + rise);
                bool behind = Mathf.Sin(a) > 0f;
                int order = behind ? 3 : 12;
                var tint = new Color(1f, 1f, 1f, alpha * (behind ? 0.75f : 1f));
                Vector2 shoulder = feet + Vector2.up * 10f * px;
                float flap = Mathf.Sin(time * (leaving ? 20f : 10f) + i * 1.7f);
                Place(bodies[i], feet, Scale, 0f, tint, order + 1);
                Place(leftWings[i], shoulder + Vector2.left * px, Scale * 1.5f, -10f + 25f * flap, tint, order);
                Place(rightWings[i], shoulder + Vector2.right * px, Scale * 1.5f, 10f - 25f * flap, tint, order, true);
                if (Time.frameCount % 20 == i && !behind)
                    HeroVfx.Sparks(root, feet + Vector2.up * 0.4f, Glow, 1, 0.8f, 0.5f, Vector2.down, 60f, 0.6f);
            }
        }

        private static void Place(SpriteRenderer layer, Vector2 at, float scale, float angle, Color tint, int order, bool mirror = false)
        {
            layer.enabled = tint.a > 0.01f;
            layer.sortingOrder = order;
            layer.transform.localPosition = at;
            layer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            layer.transform.localScale = new Vector3(mirror ? -scale : scale, scale, 1f);
            layer.color = tint;
        }

        private void OnDestroy()
        {
            if (hero != null && watching.TryGetValue(hero, out var angels) && angels == this) watching.Remove(hero);
            mesh?.Release();
        }
    }
}
