using System;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Zeal: one small golden flame orbits the Paladin for each stack. At a full count the flames spin faster and
    /// blaze brighter under a halo, so it is obvious the next blessing will be doubled; spending the stacks flares
    /// once. Draws in world units even though hero sprites are scaled.
    /// </summary>
    public sealed class ZealVfx : MonoBehaviour
    {
        private const float Orbit = 0.7f, FlareTime = 0.5f;
        private FlameMesh mesh;
        private Func<int> stacks;
        private Func<bool> visible;
        private int last;
        private float flareAt = -10f;
        private Func<Transform> effectRoot;

        public static ZealVfx Attach(Transform hero, Func<Transform> effectRoot, Func<int> stacks, Func<bool> visible)
        {
            var holder = new GameObject("Zeal");
            holder.transform.SetParent(hero, false);
            Vector3 scale = hero.lossyScale;
            holder.transform.localScale = new Vector3(Safe(scale.x), Safe(scale.y), 1f);
            var zeal = holder.AddComponent<ZealVfx>();
            zeal.effectRoot = effectRoot;
            zeal.stacks = stacks;
            zeal.visible = visible;
            zeal.mesh = new FlameMesh(holder, 11);
            return zeal;
        }

        private static float Safe(float scale) => Mathf.Abs(scale) > 0.0001f ? 1f / scale : 1f;

        private void LateUpdate()
        {
            bool shown = visible != null && visible() && stacks != null;
            int count = shown ? Mathf.Clamp(stacks(), 0, PlayerPowerups.ZealStacks) : 0;
            Vector2 at = transform.position;
            var root = effectRoot != null ? effectRoot() : null;
            // Hidden (dead, between floors) resets quietly, without a flare.
            if (shown && count != last && root != null)
            {
                if (count >= PlayerPowerups.ZealStacks)
                {
                    // Full: a ping tells the Paladin the next blessing is doubled.
                    HeroVfx.Pulse(root, at, 1.3f, AbilityCatalog.Gold, 0.4f);
                    HeroVfx.Motes(root, at, 0.8f, AbilityCatalog.Gold, 16, 0.9f);
                    flareAt = Time.time;
                }
                else if (count == 0 && last >= PlayerPowerups.ZealStacks)
                {
                    // Spent on a blessing: the flames burst outward.
                    HeroVfx.Sparks(root, at, AbilityCatalog.Gold, 16, 4.5f, 0.45f);
                    flareAt = Time.time;
                }
            }
            last = count;

            mesh.Begin();
            float flare = Mathf.Clamp01(1f - (Time.time - flareAt) / FlareTime);
            if (count > 0) Draw(count, flare);
            else if (flare > 0f) mesh.Ring(Vector2.zero, Orbit + (1f - flare) * 1.2f, 0.06f, FlameMesh.Alpha(AbilityCatalog.Gold, flare * 0.8f), 40);
            mesh.Commit();
        }

        private void Draw(int count, float flare)
        {
            bool full = count >= PlayerPowerups.ZealStacks;
            float time = Time.time, pulse = 0.5f + 0.5f * Mathf.Sin(time * (full ? 10f : 3f));
            Color gold = AbilityCatalog.Gold;
            float spin = time * (full ? 3.2f : 1.4f);
            for (int i = 0; i < count; i++)
            {
                // Even spacing around a flattened circle at chest height.
                float a = spin + i * Mathf.PI * 2f / PlayerPowerups.ZealStacks;
                var p = new Vector2(Mathf.Cos(a) * Orbit, Mathf.Sin(a) * Orbit * 0.4f + 0.1f);
                float size = full ? 0.1f + 0.03f * pulse : 0.07f;
                mesh.Disc(p, size * 2.2f, FlameMesh.Alpha(gold, full ? 0.35f : 0.2f), FlameMesh.Alpha(gold, 0f), 12);
                mesh.Diamond(p, size, FlameMesh.Alpha(full ? FlameMesh.Core : gold, 0.95f));
            }
            if (!full) return;
            // A halo over the head and a warm glow underfoot while the doubled blessing is ready.
            var halo = new Vector2(0f, 0.85f + 0.04f * Mathf.Sin(time * 4f));
            mesh.Ellipse(halo, 0.4f, 0.15f, FlameMesh.Alpha(gold, 0.3f), FlameMesh.Alpha(gold, 0f), 20);
            mesh.Ring(halo, 0.28f, 0.05f + 0.02f * pulse, FlameMesh.Alpha(FlameMesh.Core, 0.9f), 28);
            mesh.Ellipse(new Vector2(0f, -0.45f), 0.8f, 0.3f, FlameMesh.Alpha(gold, 0.2f + 0.1f * pulse + 0.3f * flare), FlameMesh.Alpha(gold, 0f), 24);
        }

        private void OnDestroy() { mesh?.Release(); }
    }
}
