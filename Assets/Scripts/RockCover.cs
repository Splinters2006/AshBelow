using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The low rock wall Boulder Toss leaves behind: it stands across the throw for a few seconds and stops every enemy
    /// bolt that flies into it, so the Behemoth and his allies can shelter behind it. It is raised on every machine,
    /// because each machine flies its own copy of the enemies' bolts.
    /// </summary>
    public sealed class RockCover : MonoBehaviour
    {
        public const float Thickness = 0.3f;
        private static readonly List<RockCover> active = new List<RockCover>();
        private Vector2 center, along;
        private float halfWidth, until, born, duration;
        private FlameMesh mesh;
        private float[] heights;

        public static IReadOnlyList<RockCover> Active => active;

        /// <param name="facing">The way the boulder flew; the wall stands across it.</param>
        public static RockCover Raise(DungeonRun run, Vector2 center, Vector2 facing, float width, float duration, bool ghost)
        {
            if (run == null || run.ProjectileRoot == null) return null;
            if (!ghost) CoopFx.RockCover(run, center, facing, width, duration);
            var cover = new GameObject("Rock cover").AddComponent<RockCover>();
            cover.transform.SetParent(run.ProjectileRoot, false);
            cover.center = center;
            cover.along = facing.sqrMagnitude > 0.0001f ? Vector2.Perpendicular(facing.normalized) : Vector2.right;
            cover.halfWidth = Mathf.Max(0.3f, width * 0.5f);
            cover.duration = Mathf.Max(0.1f, duration);
            cover.born = Time.time;
            cover.until = Time.time + cover.duration;
            int rocks = Mathf.Max(3, Mathf.RoundToInt(width / 0.4f));
            cover.heights = new float[rocks];
            for (int i = 0; i < rocks; i++) cover.heights[i] = 0.75f + 0.25f * FlameMesh.Hash(center.x + i, center.y - i);
            cover.mesh = new FlameMesh(cover.gameObject, 6);
            active.Add(cover);
            HeroVfx.Sparks(run.ProjectileRoot, center, SpecimenCatalog.Stone, 10, 2.5f, 0.35f, Vector2.up, 140f);
            return cover;
        }

        /// <summary>Whether a bolt stepping to <paramref name="to"/> runs into a standing rock wall.</summary>
        public static bool StopsBolt(Vector2 from, Vector2 to)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var cover = active[i];
                if (cover == null) { active.RemoveAt(i); continue; }
                if (Time.time < cover.until && cover.DistanceTo(to) <= Thickness) return true;
            }
            return false;
        }

        private float DistanceTo(Vector2 point)
        {
            Vector2 offset = point - center;
            float t = Mathf.Clamp(Vector2.Dot(offset, along), -halfWidth, halfWidth);
            return Vector2.Distance(point, center + along * t);
        }

        private void Update()
        {
            float left = until - Time.time;
            if (left <= -0.35f) { Destroy(gameObject); return; }
            Draw(Mathf.Clamp01((Time.time - born) / 0.15f), left < 0f ? Mathf.Clamp01(1f + left / 0.35f) : 1f);
        }

        private void Draw(float rise, float fade)
        {
            mesh.Begin();
            Color light = FlameMesh.Alpha(new Color(0.78f, 0.76f, 0.72f), fade), mid = FlameMesh.Alpha(SpecimenCatalog.Stone, fade);
            Color dark = FlameMesh.Alpha(new Color(0.34f, 0.33f, 0.32f), fade), shadow = FlameMesh.Alpha(new Color(0f, 0f, 0f, 0.3f), fade);
            int rocks = heights.Length;
            float step = halfWidth * 2f / rocks;
            for (int i = 0; i < rocks; i++)
            {
                Vector2 at = center + along * (-halfWidth + step * (i + 0.5f));
                float size = step * 0.62f, height = size * 1.3f * heights[i] * rise;
                mesh.Ellipse(at + new Vector2(0.03f, -0.05f), size * 1.1f, size * 0.5f, shadow, FlameMesh.Alpha(shadow, 0f), 14);
                mesh.Ellipse(at + Vector2.up * height * 0.35f, size, Mathf.Max(0.05f, height * 0.6f), mid, dark, 14);
                mesh.Ellipse(at + Vector2.up * height * 0.55f + Vector2.left * size * 0.2f, size * 0.45f, Mathf.Max(0.03f, height * 0.25f), light, mid, 10);
            }
            mesh.Commit();
        }

        private void OnDestroy()
        {
            active.Remove(this);
            mesh?.Release();
        }
    }
}
