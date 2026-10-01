using System.Collections.Generic;
using UnityEngine;

namespace Slopgame
{
    /// <summary>
    /// The mark on an enemy the Reaper has sown fear into: a ring around it that drains as the fear runs out, with one
    /// pip overhead for every full second left (what Reap would take from it). It goes when the fear ends or the enemy dies.
    /// </summary>
    public sealed class SowMarkVfx : MonoBehaviour
    {
        private static readonly Dictionary<DungeonEnemy, SowMarkVfx> marks = new Dictionary<DungeonEnemy, SowMarkVfx>();
        private DungeonEnemy enemy;
        private FlameMesh mesh;
        private float total;

        /// <summary>Marks <paramref name="enemy"/>, or restarts the mark it already wears from its fear as it stands now.</summary>
        public static void Attach(Transform root, DungeonEnemy enemy)
        {
            if (root == null || enemy == null || !enemy.IsFeared) return;
            if (!marks.TryGetValue(enemy, out var mark) || mark == null)
            {
                mark = new GameObject("Sown fear").AddComponent<SowMarkVfx>();
                mark.transform.SetParent(root, false);
                mark.enemy = enemy;
                mark.mesh = new FlameMesh(mark.gameObject, 6);
                marks[enemy] = mark;
            }
            mark.total = Mathf.Max(0.1f, enemy.FearRemaining);
        }

        private void LateUpdate()
        {
            if (enemy == null || enemy.Health <= 0 || !enemy.IsFeared) { Destroy(gameObject); return; }
            float left = enemy.FearRemaining, fraction = Mathf.Clamp01(left / total);
            Vector2 center = enemy.transform.position;
            float radius = enemy.HitRadius + 0.32f, pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
            mesh.Begin();
            mesh.Ring(center, radius, 0.05f, FlameMesh.Alpha(ReaperAttack.Shade, 0.45f), 40);
            // The bright arc runs clockwise from the top and shortens as the fear drains.
            Color bright = FlameMesh.Alpha(Color.Lerp(ReaperAttack.Soul, Color.white, 0.3f * pulse), 0.95f);
            const int Segments = 40;
            int lit = Mathf.CeilToInt(Segments * fraction);
            for (int i = 0; i < lit; i++)
            {
                float f0 = (float)i / Segments, f1 = Mathf.Min(fraction, (float)(i + 1) / Segments);
                float a0 = Mathf.PI * 0.5f - f0 * Mathf.PI * 2f, a1 = Mathf.PI * 0.5f - f1 * Mathf.PI * 2f;
                mesh.Quad(center + FlameMesh.Polar(a0, radius - 0.06f), center + FlameMesh.Polar(a0, radius + 0.06f),
                    center + FlameMesh.Polar(a1, radius + 0.06f), center + FlameMesh.Polar(a1, radius - 0.06f), bright, bright, bright, bright);
            }
            // The arc's leading end, so the drain is easy to follow.
            mesh.Diamond(center + FlameMesh.Polar(Mathf.PI * 0.5f - fraction * Mathf.PI * 2f, radius), 0.1f, Color.white);
            // One pip per full second left; the last one blinks as it is about to go.
            int seconds = Mathf.FloorToInt(left + 0.0001f);
            for (int i = 0; i < seconds; i++)
            {
                bool going = i == seconds - 1 && left - seconds < 0.3f;
                mesh.Diamond(center + new Vector2((i - (seconds - 1) * 0.5f) * 0.22f, radius + 0.42f), 0.1f,
                    FlameMesh.Alpha(ReaperAttack.Soul, going ? 0.4f + 0.6f * pulse : 1f));
            }
            mesh.Commit();
        }

        private void OnDestroy()
        {
            mesh?.Release();
            if (enemy != null && marks.TryGetValue(enemy, out var mark) && mark == this) marks.Remove(enemy);
            // Enemies that were destroyed leave dead keys behind; sweep them out now and then.
            if (marks.Count > 32)
            {
                var gone = new List<DungeonEnemy>();
                foreach (var pair in marks) if (pair.Key == null || pair.Value == null) gone.Add(pair.Key);
                foreach (var key in gone) marks.Remove(key);
            }
        }
    }
}
