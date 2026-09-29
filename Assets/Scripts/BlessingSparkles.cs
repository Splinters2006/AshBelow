using System;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// Golden sparks drifting around any hero under a Paladin's blessing (local heroes and teammates alike).
    /// Draws in world units even though hero sprites are scaled.
    /// </summary>
    public sealed class BlessingSparkles : MonoBehaviour
    {
        private const int Count = 12;
        private FlameMesh mesh;
        private Func<bool> isBlessed;
        private float strength;

        public static BlessingSparkles Attach(Transform hero, Func<bool> isBlessed)
        {
            var holder = new GameObject("Blessing sparkles");
            holder.transform.SetParent(hero, false);
            Vector3 scale = hero.lossyScale;
            holder.transform.localScale = new Vector3(Safe(scale.x), Safe(scale.y), 1f);
            var sparkles = holder.AddComponent<BlessingSparkles>();
            sparkles.isBlessed = isBlessed;
            sparkles.mesh = new FlameMesh(holder, 8);
            return sparkles;
        }

        private static float Safe(float scale) => Mathf.Abs(scale) > 0.0001f ? 1f / scale : 1f;

        private void LateUpdate()
        {
            strength = Mathf.MoveTowards(strength, isBlessed != null && isBlessed() ? 1f : 0f, Time.deltaTime * 4f);
            mesh.Begin();
            if (strength > 0f)
            {
                float time = Time.time;
                Color gold = AbilityCatalog.Gold;
                for (int i = 0; i < Count; i++)
                {
                    float seed = FlameMesh.Hash(i, 6.6f);
                    float rise = Mathf.Repeat(time * (0.5f + seed * 0.4f) + seed, 1f);
                    float angle = seed * 40f + time * (1.2f + seed);
                    Vector2 p = new Vector2(Mathf.Cos(angle) * (0.38f + seed * 0.2f), -0.5f + rise * 1.4f);
                    float twinkle = 0.6f + 0.4f * Mathf.Sin(time * 11f + i * 2.1f);
                    float alpha = strength * Mathf.Sin(rise * Mathf.PI) * twinkle;
                    mesh.Diamond(p, 0.1f, FlameMesh.Alpha(gold, alpha * 0.35f));
                    mesh.Diamond(p, 0.05f, FlameMesh.Alpha(Color.Lerp(gold, Color.white, 0.6f), alpha));
                }
                mesh.Ring(new Vector2(0f, -0.45f), 0.45f, 0.03f, FlameMesh.Alpha(gold, 0.3f * strength), 32);
            }
            mesh.Commit();
        }

        private void OnDestroy() { mesh?.Release(); }
    }
}
